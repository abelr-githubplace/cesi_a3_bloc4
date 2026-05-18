namespace EasySave.Remote;

// ISubscriber branché sur Progress.Progress d'un Saver actif.
// À chaque avancement de fichier, notifie tous les clients WebSocket.
internal sealed class ProgressBroadcaster : Observer.ISubscriber
{
    private readonly Guid            _jobId;
    private readonly JobEntry        _entry;
    private readonly JobManager      _manager;
    private readonly Progress.Progress _progress;

    public ProgressBroadcaster(Guid jobId, JobEntry entry, JobManager manager, Progress.Progress progress)
    {
        _jobId    = jobId;
        _entry    = entry;
        _manager  = manager;
        _progress = progress;
        _progress.Subscribe(this);
    }

    public void Update()
    {
        float p = _progress.GetProgress();
        _entry.Progress = p;
        _manager.BroadcastStateUpdate(_jobId, _entry.Info.SaveName, _entry.Status, p);
    }
}
