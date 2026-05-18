using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EasySave.Remote;

// Serveur WebSocket sur TcpListener — pas besoin de droits administrateur,
// fonctionne sur toutes les interfaces réseau (contrairement à HttpListener avec "+").
internal sealed class WsServer
{
    private readonly int        _port;
    private readonly JobManager _manager;
    private readonly ConcurrentDictionary<Guid, ClientConn> _clients = new();

    private sealed class ClientConn(WebSocket socket)
    {
        public WebSocket     Socket { get; } = socket;
        public SemaphoreSlim Lock   { get; } = new(1, 1);
    }

    public WsServer(int port, JobManager manager)
    {
        _port    = port;
        _manager = manager;
        _manager.Server = this;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        Console.WriteLine($"[EasySave.Remote] WebSocket prêt — ws://0.0.0.0:{_port}/ws/");
        Console.WriteLine($"[EasySave.Remote] IP locale : {GetLocalIp()}:{_port}");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient tcp;
                try   { tcp = await listener.AcceptTcpClientAsync(ct); }
                catch (OperationCanceledException) { break; }

                _ = HandleTcpAsync(tcp, ct);
            }
        }
        finally { listener.Stop(); }
    }

    // ── Handshake HTTP → WebSocket ─────────────────────────────────────────

    private async Task HandleTcpAsync(TcpClient tcp, CancellationToken ct)
    {
        tcp.NoDelay = true;
        var stream = tcp.GetStream();

        // Lecture de la requête HTTP d'upgrade
        var buf      = new byte[4096];
        int bytesRead;
        try   { bytesRead = await stream.ReadAsync(buf, ct); }
        catch { tcp.Dispose(); return; }

        var request = Encoding.UTF8.GetString(buf, 0, bytesRead);

        // Extraction du Sec-WebSocket-Key
        string? wsKey = null;
        foreach (var line in request.Split("\r\n"))
        {
            if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
            {
                wsKey = line[(line.IndexOf(':') + 1)..].Trim();
                break;
            }
        }

        if (wsKey == null) { tcp.Dispose(); return; }

        // Calcul du Sec-WebSocket-Accept (RFC 6455)
        var accept = Convert.ToBase64String(
            SHA1.HashData(Encoding.UTF8.GetBytes(wsKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

        var response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
           $"Sec-WebSocket-Accept: {accept}\r\n\r\n";

        try   { await stream.WriteAsync(Encoding.UTF8.GetBytes(response), ct); }
        catch { tcp.Dispose(); return; }

        // Création du WebSocket côté serveur sur le flux TCP brut
        var ws = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions
        {
            IsServer          = true,
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });

        await AcceptWebSocketAsync(ws, tcp, ct);
    }

    // ── Boucle de réception WebSocket ──────────────────────────────────────

    private async Task AcceptWebSocketAsync(WebSocket ws, TcpClient tcp, CancellationToken ct)
    {
        var conn = new ClientConn(ws);
        var id   = Guid.NewGuid();
        _clients[id] = conn;
        Console.WriteLine($"[EasySave.Remote] Client {id} connecté");

        // Envoi immédiat de la liste des jobs
        await SendAsync(conn, new ServerMsg { Type = "job_list", Jobs = _manager.GetJobDtos() }, ct);

        try
        {
            var buf     = new byte[8192];
            var running = true;

            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested && running)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buf, ct);
                    if (result.MessageType == WebSocketMessageType.Close) { running = false; break; }
                    ms.Write(buf, 0, result.Count);
                } while (!result.EndOfMessage);

                if (!running) break;

                var json = Encoding.UTF8.GetString(ms.ToArray());
                var msg  = JsonSerializer.Deserialize<ClientMsg>(json);
                if (msg != null) _manager.Handle(msg);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine($"[EasySave.Remote] Client {id} erreur : {ex.Message}"); }
        finally
        {
            _clients.TryRemove(id, out _);
            tcp.Dispose();
            Console.WriteLine($"[EasySave.Remote] Client {id} déconnecté");
        }
    }

    // ── Broadcast ──────────────────────────────────────────────────────────

    public void Broadcast(ServerMsg msg) => _ = BroadcastAsync(msg);

    private async Task BroadcastAsync(ServerMsg msg)
    {
        foreach (var (id, conn) in _clients)
        {
            if (conn.Socket.State != WebSocketState.Open) { _clients.TryRemove(id, out _); continue; }
            try   { await SendAsync(conn, msg, CancellationToken.None); }
            catch { _clients.TryRemove(id, out _); }
        }
    }

    private static async Task SendAsync(ClientConn conn, ServerMsg msg, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
        await conn.Lock.WaitAsync(ct);
        try
        {
            if (conn.Socket.State == WebSocketState.Open)
                await conn.Socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        finally { conn.Lock.Release(); }
    }

    // ── Utilitaire ─────────────────────────────────────────────────────────

    private static string GetLocalIp()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect("8.8.8.8", 80);
            return ((IPEndPoint)s.LocalEndPoint!).Address.ToString();
        }
        catch { return "?.?.?.?"; }
    }
}
