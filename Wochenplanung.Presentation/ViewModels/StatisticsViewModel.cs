using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Wochenplanung.Presentation.ViewModels;

public partial class StatisticsViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime _startDate = DateTime.Now;

    [ObservableProperty]
    private DateTime _endDate = DateTime.Now;

    public ObservableCollection<EmployeeStatRow> Rows { get; } = new ObservableCollection<EmployeeStatRow>();
}

public class EmployeeStatRow
{
    public string EmployeeName { get; set; } = "";
    public int TaskDays { get; set; }
    public double TaskHours { get; set; }
}
