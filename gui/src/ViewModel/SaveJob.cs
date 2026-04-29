using EasySave.GUI.ViewModels.Base;
using SaveManager;

namespace EasySave.GUI.ViewModels
{
    public class SaveJob : ViewModel
    {
        public SaveInfo Model { get; private set; }

        public SaveJob(SaveInfo model)
        {
            Model = model;

            _name = model.SaveName;
            _sourcePath = model.SourcePath;
            _targetPath = model.DestinationPath;
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

        // --- Méthode utilitaire (Optionnelle) ---

        // Si plus tard tu as besoin de renvoyer un objet SaveInfo mis à jour à ta lib
        // (par exemple lors de l'édition d'une sauvegarde), tu peux utiliser cette méthode :
        public SaveInfo GetUpdatedModel()
        {
            return new SaveInfo
            {
                // On recrée un objet SaveInfo proprement avec les nouvelles valeurs
                SaveId = Model.SaveId,
                SaveName = this.Name,
                SourcePath = this.SourcePath,
                DestinationPath = this.TargetPath
            };
        }
    }
}