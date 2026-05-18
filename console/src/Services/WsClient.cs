using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace EasySave.Console.Services;

// Client WebSocket : se connecte au serveur EasySave.Remote,
// écoute les événements en arrière-plan et expose des méthodes d'envoi.
public sealed class WsClient : IDisposable
{
    private readonly ClientWebSocket _ws       = new();
    private readonly SemaphoreSlim   _sendLock = new(1, 1);

    public event Action<List<JobDto>>? JobListReceived;
    public event Action<ServerMsg>?    StateUpdated;
    public event Action<string>?       LogReceived;
    public event Action<string>?       ErrorReceived;
    public event Action?               Disconnected;

    public async Task ConnectAsync(string host, int port, CancellationToken ct)
    {
        await _ws.ConnectAsync(new Uri($"ws://{host}:{port}/ws/"), ct);
        _ = ReceiveLoopAsync(ct);
    }

    // Boucle de réception — tourne sur un thread de pool jusqu'à la déconnexion.
    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buf = new byte[16384];
        try
        {
            while (_ws.State == WebSocketState.Open)
            {
                // Accumulation des frames WebSocket jusqu'à EndOfMessage
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buf, ct);
                    if (result.MessageType == WebSocketMessageType.Close) goto done;
                    ms.Write(buf, 0, result.Count);
                } while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(ms.ToArray());
                var msg  = JsonSerializer.Deserialize<ServerMsg>(json);
                if (msg == null) continue;

                switch (msg.Type)
                {
                    case "job_list":     JobListReceived?.Invoke(msg.Jobs ?? []); break;
                    case "state_update": StateUpdated?.Invoke(msg); break;
                    case "log":          LogReceived?.Invoke(msg.Message ?? ""); break;
                    case "error":        ErrorReceived?.Invoke(msg.Message ?? "Erreur inconnue"); break;
                }
            }
            done:;
        }
        catch (OperationCanceledException) { }
        catch { }
        finally { Disconnected?.Invoke(); }
    }

    public async Task SendAsync(ClientMsg msg, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
        await _sendLock.WaitAsync(ct);
        try
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        finally { _sendLock.Release(); }
    }

    public void Dispose()
    {
        _ws.Dispose();
        _sendLock.Dispose();
    }
}
