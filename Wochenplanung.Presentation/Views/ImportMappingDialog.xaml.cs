using System.Windows;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.ViewModels;
using MessageBox = Wochenplanung.Presentation.Services.ModernMessageBox;

namespace Wochenplanung.Presentation.Views;

public partial class ImportMappingDialog : Window
{
    public ImportMappingViewModel ViewModel => (ImportMappingViewModel)DataContext!;

    public ImportMappingDialog()
    {
        InitializeComponent();
    }

    public void SetViewModel(ImportMappingViewModel viewModel)
    {
        DataContext = viewModel;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsValid())
        {
            MessageBox.Show(UiText.MsgMapAllRequiredColumns, UiText.CaptionImport, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
