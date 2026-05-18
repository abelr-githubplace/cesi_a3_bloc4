using Config;
using Save;
using State;
using SaveManager;

namespace EasySave.Remote;

// État interne d'un job côté serveur
internal sealed class JobEntry
{
    public SaveInfo                  Info     { get; set; }
    public string                    Status   { get; set; } = "Inactive";
    public float                     Progress { get; set; }
    public Saver?                    Saver    { get; set; }
    public CancellationTokenSource?  Cts      { get; set; }

    public JobEntry(SaveInfo info) { Info = info; }
}

// Cerveau du serveur : charge les jobs depuis le StateManager,
// reçoit les commandes WebSocket et orchestre les Savers.
internal sealed class JobManager
{
    private readonly ConfigManager              _config;
    private readonly Dictionary<Guid, JobEntry> _jobs = new();
    private readonly object                     _lock = new();

    public WsServer? Server { get; set; }

    public JobManager(ConfigManager config)
    {
        _config = config;
        foreach (var s in config.State.GetSaves())
            _jobs[s.SaveId] = new JobEntry(s);
    }

    // ── Lecture ────────────────────────────────────────────────────────────

    public List<JobDto> GetJobDtos()
    {
        lock (_lock)
        {
            return [.. _jobs.Values.Select(e => new JobDto
            {
                Id         = e.Info.SaveId.ToString(),
                Name       = e.Info.SaveName,
                SourcePath = e.Info.SourcePath,
                DestPath   = e.Info.DestinationPath,
                SaveType   = e.Info.SaveType,
                Status     = e.Status,
                Progress   = e.Progress
            })];
        }
    }

    // ── Dispatch des commandes WebSocket ────────────────────────────────────

    public void Handle(ClientMsg msg)
    {
        switch (msg.Type)
        {
            case "list_jobs":  BroadcastJobList(); break;
            case "start_job":  StartJob(msg.JobId, msg.SaveType ?? "Complete"); break;
            case "stop_job":   StopJob(msg.JobId); break;
            case "pause_job":  PauseJob(msg.JobId); break;
            case "resume_job": ResumeJob(msg.JobId); break;
            case "create_job": CreateJob(msg.Name, msg.SourcePath, msg.DestPath, msg.SaveType); break;
            case "update_job": UpdateJob(msg.JobId, msg.Name, msg.SourcePath, msg.DestPath, msg.SaveType); break;
            case "delete_job": DeleteJob(msg.JobId); break;
        }
    }

    // ── Broadcast helpers ───────────────────────────────────────────────────

    public void BroadcastJobList() =>
        Server?.Broadcast(new ServerMsg { Type = "job_list", Jobs = GetJobDtos() });

    public void BroadcastStateUpdate(Guid jobId, string name, string status, float progress) =>
        Server?.Broadcast(new ServerMsg
        {
            Type     = "state_update",
            JobId    = jobId.ToString(),
            Name     = name,
            Status   = status,
            Progress = progress
        });

    private void BroadcastLog(string message) =>
        Server?.Broadcast(new ServerMsg
        {
            Type    = "log",
            Message = $"[{DateTime.Now:HH:mm:ss}] {message}"
        });

    // ── Commandes ──────────────────────────────────────────────────────────

    private void StartJob(string? jobIdStr, string saveType)
    {
        if (!Guid.TryParse(jobIdStr, out var id)) return;

        JobEntry? entry;
        lock (_lock)
        {
            if (!_jobs.TryGetValue(id, out entry) || entry.Saver != null) return;
            entry.Status   = "Active";
            entry.Progress = 0;
        }
        BroadcastLog($"▶ Sauvegarde démarrée : {entry.Info.SaveName} ({saveType})");
        BroadcastJobList();

        // CTS créé avant le Task.Run pour que Stop() fonctionne même pendant
        // l'énumération des fichiers dans le constructeur de Saver.
        var cts = new CancellationTokenSource();
        entry.Cts = cts;

        _ = Task.Run(() =>
        {
            var progress    = new Progress.Progress();
            var _broadcaster = new ProgressBroadcaster(id, entry, this, progress);

            var action = saveType switch
            {
                "Differential" => SaveManager.Action.DifferentialSave,
                "Delta"        => SaveManager.Action.DeltaSave,
                _              => SaveManager.Action.CompleteSave
            };

            var saver = new Saver(entry.Info, action, progress, _config, cts);
            lock (_lock) { entry.Saver = saver; }

            saver.Start(false);

            lock (_lock)
            {
                entry.Saver  = null;
                entry.Cts    = null;
                entry.Status   = saver.IsStopped ? "Stopped" : "Inactive";
                if (!saver.IsStopped) entry.Progress = 100f;
            }
            if (saver.IsStopped)
                BroadcastLog($"⏹ Sauvegarde arrêtée : {entry.Info.SaveName}");
            else
                BroadcastLog($"✔ Sauvegarde terminée : {entry.Info.SaveName}");
            BroadcastJobList();
        });
    }

