using System.Windows;

namespace EasySave.Console.Views;

public partial class SaveEditorWindow : Window
{
    public SaveEditorWindow() => InitializeComponent();

    private void OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
