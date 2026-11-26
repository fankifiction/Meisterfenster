using System.Windows;
using System.Windows.Controls;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.Services;
using Wochenplanung.Presentation.ViewModels;
using MessageBox = Wochenplanung.Presentation.Services.ModernMessageBox;

namespace Wochenplanung.Presentation.Views;

public partial class DatabaseSettingsDialog : Window
{
    public DatabaseSettingsViewModel ViewModel => (DatabaseSettingsViewModel)DataContext!;

    public DatabaseSettingsDialog()
    {
        InitializeComponent();
        DataContext = new DatabaseSettingsViewModel();
        PasswordBox.PasswordChanged += (_, _) => ViewModel.Password = PasswordBox.Password;
        Loaded += (_, _) =>
        {
            if (PasswordBox != null)
                PasswordBox.Password = ViewModel.Password;
        };
    }

    private void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.UseWindowsAuthentication)
            ViewModel.Password = PasswordBox.Password;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsServerMode && string.IsNullOrWhiteSpace(ViewModel.Server))
        {
            MessageBox.Show(
                UiText.Tr("Bitte den Servernamen angeben (z. B. BUEROSERVER\\SQLEXPRESS).", "Please enter the server name (e.g. OFFICE-PC\\SQLEXPRESS).", "Veuillez indiquer le nom du serveur.", "Inserisci il nome del server."),
                UiText.CaptionHint,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!ViewModel.UseWindowsAuthentication)
            ViewModel.Password = PasswordBox.Password;

        var previous = DatabaseSettingsStore.Load();
        var previousWasLocal = !string.Equals(previous.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase)
                               || string.IsNullOrWhiteSpace(previous.Server);

        DatabaseSettingsStore.Save(ViewModel.ToSettings());

        if (ViewModel.IsServerMode && ViewModel.CopyLocalData)
        {
            var dest = DatabaseSettingsStore.ToOptions(ViewModel.ToSettings());
            var result = await LocalDataImportPrompt.ImportIfNeededAsync(dest, null, promptUser: false);
            if (result.Copied
                || result.Status == Wochenplanung.Infrastructure.Data.LocalDataMigrationStatus.Failed
                || (previousWasLocal && result.Status == Wochenplanung.Infrastructure.Data.LocalDataMigrationStatus.DestinationNotEmpty))
            {
                LocalDataImportPrompt.ShowResult(result);
            }
        }

        DialogResult = true;
        Close();
    }
}
