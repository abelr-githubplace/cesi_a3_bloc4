using Config;
using EasySave.Remote;

// Lecture du port optionnel : EasySave.Remote.exe --port 8080
int port = 8080;
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--port" && int.TryParse(args[i + 1], out int p))
        port = p;

var config  = ConfigManager.Get();
var manager = new JobManager(config);
var server  = new WsServer(port, manager);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine("[EasySave.Remote] Démarrage — Ctrl+C pour arrêter");
await server.RunAsync(cts.Token);
Console.WriteLine("[EasySave.Remote] Arrêté.");
