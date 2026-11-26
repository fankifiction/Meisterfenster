using System.Windows;
using System.Windows.Input;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.ViewModels;
using MessageBox = Wochenplanung.Presentation.Services.ModernMessageBox;

namespace Wochenplanung.Presentation.Views;

public partial class TaskDialog : Window
{
    public TaskDialogViewModel ViewModel => (TaskDialogViewModel)DataContext!;

    public TaskDialog()
    {
        InitializeComponent();
        TaskTypeCombo.ItemsSource = Enum.GetValues(typeof(TaskType));
        TaskColorCombo.ItemsSource = Enum.GetValues(typeof(TaskColor));
        RecurrenceCombo.ItemsSource = Enum.GetValues(typeof(RecurrencePattern));
        SeriesScopeCombo.ItemsSource = Enum.GetValues(typeof(SeriesEditScope));
        Loaded += (_, _) => CommandManager.InvalidateRequerySuggested();
    }

    public void SetViewModel(TaskDialogViewModel viewModel)
    {
        DataContext = viewModel;
        CommandManager.InvalidateRequerySuggested();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEmployee == null)
        {
            MessageBox.Show(UiText.MsgSelectEmployee, UiText.CaptionHint, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(ViewModel.Description))
        {
            MessageBox.Show(UiText.MsgEnterDescription, UiText.CaptionHint, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (ViewModel.RepeatCount < 1)
        {
            MessageBox.Show(UiText.MsgRepeatCountMinOne, UiText.CaptionHint, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
        Close();
    }
}
