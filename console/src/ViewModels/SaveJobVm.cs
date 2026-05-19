namespace EasySave.Console.ViewModels;

// ViewModel d'un job individuel — bindé à une ligne du DataGrid.
public sealed class SaveJobVm : ViewModel
{
    public string Id { get; }

    private string _name       = "";
    private string _sourcePath = "";
    private string _destPath   = "";
    private string _saveType   = "Complete";
    private string _status     = "Inactive";
    private float  _progress;
    private bool   _isSelected;

    public string Name       { get => _name;       set { _name       = value; OnPropertyChanged(); } }
    public string SourcePath { get => _sourcePath; set { _sourcePath = value; OnPropertyChanged(); } }
    public string DestPath   { get => _destPath;   set { _destPath   = value; OnPropertyChanged(); } }
    public string SaveType   { get => _saveType;   set { _saveType   = value; OnPropertyChanged(); } }
    public string Status     { get => _status;     set { _status     = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsRunning)); } }
    public float  Progress   { get => _progress;   set { _progress   = value; OnPropertyChanged(); } }
    public bool   IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

    public bool IsRunning => Status is "Active" or "Paused";

    public SaveJobVm(JobDto dto)
    {
        Id          = dto.Id;
        _name       = dto.Name;
        _sourcePath = dto.SourcePath;
        _destPath   = dto.DestPath;
        _saveType   = dto.SaveType;
        _status     = dto.Status;
        _progress   = dto.Progress;
    }

    public void ApplyDto(JobDto dto)
    {
        Name       = dto.Name;
        SourcePath = dto.SourcePath;
        DestPath   = dto.DestPath;
        SaveType   = dto.SaveType;
        Status     = dto.Status;
        Progress   = dto.Progress;
        OnPropertyChanged(nameof(IsRunning));
    }
}
