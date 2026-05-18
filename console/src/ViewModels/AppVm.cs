using EasySave.Console.Services;

namespace EasySave.Console.ViewModels;

// ViewModel racine : contrôle la navigation entre l'écran de connexion et l'écran principal.
// Le ContentControl de MainWindow se bind sur CurrentPage ; les DataTemplates dans App.xaml
// font le mapping ViewModel → Vue.
public sealed class AppVm : ViewModel
{
    private object _currentPage = null!;
    public object CurrentPage
    {
        get => _currentPage;
        private set { _currentPage = value; OnPropertyChanged(); }
    }

    public AppVm()
    {
        var connection = new ConnectionVm();
        connection.Connected += OnConnected;
        CurrentPage = connection;
    }

    private void OnConnected(WsClient client)
        => CurrentPage = new MainVm(client);
}
