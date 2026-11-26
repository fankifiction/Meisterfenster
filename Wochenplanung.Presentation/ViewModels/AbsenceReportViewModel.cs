using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Presentation.Localization;

namespace Wochenplanung.Presentation.ViewModels;

public sealed class EmployeeOption
{
    public int? Id { get; init; }
    public string Name { get; init; } = "";
}

public partial class AbsenceReportViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime _startDate = new(DateTime.Today.Year, 1, 1);

    [ObservableProperty]
    private DateTime _endDate = new(DateTime.Today.Year, 12, 31);

    [ObservableProperty]
    private int _selectedYear = DateTime.Today.Year;

    [ObservableProperty]
    private bool _useCustomRange;

    [ObservableProperty]
    private EmployeeOption? _selectedEmployeeOption;

    public List<EmployeeOption> EmployeeOptions { get; set; } = new();

    public IReadOnlyList<int> YearOptions { get; } =
        Enumerable.Range(DateTime.Today.Year - 2, 13).ToList();

    public ObservableCollection<AbsenceReportRow> Rows { get; } = new();

    public string TotalText => string.Format(UiText.AbsenceTotalFormat, Rows.Count);

    public bool UseYearMode => !UseCustomRange;

    public int? SelectedEmployeeId => SelectedEmployeeOption?.Id;

    public void SetEmployees(IEnumerable<Employee> employees)
    {
        EmployeeOptions = new List<EmployeeOption>
        {
            new() { Id = null, Name = UiText.AllEmployees }
        };
        EmployeeOptions.AddRange(employees
            .OrderBy(e => e.DisplayOrder)
            .ThenBy(e => e.LastName)
            .Select(e => new EmployeeOption { Id = e.Id, Name = e.DisplayName }));
        SelectedEmployeeOption = EmployeeOptions[0];
        OnPropertyChanged(nameof(EmployeeOptions));
    }

    public void NotifyTotalChanged() => OnPropertyChanged(nameof(TotalText));

    partial void OnSelectedYearChanged(int value)
    {
        if (!UseCustomRange)
            ApplyYearToRange();
    }

    partial void OnUseCustomRangeChanged(bool value)
    {
        OnPropertyChanged(nameof(UseYearMode));
        if (!value)
            ApplyYearToRange();
    }

    public void ApplyYearToRange()
    {
        StartDate = new DateTime(SelectedYear, 1, 1);
        EndDate = new DateTime(SelectedYear, 12, 31);
    }
}

public class AbsenceReportRow
{
    public string EmployeeName { get; set; } = "";
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public string TypeDisplay { get; set; } = "";
}
