using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PdfSharp.Fonts;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wochenplanung.Application.Interfaces;
using Wochenplanung.Infrastructure;
using Wochenplanung.Infrastructure.Data;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.Services;
using Wochenplanung.Presentation.ViewModels;
using Wochenplanung.Presentation.Views;
using MessageBox = Wochenplanung.Presentation.Services.ModernMessageBox;

namespace Wochenplanung.Presentation;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private IServiceScope? _appScope;

    private static ResourceDictionary? _lightThemeDict;
    private static ResourceDictionary? _darkThemeDict;
    public static bool IsDarkMode { get; private set; }
    public static bool JumpToDemoWeek { get; private set; }
    public static bool ForceLocalDemo { get; private set; }

    public static void ToggleTheme()
    {
        SetTheme(!IsDarkMode);
    }

    public static void SetTheme(bool dark)
    {
        IsDarkMode = dark;
        ApplyTheme(IsDarkMode);
        var path = AppPaths.Combine("dark_mode.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, IsDarkMode ? "1" : "0");
    }

    public static void ApplyTheme(bool dark)
    {
        if (Current?.Resources == null)
            return;

        if (_lightThemeDict == null)
        {
            _lightThemeDict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Wochenplanung.Presentation;component/Themes/LightTheme.xaml")
            };
        }

        if (_darkThemeDict == null)
        {
            _darkThemeDict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Wochenplanung.Presentation;component/Themes/DarkTheme.xaml")
            };
        }

        var source = dark ? _darkThemeDict : _lightThemeDict;
        foreach (var key in source.Keys)
            Current.Resources[key] = source[key];
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Window? splash = null;

        try
        {
            WriteStartupLog($"start path={Environment.ProcessPath}");
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            splash = CreateStartupStatusWindow(
                UiText.Tr("Meisterfenster startet …", "Meisterfenster is starting …", "Meisterfenster démarre …", "Meisterfenster si avvia …"));
            splash.Show();
            splash.Activate();
            await Dispatcher.Yield(DispatcherPriority.Render);

            if (await TryRelaunchFromLocalCopyAsync(splash))
            {
                CloseWindow(ref splash);
                Shutdown();
                return;
            }

            GlobalFontSettings.UseWindowsFontsUnderWindows = true;

            var seedDemo = e.Args.Any(a =>
                string.Equals(a, "--seed-demo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/seed-demo", StringComparison.OrdinalIgnoreCase));
            if (seedDemo)
            {
                JumpToDemoWeek = true;
                ForceLocalDemo = true;
            }

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            var dbOptions = _serviceProvider.GetRequiredService<DatabaseOptions>();
            var settings = ForceLocalDemo ? new DatabaseSettings() : DatabaseSettingsStore.Load();

            if (dbOptions.IsServer)
            {
                SetSplashText(
                    splash,
                    string.Format(
                        UiText.Tr(
                            "Verbinde mit SQL Server „{0}“ …\n\nWenn das länger als ein paar Sekunden dauert, erreicht dieser PC den Server nicht.",
                            "Connecting to SQL Server \"{0}\" …\n\nIf this stays for more than a few seconds, this PC cannot reach the server.",
                            "Connexion au serveur SQL « {0} » …\n\nSi cela dure plus de quelques secondes, ce PC n'atteint pas le serveur.",
                            "Connessione a SQL Server \"{0}\" …\n\nSe resta così più di qualche secondo, questo PC non raggiunge il server."),
                        dbOptions.DisplayName));
                await Dispatcher.Yield(DispatcherPriority.Render);
            }

            try
            {
                if (dbOptions.IsServer
                    && !settings.UseWindowsAuthentication
                    && settings.PasswordCouldNotBeRead)
                {
                    throw new InvalidOperationException("password-reentry");
                }

                using var startupScope = _serviceProvider.CreateScope();
                var startup = startupScope.ServiceProvider.GetRequiredService<IDatabaseStartup>();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(SqlServerConnection.StartupTimeoutSeconds));
                await startup.InitializeAsync(cts.Token).WaitAsync(cts.Token).ConfigureAwait(true);
                if (seedDemo)
                {
                    var templates = startupScope.ServiceProvider.GetRequiredService<TaskTemplateStorage>();
                    var startupDb = startupScope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                    await DemoDataSeeder.SeedAsync(startupDb, templates).ConfigureAwait(true);
                    logger.LogInformation("Demo-Daten (Feedback) wurden geladen.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, seedDemo ? "Fehler bei Datenbank/Demo-Daten" : "Fehler bei der Datenbankinitialisierung");
                if (dbOptions.IsServer)
                {
                    if (OfferDatabaseSettings(FormatServerStartupError(ex, dbOptions)))
                    {
                        CloseWindow(ref splash);
                        Restart();
                    }
                    else
                    {
                        CloseWindow(ref splash);
                        Shutdown();
                    }
                    return;
                }

                MessageBox.Show(
                    $"{UiText.Tr("Fehler bei der Datenbankinitialisierung", "Database initialization error", "Erreur d'initialisation de la base de données", "Errore durante l'inizializzazione del database")}:\n{ex.InnerException?.Message ?? ex.Message}\n\n{UiText.Tr("Die Anwendung wird trotzdem gestartet.", "The application will still start.", "L'application démarrera quand même.", "L'applicazione verrà comunque avviata.")}",
                    UiText.Tr("Warnung", "Warning", "Avertissement", "Avviso"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            var darkPath = AppPaths.Combine("dark_mode.txt");
            if (File.Exists(darkPath) && File.ReadAllText(darkPath).Trim() == "1")
            {
                IsDarkMode = true;
                ApplyTheme(true);
            }

            _appScope = _serviceProvider.CreateScope();
            var mainWindow = _appScope.ServiceProvider.GetRequiredService<MainWindow>();
            if (!seedDemo && dbOptions.IsServer && !DatabaseSettingsStore.Load().SkipLocalImportPrompt)
            {
                RoutedEventHandler? onLoaded = null;
                onLoaded = async (_, _) =>
                {
                    mainWindow.Loaded -= onLoaded;
                    var importResult = await LocalDataImportPrompt.ImportIfNeededAsync(dbOptions, logger, promptUser: true);
                    if (importResult.Copied || importResult.Status == LocalDataMigrationStatus.Failed)
                        LocalDataImportPrompt.ShowResult(importResult);
                };
                mainWindow.Loaded += onLoaded;
            }

            mainWindow.Show();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            CloseWindow(ref splash);
            mainWindow.Activate();
            mainWindow.WindowState = WindowState.Maximized;
            mainWindow.Topmost = true;
            mainWindow.Topmost = false;
        }
        catch (Exception ex)
        {
            CloseWindow(ref splash);
            WriteStartupLog("fatal " + ex);
            MessageBox.Show(
                $"{UiText.Tr("Ein kritischer Fehler ist aufgetreten", "A critical error occurred", "Une erreur critique est survenue", "Si è verificato un errore critico")}:\n{ex.Message}\n\n{UiText.Tr("Die Anwendung wird beendet.", "The application will be closed.", "L'application va se fermer.", "L'applicazione verrà chiusa.")}",
                UiText.Tr("Kritischer Fehler", "Critical error", "Erreur critique", "Errore critico"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    public static void Restart()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe))
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        Current.Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteStartupLog("crash " + e.Exception);
        e.Handled = true;
        MessageBox.Show(
            $"{UiText.Tr("Ein unerwarteter Fehler ist aufgetreten", "An unexpected error occurred", "Une erreur inattendue s'est produite", "Si è verificato un errore imprevisto")}:\n{e.Exception.Message}",
            UiText.Tr("Fehler", "Error", "Erreur", "Errore"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void WriteStartupLog(string message)
    {
        try
        {
            var dir = Path.Combine(AppPaths.Root, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "startup.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // Best-effort breadcrumb when a colleague PC never shows a window.
        }
    }

    private static async Task<bool> TryRelaunchFromLocalCopyAsync(Window splash)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            return false;
        if (!AppLaunchLocation.IsNetworkPath(exe) || AppLaunchLocation.IsLocalAppCopy(exe))
            return false;

        WriteStartupLog("network-launch " + exe);
        var continueCopy = MessageBox.Show(
            UiText.Tr(
                "Meisterfenster darf nicht direkt von der Freigabe (z. B. H:) starten.\n\nDas Programm wird jetzt auf diesen PC kopiert. Danach bitte immer die lokale Kopie oder „Meisterfenster-Starten.bat“ verwenden.\n\nJetzt lokal kopieren und starten?",
                "Do not start Meisterfenster directly from a network share (e.g. H:).\n\nThe program will be copied to this PC. Afterwards always use the local copy or Meisterfenster-Starten.bat.\n\nCopy locally and start now?",
                "Ne démarrez pas Meisterfenster depuis un partage réseau.\n\nCopier localement et démarrer ?",
                "Non avviare Meisterfenster dalla condivisione di rete.\n\nCopiare in locale e avviare?"),
            "Meisterfenster",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (continueCopy != MessageBoxResult.Yes)
            return true;

        SetSplashText(
            splash,
            UiText.Tr(
                "Meisterfenster liegt auf dem Server. Das Programm wird zuerst auf diesen PC kopiert …\n\nBitte die EXE nicht direkt von der Freigabe starten. Nächstes Mal „Meisterfenster-Starten.bat“ verwenden.",
                "Meisterfenster is on the server. Copying the program to this PC first …\n\nDo not start the EXE directly from the share. Next time use Meisterfenster-Starten.bat.",
                "Meisterfenster est sur le serveur. Copie du programme sur ce PC …",
                "Meisterfenster è sul server. Copia del programma su questo PC …"));
        await Dispatcher.Yield(DispatcherPriority.Render);

        try
        {
            var destExe = await Task.Run(() => AppLaunchLocation.CopyToLocalApp(exe)).ConfigureAwait(true);
            WriteStartupLog("relaunch " + destExe);
            Process.Start(new ProcessStartInfo(destExe) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            WriteStartupLog("copy-failed " + ex.Message);
            MessageBox.Show(
                UiText.Tr(
                    "Meisterfenster kann nicht von der Server-Freigabe starten.\n\nBitte auf diesem PC installieren (Setup) oder den Ordner lokal kopieren und dort starten.\nOder „Meisterfenster-Starten.bat“ auf der Freigabe doppelklicken.",
                    "Meisterfenster cannot start from the server share.\n\nInstall it on this PC (Setup) or copy the folder locally and start it there.\nOr double-click Meisterfenster-Starten.bat on the share.",
                    "Meisterfenster ne peut pas démarrer depuis le partage.\n\nInstallez-le sur ce PC ou copiez le dossier localement.",
                    "Meisterfenster non si avvia dalla condivisione.\n\nInstallalo su questo PC o copia la cartella in locale.")
                + "\n\n" + ex.Message,
                "Meisterfenster",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return true;
        }
    }

    private static Window CreateStartupStatusWindow(string message)
    {
        var foreground = Current.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
        var surface = Current.TryFindResource("SurfaceBrush") as Brush ?? Brushes.White;
        var background = Current.TryFindResource("BackgroundBrush") as Brush ?? Brushes.WhiteSmoke;
        var border = Current.TryFindResource("BorderBrush") as Brush ?? Brushes.LightGray;
        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 15,
            Foreground = foreground
        };

        return new Window
        {
            Title = "Meisterfenster",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = background,
            Tag = text,
            Content = new Border
            {
                Background = surface,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(24),
                Margin = new Thickness(16),
                Child = text
            }
        };
    }

    private static void SetSplashText(Window? splash, string message)
    {
        if (splash?.Tag is TextBlock text)
            text.Text = message;
    }

    private static void CloseWindow(ref Window? window)
    {
        if (window == null)
            return;
        try
        {
            window.Close();
        }
        catch
        {
            // Splash may already be closing.
        }

        window = null;
    }

    private static string FormatServerStartupError(Exception ex, DatabaseOptions dbOptions)
    {
        var server = dbOptions.DisplayName;
        if (ex is OperationCanceledException or TimeoutException)
        {
            return string.Format(
                UiText.Tr(
                    "Der SQL Server „{0}“ antwortet nicht (Zeitüberschreitung nach {1} Sekunden).\n\nTypisch am anderen PC:\n• Firewall auf dem Server (TCP 1433 / UDP 1434)\n• SQL Server-Browser läuft nicht\n• Falscher Servername (nicht localhost / .\\SQLEXPRESS)\n• Windows-Anmeldung: dieser Benutzer hat kein SQL-Recht\n• Ohne Domäne: SQL-Benutzer statt Windows-Anmeldung verwenden",
                    "SQL Server \"{0}\" did not respond (timed out after {1} seconds).\n\nTypical on another PC:\n• Firewall on the server (TCP 1433 / UDP 1434)\n• SQL Server Browser is not running\n• Wrong server name (not localhost / .\\SQLEXPRESS)\n• Windows login: this user has no SQL permission\n• Without a domain: use a SQL login instead of Windows authentication",
                    "Le serveur SQL « {0} » ne répond pas (délai dépassé après {1} secondes).",
                    "SQL Server \"{0}\" non risponde (timeout dopo {1} secondi)."),
                server,
                SqlServerConnection.StartupTimeoutSeconds);
        }

        if (string.Equals(ex.Message, "password-reentry", StringComparison.Ordinal))
        {
            return UiText.Tr(
                "Das SQL-Passwort ist auf diesem PC nicht lesbar. Es wird pro Windows-Benutzer gespeichert und darf nicht mitkopiert werden. Bitte in den Einstellungen neu eingeben.",
                "The SQL password cannot be read on this PC. It is stored per Windows user and must not be copied. Please enter it again in the settings.",
                "Le mot de passe SQL n'est pas lisible sur ce PC. Il est enregistré par utilisateur Windows. Veuillez le saisir à nouveau.",
                "La password SQL non è leggibile su questo PC. È salvata per utente Windows. Inseriscila di nuovo nelle impostazioni.");
        }

        var detail = ex.InnerException?.Message ?? ex.Message;
        if (ServerStartupError.IsSqlPermissionError(detail))
        {
            var login = ServerStartupError.UsesWindowsAuthentication(dbOptions.ConnectionString)
                ? string.Format(
                    UiText.Tr(
                        "Die App ist mit Windows-Anmeldung verbunden als: {0}",
                        "The app is using Windows authentication as: {0}",
                        "L'application utilise l'authentification Windows : {0}",
                        "L'app usa l'autenticazione Windows: {0}"),
                    ServerStartupError.DescribeWindowsUser())
                : UiText.Tr(
                    "Die App verwendet einen SQL-Benutzer (nicht das Windows-Konto).",
                    "The app is using a SQL login (not the Windows account).",
                    "L'application utilise un compte SQL (pas le compte Windows).",
                    "L'app usa un utente SQL (non l'account Windows).");
            return string.Format(
                UiText.Tr(
                    "Der SQL Server „{0}“ ist erreichbar, aber dieser Anmeldename darf die Datenbank „Wochenplanung“ nicht verwenden.\n\n{1}\n\n{2}\n\nIn SSMS muss genau dieser Anmeldename auf die Datenbank Wochenplanung (Rolle db_owner) berechtigt sein — nicht nur ein anderes Konto derselben Person.",
                    "SQL Server \"{0}\" is reachable, but this login may not use the Wochenplanung database.\n\n{1}\n\n{2}\n\nIn SSMS this exact login needs db_owner on Wochenplanung — not a different account of the same person.",
                    "Le serveur SQL « {0} » est joignable, mais cet identifiant n'a pas le droit d'utiliser la base Wochenplanung.\n\n{1}\n\n{2}",
                    "SQL Server \"{0}\" è raggiungibile, ma questo accesso non può usare il database Wochenplanung.\n\n{1}\n\n{2}"),
                server,
                login,
                detail);
        }

        return string.Format(
            UiText.Tr(
                "Keine Verbindung zum SQL Server „{0}“.\n\n{1}\n\nAuf dem anderen PC denselben Servernamen eintragen und die Verbindung testen.",
                "Could not connect to SQL Server \"{0}\".\n\n{1}\n\nOn the other PC enter the same server name and test the connection.",
                "Impossible de se connecter au serveur SQL « {0} ».\n\n{1}",
                "Impossibile connettersi a SQL Server \"{0}\".\n\n{1}"),
            server,
            detail);
    }

    private static bool OfferDatabaseSettings(string message)
    {
        var openSettings = MessageBox.Show(
            message + "\n\n" + UiText.Tr(
                "Datenbank-Einstellungen jetzt öffnen?",
                "Open database settings now?",
                "Ouvrir les paramètres de la base maintenant ?",
                "Aprire ora le impostazioni del database?"),
            UiText.Tr("Datenbank / Server", "Database / Server", "Base de données / Serveur", "Database / Server"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (openSettings != MessageBoxResult.Yes)
            return false;

        var dialog = new DatabaseSettingsDialog
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        return dialog.ShowDialog() == true;
    }

    private void ConfigureServices(IServiceCollection services)
    {
            var dbOptions = ForceLocalDemo
                ? DatabaseSettingsStore.ToLocalDemoOptions()
                : DatabaseSettingsStore.ToOptions(DatabaseSettingsStore.Load());
            var logPath = Path.Combine(AppPaths.Root, "logs", "app-.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var structuredLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithProperty("Application", "Meisterfenster")
                .WriteTo.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
                .CreateLogger();

            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(structuredLogger, dispose: true);
            });

            services.AddInfrastructure(dbOptions);
            services.AddScoped<ExportService>();
            services.AddScoped<TaskTemplateStorage>();
            services.AddSingleton<UpdateCheckService>();
            services.AddTransient<DemoDataService>();
            services.AddTransient<ScheduleBackupService>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<ScheduleView>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _appScope?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
