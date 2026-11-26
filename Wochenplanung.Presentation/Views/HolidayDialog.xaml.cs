using System.Windows;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.ViewModels;
using MessageBox = Wochenplanung.Presentation.Services.ModernMessageBox;

namespace Wochenplanung.Presentation.Views;

public partial class HolidayDialog : Window
{
    public HolidayDialogViewModel ViewModel => (HolidayDialogViewModel)DataContext!;

    public HolidayDialog()
    {
        InitializeComponent();
    }

    public void SetViewModel(HolidayDialogViewModel viewModel)
    {
        DataContext = viewModel;
        if (viewModel.IsCompanyVacation)
        {
            Title = UiText.CompanyVacationTitle;
            IntroText.Text = UiText.CompanyVacationIntro;
            NameLabel.Text = UiText.LabelCompanyVacationName;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.GetHolidayDays().Count == 0)
        {
            MessageBox.Show(UiText.MsgEnterHolidayName, UiText.CaptionHint, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var (start, end) = ViewModel.GetRange();
        if (end < start)
        {
            MessageBox.Show(UiText.MsgEndBeforeStart, UiText.CaptionInvalidRange, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
