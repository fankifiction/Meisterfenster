using System.Windows;
using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation.Views;

public partial class AbsenceReportDialog : Window
{
    public AbsenceReportViewModel ViewModel => (AbsenceReportViewModel)DataContext!;

    public Func<AbsenceReportViewModel, Task<List<AbsenceReportRow>>>? ComputeReportAsync { get; set; }

    public AbsenceReportDialog()
    {
        InitializeComponent();
    }

    public void SetViewModel(AbsenceReportViewModel viewModel)
    {
        DataContext = viewModel;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    public async Task RefreshAsync()
    {
        if (ComputeReportAsync == null || DataContext is not AbsenceReportViewModel vm)
            return;
        if (!vm.UseCustomRange)
            vm.ApplyYearToRange();
        var rows = await ComputeReportAsync(vm);
        vm.Rows.Clear();
        foreach (var r in rows)
            vm.Rows.Add(r);
        vm.NotifyTotalChanged();
    }
}
