using System.Windows.Input;
using Microsoft.Win32;

namespace EasySave.Console.ViewModels;

// ViewModel de la fenêtre de création / modification d'un job.
public sealed class SaveEditorVm : ViewModel
{
    private string _name       = "";
    private string _sourcePath = "";
    private string _destPath   = "";
    private string _saveType   = "Complete";

    public string Name       { get => _name;       set { _name       = value; OnPropertyChanged(); } }
    public string SourcePath { get => _sourcePath; set { _sourcePath = value; OnPropertyChanged(); } }
    public string DestPath   { get => _destPath;   set { _destPath   = value; OnPropertyChanged(); } }
    public string SaveType   { get => _saveType;   set { _saveType   = value; OnPropertyChanged(); } }

    public List<string> SaveTypes { get; } = ["Complete", "Differential", "Delta"];

    public ICommand BrowseSourceCommand { get; }
    public ICommand BrowseDestCommand   { get; }

    // Création d'un nouveau job
    public SaveEditorVm()
    {
        BrowseSourceCommand = new RelayCommand(_ => Browse(s => SourcePath = s));
        BrowseDestCommand   = new RelayCommand(_ => Browse(s => DestPath   = s));
    }

    // Édition d'un job existant
    public SaveEditorVm(string name, string src, string dest, string type) : this()
    {
        _name       = name;
        _sourcePath = src;
        _destPath   = dest;
        _saveType   = type;
    }

    private static void Browse(Action<string> apply)
    {
        var dlg = new OpenFolderDialog { Title = "Sélectionner un dossier" };
        if (dlg.ShowDialog() == true) apply(dlg.FolderName);
    }
}
