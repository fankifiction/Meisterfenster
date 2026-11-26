using System.Windows;
using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation.Views;

public partial class StatisticsDialog : Window
{
    public StatisticsViewModel ViewModel => (StatisticsViewModel)DataContext!;

    public Func<DateTime, DateTime, Task<List<EmployeeStatRow>>>? ComputeStatsAsync { get; set; }

    public StatisticsDialog()
    {
        InitializeComponent();
    }

    public void SetViewModel(StatisticsViewModel viewModel)
    {
        DataContext = viewModel;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (ComputeStatsAsync == null || DataContext is not StatisticsViewModel vm) return;
        var rows = await ComputeStatsAsync(vm.StartDate, vm.EndDate);
        vm.Rows.Clear();
        foreach (var r in rows)
            vm.Rows.Add(r);
    }
}
