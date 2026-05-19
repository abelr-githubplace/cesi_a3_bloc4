using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EasySave.Console.Services;
using EasySave.Console.Views;

namespace EasySave.Console.ViewModels;

// ViewModel principal : reçoit les événements WebSocket et pilote la liste de jobs.
public sealed class MainVm : ViewModel
{
    private readonly WsClient   _client;
    private readonly LogWriter  _logWriter;

    public ObservableCollection<SaveJobVm> Jobs { get; } = [];
    public ObservableCollection<string>    Logs { get; } = [];

    private SaveJobVm? _selected;
    public SaveJobVm? SelectedJob
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); }
    }

    public ICommand AddCommand    { get; }
    public ICommand EditCommand   { get; }
    public ICommand DeleteCommand { get; }
    public ICommand StartCommand  { get; }
    public ICommand StopCommand   { get; }
    public ICommand PauseCommand  { get; }
    public ICommand ResumeCommand { get; }

    public MainVm(WsClient client, LogWriter logWriter)
    {
        _client    = client;
        _logWriter = logWriter;
        _client.JobListReceived += OnJobList;
        _client.StateUpdated    += OnStateUpdate;
        _client.LogReceived     += msg => Ui(() => AddLog(msg));
        _client.ErrorReceived   += msg => Ui(() => AddLog($"[Erreur] {msg}"));
        _client.Disconnected    += ()  => Ui(() => AddLog("[Serveur] Déconnexion"));

        AddCommand    = new RelayCommand(_ => AddJob());
        EditCommand   = new RelayCommand(_ => EditJob(),   _ => SelectedJob != null && !SelectedJob.IsRunning);
        DeleteCommand = new RelayCommand(_ => DeleteJob(), _ => SelectedJob != null && !SelectedJob.IsRunning);
        StartCommand  = new RelayCommand(_ => SendJob("start_job", j => new ClientMsg { Type = "start_job", JobId = j.Id, SaveType = j.SaveType }),
                                         _ => SelectedJob?.Status is "Inactive" or "Stopped");
        PauseCommand  = new RelayCommand(_ => Send("pause_job"),  _ => SelectedJob?.Status == "Active");
        ResumeCommand = new RelayCommand(_ => Send("resume_job"), _ => SelectedJob?.Status == "Paused");
        StopCommand   = new RelayCommand(_ => Send("stop_job"),   _ => SelectedJob?.Status is "Active" or "Paused");

        // Demande la liste initiale des jobs
        _ = _client.SendAsync(new ClientMsg { Type = "list_jobs" });
    }

    // ── Handlers WebSocket ─────────────────────────────────────────────────

    private void OnJobList(List<JobDto> dtos)
    {
        Ui(() =>
        {
            // Mise à jour des jobs existants, ajout des nouveaux
            foreach (var dto in dtos)
            {
                var vm = Jobs.FirstOrDefault(j => j.Id == dto.Id);
                if (vm != null) vm.ApplyDto(dto);
                else            Jobs.Add(new SaveJobVm(dto));
            }
            // Suppression des jobs qui n'existent plus côté serveur
            var ids = dtos.Select(d => d.Id).ToHashSet();
            foreach (var orphan in Jobs.Where(j => !ids.Contains(j.Id)).ToList())
                Jobs.Remove(orphan);
            CommandManager.InvalidateRequerySuggested();
        });
    }

    private void OnStateUpdate(ServerMsg msg)
    {
        Ui(() =>
        {
            var vm = Jobs.FirstOrDefault(j => j.Id == msg.JobId);
            if (vm == null) return;
            if (msg.Status   != null) vm.Status   = msg.Status;
            if (msg.Progress != null) vm.Progress  = msg.Progress.Value;
            CommandManager.InvalidateRequerySuggested();
        });
    }

    // ── Commandes ──────────────────────────────────────────────────────────

    private void AddJob()
    {
        var vm  = new SaveEditorVm();
        var win = new SaveEditorWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        if (win.ShowDialog() == true)
            _ = _client.SendAsync(new ClientMsg
            {
                Type       = "create_job",
                Name       = vm.Name,
                SourcePath = vm.SourcePath,
                DestPath   = vm.DestPath,
                SaveType   = vm.SaveType
            });
    }

    private void EditJob()
    {
        var job = SelectedJob;
        if (job == null) return;
        var vm  = new SaveEditorVm(job.Name, job.SourcePath, job.DestPath, job.SaveType);
        var win = new SaveEditorWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        if (win.ShowDialog() == true)
            _ = _client.SendAsync(new ClientMsg
            {
                Type       = "update_job",
                JobId      = job.Id,
                Name       = vm.Name,
                SourcePath = vm.SourcePath,
                DestPath   = vm.DestPath,
                SaveType   = vm.SaveType
            });
    }

    private void DeleteJob()
    {
        if (SelectedJob == null) return;
        _ = _client.SendAsync(new ClientMsg { Type = "delete_job", JobId = SelectedJob.Id });
    }

    private void Send(string type)
    {
        if (SelectedJob == null) return;
        _ = _client.SendAsync(new ClientMsg { Type = type, JobId = SelectedJob.Id });
    }

    private void SendJob(string type, Func<SaveJobVm, ClientMsg> build)
    {
        if (SelectedJob == null) return;
        _ = _client.SendAsync(build(SelectedJob));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void AddLog(string msg)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        Logs.Insert(0, entry);
        if (Logs.Count > 200) Logs.RemoveAt(Logs.Count - 1);
        _logWriter.Write(entry);
    }

    // Toutes les modifications de collections WPF doivent se faire sur le thread UI.
    private static void Ui(Action action) =>
        Application.Current?.Dispatcher.Invoke(action);
}
