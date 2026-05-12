using EasySave.GUI.ViewModels.Base;
using SaveManager;

namespace EasySave.GUI.ViewModels
{
    public class SaveJob : ViewModel
    {
        // Computed from the editable fields so that edits in the UI flow through
        // to the SaveInfo used by the Saver. Id stays stable across edits.
        public SaveInfo Model => new SaveInfo
        {
            SaveId = _id,
            SaveName = Name,
            SourcePath = SourcePath,
            DestinationPath = TargetPath
        };

        private readonly Guid _id;

        public SaveJob(SaveInfo model)
        {
            _id = model.SaveId;
            _name = model.SaveName;
            _sourcePath = model.SourcePath;
            _targetPath = model.DestinationPath;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _sourcePath;
        public string SourcePath
        {
            get => _sourcePath;
            set { _sourcePath = value; OnPropertyChanged(); }
        }

        private string _targetPath;
        public string TargetPath
        {
            get => _targetPath;
            set { _targetPath = value; OnPropertyChanged(); }
        }


        private float _progress;
        public float Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        private string _state = "Prêt";
        public string State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(); }
        }

        private string _type = "Complète";
        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public SaveInfo GetUpdatedModel() => Model;
    }
}