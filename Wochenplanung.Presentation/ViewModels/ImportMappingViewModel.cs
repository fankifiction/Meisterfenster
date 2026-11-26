using CommunityToolkit.Mvvm.ComponentModel;
using Wochenplanung.Presentation.Localization;

namespace Wochenplanung.Presentation.ViewModels;

public partial class ImportMappingViewModel : ObservableObject
{
    [ObservableProperty]
    private List<string> _availableColumns = new();

    [ObservableProperty]
    private string? _employeeColumn;

    [ObservableProperty]
    private string? _dateColumn;

    [ObservableProperty]
    private string? _descriptionColumn;

    [ObservableProperty]
    private int _rowCount;

    public string RowCountText => string.Format(UiText.ImportMappingRowsDetectedFormat, RowCount);

    public ImportMappingViewModel(IEnumerable<string> columns, string? employeeDefault, string? dateDefault, string? descriptionDefault, int rowCount)
    {
        AvailableColumns = columns.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        EmployeeColumn = employeeDefault ?? AvailableColumns.FirstOrDefault();
        DateColumn = dateDefault ?? AvailableColumns.FirstOrDefault(c => !string.Equals(c, EmployeeColumn, StringComparison.OrdinalIgnoreCase));
        DescriptionColumn = descriptionDefault ?? AvailableColumns.FirstOrDefault(c =>
            !string.Equals(c, EmployeeColumn, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(c, DateColumn, StringComparison.OrdinalIgnoreCase));
        RowCount = rowCount;
    }

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(EmployeeColumn) &&
        !string.IsNullOrWhiteSpace(DateColumn) &&
        !string.IsNullOrWhiteSpace(DescriptionColumn);

    partial void OnRowCountChanged(int value)
    {
        OnPropertyChanged(nameof(RowCountText));
    }
}