    private void StopJob(string? jobIdStr)
    {
        if (!Guid.TryParse(jobIdStr, out var id)) return;
        lock (_lock)
        {
            if (!_jobs.TryGetValue(id, out var entry)) return;
            entry.Cts?.Cancel();
            entry.Saver?.Stop();
        }
    }

    private void PauseJob(string? jobIdStr)
    {
        if (!Guid.TryParse(jobIdStr, out var id)) return;
        lock (_lock)
        {
            if (!_jobs.TryGetValue(id, out var entry) || entry.Saver == null) return;
            entry.Saver.Pause();
            entry.Status = "Paused";
            BroadcastLog($"⏸ Mise en pause : {entry.Info.SaveName}");
        }
        BroadcastJobList();
    }

    private void ResumeJob(string? jobIdStr)
    {
        if (!Guid.TryParse(jobIdStr, out var id)) return;
        lock (_lock)
        {
            if (!_jobs.TryGetValue(id, out var entry) || entry.Saver == null) return;
            entry.Saver.Resume();
            entry.Status = "Active";
            BroadcastLog($"▷ Reprise : {entry.Info.SaveName}");
        }
        BroadcastJobList();
    }

    private void CreateJob(string? name, string? sourcePath, string? destPath, string? saveType)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(sourcePath) ||
            string.IsNullOrWhiteSpace(destPath)) return;

        var info = new SaveInfo
        {
            SaveId          = Guid.NewGuid(),
            SaveName        = name,
            SourcePath      = sourcePath,
            DestinationPath = destPath,
            SaveType        = saveType ?? "Complete"
        };

        _config.State.Save(new SaveState
        {
            Id              = info.SaveId,
            Name            = info.SaveName,
            SourcePath      = info.SourcePath,
            DestinationPath = info.DestinationPath,
            LastActionTime  = DateTime.Now,
            Status          = Status.Inactive,
            SaveType        = info.SaveType
        });

        lock (_lock) { _jobs[info.SaveId] = new JobEntry(info); }
        BroadcastLog($"＋ Job créé : {info.SaveName}");
        BroadcastJobList();
    }

    private void UpdateJob(string? jobIdStr, string? name, string? sourcePath, string? destPath, string? saveType)
    {
        if (!Guid.TryParse(jobIdStr, out var id)) return;
        lock (_lock)
        {
            if (!_jobs.TryGetValue(id, out var entry) || entry.Saver != null) return;

            var updated = entry.Info with
            {
                SaveName        = name        ?? entry.Info.SaveName,
                SourcePath      = sourcePath  ?? entry.Info.SourcePath,
                DestinationPath = destPath    ?? entry.Info.DestinationPath,
                SaveType        = saveType    ?? entry.Info.SaveType
            };

            _config.State.Save(new SaveState
            {
                Id              = updated.SaveId,
                Name            = updated.SaveName,
                SourcePath      = updated.SourcePath,
                DestinationPath = updated.DestinationPath,
                LastActionTime  = DateTime.Now,
                Status          = Status.Inactive,
                SaveType        = updated.SaveType
            });
            entry.Info = updated;
            BroadcastLog($"✎ Job modifié : {updated.SaveName}");
        }
        BroadcastJobList();
    }

    private void DeleteJob(string? jobIdStr)
    {
        if (!Guid.TryParse(jobIdStr, out var id)) return;
        string? deletedName = null;
        lock (_lock)
        {
            if (!_jobs.TryGetValue(id, out var entry) || entry.Saver != null) return;
            deletedName = entry.Info.SaveName;
            _jobs.Remove(id);
            _config.State.Delete(id);
        }
        if (deletedName != null) BroadcastLog($"✕ Job supprimé : {deletedName}");
        BroadcastJobList();
    }
}
