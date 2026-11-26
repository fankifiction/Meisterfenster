using System.Windows;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.ViewModels;
using MessageBox = Wochenplanung.Presentation.Services.ModernMessageBox;

namespace Wochenplanung.Presentation.Views;

public partial class ExportRangeDialog : Window
{
    public ExportRangeViewModel ViewModel => (ExportRangeViewModel)DataContext!;

    public ExportRangeDialog()
    {
        InitializeComponent();
    }

    public void SetViewModel(ExportRangeViewModel viewModel)
    {
        DataContext = viewModel;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.EndDate < ViewModel.StartDate)
        {
            MessageBox.Show(UiText.MsgEndBeforeStart, UiText.CaptionInvalidRange, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
        Close();
    }
}
