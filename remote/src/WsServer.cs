using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace EasySave.Remote;

// Serveur WebSocket basé sur HttpListener (.NET natif, aucune dépendance externe).
// Maintient une liste de connexions actives et broadcast à tous en cas d'événement.
internal sealed class WsServer
{
    private readonly int        _port;
    private readonly JobManager _manager;
    private readonly ConcurrentDictionary<Guid, ClientConn> _clients = new();

    // Une connexion = socket + sémaphore pour garantir des envois séquentiels
    // (WebSocket.SendAsync n'est pas thread-safe sur la même instance).
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
        var listener = new HttpListener();
        // Écoute uniquement sur localhost. Pour accepter des connexions réseau,
        // remplacer "localhost" par "+" et lancer en mode administrateur (Windows).
        listener.Prefixes.Add($"http://localhost:{_port}/ws/");
        listener.Start();
        Console.WriteLine($"[EasySave.Remote] WebSocket prêt — ws://localhost:{_port}/ws/");

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[EasySave.Remote] Erreur listener : {ex.Message}");
                break;
            }

            if (!ctx.Request.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                continue;
            }

            _ = AcceptAsync(ctx, ct);
        }

        listener.Stop();
    }

    private async Task AcceptAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        var wsCtx = await ctx.AcceptWebSocketAsync(null);
        var conn  = new ClientConn(wsCtx.WebSocket);
        var id    = Guid.NewGuid();
        _clients[id] = conn;
        Console.WriteLine($"[EasySave.Remote] Client {id} connecté");

        // Envoi immédiat de la liste des jobs au nouveau client
        await SendAsync(conn, new ServerMsg { Type = "job_list", Jobs = _manager.GetJobDtos() }, ct);

        try
        {
            var buf     = new byte[8192];
            var running = true;

            while (conn.Socket.State == WebSocketState.Open && !ct.IsCancellationRequested && running)
            {
                // Accumulation des frames WebSocket jusqu'à EndOfMessage
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await conn.Socket.ReceiveAsync(buf, ct);
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
            Console.WriteLine($"[EasySave.Remote] Client {id} déconnecté");
        }
    }

    // Broadcast fire-and-forget : ne bloque pas les workers de sauvegarde
    public void Broadcast(ServerMsg msg) => _ = BroadcastAsync(msg);

    private async Task BroadcastAsync(ServerMsg msg)
    {
        foreach (var (id, conn) in _clients)
        {
            if (conn.Socket.State != WebSocketState.Open) { _clients.TryRemove(id, out _); continue; }
            try { await SendAsync(conn, msg, CancellationToken.None); }
            catch  { _clients.TryRemove(id, out _); }
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
}
