using System.Collections.ObjectModel;
using System.Windows.Input;
using SaveManager;

namespace EasySave.GUI.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private SaveInfo _selectedSave;
        public ObservableCollection<SaveInfo> Saves { get; set; }

        public SaveInfo SelectedSave
        {
            get => _selectedSave;
            set { _selectedSave = value; OnPropertyChanged(); }
        }

        public ICommand SaveCommand { get; }
        public ICommand AddSaveCommand { get; }
        public ICommand OpenOptionsCommand { get; }

        public MainViewModel()
        {
            var stateManager = StateManager.StateManager.Get("./state.json");
            Saves = new ObservableCollection<SaveInfo>(stateManager.GetSaves());

            SaveCommand = new RelayCommand(o => ExecuteSave(), o => SelectedSave != null);
            AddSaveCommand = new RelayCommand(o => OpenAddDialog());
            OpenOptionsCommand = new RelayCommand(o => OpenOptions());
        }

        private void ExecuteSave() { /* Logique CLI */ }
        private void OpenAddDialog() { /* Ouvrir SaveEditorWindow */ }
        private void OpenOptions() { /* Logique Options */ }
    }
}