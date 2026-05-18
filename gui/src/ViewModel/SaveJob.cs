using EasySave.GUI.Helpers;
using EasySave.GUI.ViewModels.Base;
using SaveManager;

namespace EasySave.GUI.ViewModels
{
    public enum SaveType { Complete, Differential, Delta }

    public class SaveJob : ViewModel
    {
        public SaveInfo Model { get; private set; }

        public SaveJob(SaveInfo model, SaveType type)
        {
            Model = model;

            _name = model.SaveName;
            _sourcePath = model.SourcePath;
            _targetPath = model.DestinationPath;
            _type = type;
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

        private string _state = TranslationSource.Instance["Ready"];
        public string State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(); }
        }

        private SaveType _type;
        public SaveType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public SaveInfo GetUpdatedModel()
        {
            return new SaveInfo
            {
                SaveId = Model.SaveId,
                SaveName = this.Name,
                SourcePath = this.SourcePath,
                DestinationPath = this.TargetPath
            };
        }
    }
}