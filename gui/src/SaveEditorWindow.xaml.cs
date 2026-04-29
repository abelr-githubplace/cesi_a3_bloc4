using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EasySave.GUI.Views
{
    public partial class SaveEditorWindow : Window
    {
        public SaveEditorWindow()
        {
            InitializeComponent();
        }

        // Gestion de la fermeture de la fenêtre côté UI pure
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Le ViewModel s'occupe de la logique métier via la SaveCommand,
            // ici on ferme juste la fenêtre de dialogue.
            DialogResult = true;
            Close();
        }
    }
}
