using System.Windows.Input;
using EasySave.Console.Services;

namespace EasySave.Console.ViewModels;

// Écran de connexion : l'utilisateur saisit hôte + port et clique sur "Connecter".
public sealed class ConnectionVm : ViewModel
{
    private string _host         = "localhost";
    private string _port         = "8080";
    private string _error        = "";
    private bool   _isConnecting;

    public string Host  { get => _host;  set { _host  = value; OnPropertyChanged(); } }
    public string Port  { get => _port;  set { _port  = value; OnPropertyChanged(); } }
    public string Error { get => _error; set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
    public bool   HasError    => !string.IsNullOrEmpty(_error);
    public bool   IsConnecting
    {
        get => _isConnecting;
        set { _isConnecting = value; OnPropertyChanged(); }
    }

    // Déclenché quand la connexion WebSocket est établie
    public event Action<WsClient>? Connected;

    public ICommand ConnectCommand { get; }

    public ConnectionVm()
    {
        ConnectCommand = new RelayCommand(
            async _ => await TryConnectAsync(),
            _        => !_isConnecting);
    }

    private async Task TryConnectAsync()
    {
        if (!int.TryParse(Port, out int port))
        {
            Error = "Le port doit être un nombre entier.";
            return;
        }

        IsConnecting = true;
        Error        = "";
        try
        {
            var client = new WsClient();
            await client.ConnectAsync(Host, port, CancellationToken.None);
            Connected?.Invoke(client);
        }
        catch (Exception ex)
        {
            Error = $"Connexion échouée : {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }
}
