using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Wochenplanung.Presentation.ViewModels;

public partial class ExportRangeViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime _startDate = DateTime.Now;

    [ObservableProperty]
    private DateTime _endDate = DateTime.Now;

    [ObservableProperty]
    private int _fontSize = 12;

    public ExportRangeViewModel(DateTime? defaultStart = null, DateTime? defaultEnd = null)
    {
        if (defaultStart.HasValue) StartDate = defaultStart.Value.Date;
        if (defaultEnd.HasValue) EndDate = defaultEnd.Value.Date;
        if (!defaultStart.HasValue && !defaultEnd.HasValue)
        {
            var weekStart = StartDate.DayOfWeek == DayOfWeek.Sunday
                ? StartDate.AddDays(-6)
                : StartDate.AddDays(-(int)StartDate.DayOfWeek + (int)DayOfWeek.Monday);
            StartDate = weekStart;
            EndDate = StartDate.AddDays(6);
        }
    }

    public (DateTime Start, DateTime End) GetRange()
    {
        if (EndDate < StartDate)
            return (EndDate, StartDate);
        return (StartDate, EndDate);
    }
}
