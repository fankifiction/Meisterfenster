using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wochenplanung.Infrastructure;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.Services;

namespace Wochenplanung.Presentation.ViewModels;

public partial class DatabaseSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsServerMode))]
    private bool _isLocalMode = true;

    [ObservableProperty]
    private string _server = string.Empty;

    [ObservableProperty]
    private string _databaseName = "Wochenplanung";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSqlLogin))]
    private bool _useWindowsAuthentication = true;

    [ObservableProperty]
    private string _user = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _testSucceeded;

    [ObservableProperty]
    private bool _copyLocalData = true;

    public bool IsServerMode
    {
        get => !IsLocalMode;
        set
        {
            if (IsLocalMode == !value)
                return;
            IsLocalMode = !value;
            OnPropertyChanged();
        }
    }

    public bool ShowSqlLogin => !UseWindowsAuthentication;

    public DatabaseSettingsViewModel()
    {
        var current = DatabaseSettingsStore.Load();
        IsLocalMode = !string.Equals(current.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase)
                      || string.IsNullOrWhiteSpace(current.Server);
        Server = current.Server;
        DatabaseName = string.IsNullOrWhiteSpace(current.Database) ? "Wochenplanung" : current.Database;
        UseWindowsAuthentication = current.UseWindowsAuthentication;
        User = current.User ?? string.Empty;
        Password = current.Password ?? string.Empty;
        CopyLocalData = IsLocalMode;
    }

    public DatabaseSettings ToSettings()
    {
        var current = DatabaseSettingsStore.Load();
        return new DatabaseSettings
        {
            Provider = IsLocalMode ? "Sqlite" : "SqlServer",
            Server = Server.Trim(),
            Database = string.IsNullOrWhiteSpace(DatabaseName) ? "Wochenplanung" : DatabaseName.Trim(),
            UseWindowsAuthentication = UseWindowsAuthentication,
            User = UseWindowsAuthentication ? string.Empty : User,
            Password = UseWindowsAuthentication ? string.Empty : Password,
            SkipLocalImportPrompt = current.SkipLocalImportPrompt
        };
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        StatusMessage = UiText.Tr("Prüfe Verbindung …", "Testing connection …", "Test de la connexion …", "Verifica connessione …");
        TestSucceeded = false;
        var options = DatabaseSettingsStore.ToOptions(ToSettings());
        var (ok, message) = await DependencyInjection.TestConnectionAsync(options);
        TestSucceeded = ok;
        StatusMessage = ok
            ? message == "database-missing"
                ? UiText.Tr(
                    "Server erreichbar. Die Datenbank wird beim Start angelegt.",
                    "Server reachable. The database will be created on startup.",
                    "Serveur joignable. La base sera créée au démarrage.",
                    "Server raggiungibile. Il database verrà creato all'avvio.")
                : UiText.Tr("Verbindung erfolgreich.", "Connection succeeded.", "Connexion réussie.", "Connessione riuscita.")
            : $"{UiText.Tr("Verbindung fehlgeschlagen", "Connection failed", "Échec de la connexion", "Connessione non riuscita")}: {message}";
    }
}
