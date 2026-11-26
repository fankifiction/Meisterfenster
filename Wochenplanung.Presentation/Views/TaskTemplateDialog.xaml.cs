using System.Windows;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation.Views;

public partial class TaskTemplateDialog : Window
{
    public TaskTemplateDialogViewModel ViewModel => (TaskTemplateDialogViewModel)DataContext!;

    public TaskTemplateDialog()
    {
        InitializeComponent();
        TaskTypeCombo.ItemsSource = Enum.GetValues(typeof(TaskType));
        TaskColorCombo.ItemsSource = Enum.GetValues(typeof(TaskColor));
    }

    public void SetViewModel(TaskTemplateDialogViewModel viewModel)
    {
        DataContext = viewModel;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SaveCommand.CanExecute(null))
        {
            DialogResult = true;
            Close();
        }
    }
}
