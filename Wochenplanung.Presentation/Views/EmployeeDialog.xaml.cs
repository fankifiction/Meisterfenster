using System.Windows;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.ViewModels;
using MessageBox = Wochenplanung.Presentation.Services.ModernMessageBox;

namespace Wochenplanung.Presentation.Views;

public partial class EmployeeDialog : Window
{
    public EmployeeDialogViewModel ViewModel => (EmployeeDialogViewModel)DataContext!;

    public EmployeeDialog()
    {
        InitializeComponent();
    }

    public void SetViewModel(EmployeeDialogViewModel viewModel)
    {
        DataContext = viewModel;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.FirstName) || string.IsNullOrWhiteSpace(ViewModel.LastName))
        {
            MessageBox.Show(UiText.MsgFillFirstLastName, UiText.CaptionHint, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
        Close();
    }
}
