using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wochenplanung.Application.Services;
using Wochenplanung.Presentation.Localization;

namespace Wochenplanung.Presentation.ViewModels;

public partial class HolidayChoice : ObservableObject
{
    public HolidayChoice(SwissHoliday holiday, bool isSelected)
    {
        Date = holiday.Date;
        NameDe = holiday.NameDe;
        NameEn = holiday.NameEn;
        NameFr = holiday.NameFr;
        NameIt = holiday.NameIt;
        IsFederal = holiday.IsFederal;
        IsSolothurn = holiday.IsSolothurn;
        IsSelected = isSelected;
    }

    public DateTime Date { get; }
    public string NameDe { get; }
    public string NameEn { get; }
    public string NameFr { get; }
    public string NameIt { get; }
    public bool IsFederal { get; }
    public bool IsSolothurn { get; }

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayName
    {
        get
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var name = lang switch
            {
                "en" => NameEn,
                "fr" => NameFr,
                "it" => NameIt,
                _ => NameDe
            };
            return IsFederal || !IsSolothurn
                ? $"{Date:dd.MM.yyyy}  {name}"
                : $"{Date:dd.MM.yyyy}  {name}  (SO)";
        }
    }
}

public partial class HolidayDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today;

    [ObservableProperty]
    private bool _isRange;

    [ObservableProperty]
    private string _holidayName = string.Empty;

    [ObservableProperty]
    private bool _showSwissHolidays = true;

    [ObservableProperty]
    private bool _isCompanyVacation;

    [ObservableProperty]
    private int _swissYear = DateTime.Today.Year;

    public ObservableCollection<HolidayChoice> SwissHolidays { get; } = new();

    public IReadOnlyList<int> SwissYearOptions { get; } =
        Enumerable.Range(DateTime.Today.Year - 1, 12).ToList();

    public HolidayDialogViewModel()
    {
        ReloadSwissHolidays();
    }

    public static HolidayDialogViewModel ForCompanyVacation()
    {
        return new HolidayDialogViewModel
        {
            ShowSwissHolidays = false,
            IsCompanyVacation = true,
            IsRange = true,
            HolidayName = UiText.DefaultCompanyVacationName
        };
    }

    partial void OnIsRangeChanged(bool value)
    {
        if (!value)
            EndDate = StartDate;
    }

    partial void OnStartDateChanged(DateTime value)
    {
        if (!IsRange)
            EndDate = value;
        else if (EndDate < value)
            EndDate = value;
    }

    partial void OnSwissYearChanged(int value) => ReloadSwissHolidays();

    [RelayCommand]
    private void ReloadSwissHolidays()
    {
        SwissHolidays.Clear();
        foreach (var holiday in SwissPublicHolidays.ForYear(SwissYear))
            SwissHolidays.Add(new HolidayChoice(holiday, isSelected: false));
    }

    [RelayCommand]
    private void SelectAllSwiss()
    {
        var allSelected = SwissHolidays.Count > 0 && SwissHolidays.All(h => h.IsSelected);
        foreach (var item in SwissHolidays)
            item.IsSelected = !allSelected;
    }

    [RelayCommand]
    private void SelectFederalOnly()
    {
        var federals = SwissHolidays.Where(h => h.IsFederal).ToList();
        var allFederalSelected = federals.Count > 0 && federals.All(h => h.IsSelected);
        if (allFederalSelected)
        {
            foreach (var item in federals)
                item.IsSelected = false;
            return;
        }

        foreach (var item in SwissHolidays)
            item.IsSelected = item.IsFederal;
    }

    [RelayCommand]
    private void SelectSolothurn()
    {
        var soDays = SwissHolidays.Where(h => h.IsSolothurn).ToList();
        var allSelected = soDays.Count > 0 && soDays.All(h => h.IsSelected);
        if (allSelected)
        {
            foreach (var item in soDays)
                item.IsSelected = false;
            return;
        }

        foreach (var item in SwissHolidays)
            item.IsSelected = item.IsSolothurn;
    }

    public (DateTime Start, DateTime End) GetRange()
    {
        if (EndDate < StartDate)
            return (EndDate, StartDate);
        return (StartDate, EndDate);
    }

    public IReadOnlyList<HolidayDay> GetHolidayDays()
    {
        if (IsCompanyVacation)
            return HolidayPlanner.FromRange(StartDate, EndDate, HolidayName);

        var selected = SwissHolidays.Where(h => h.IsSelected).ToList();
        if (selected.Count > 0)
        {
            return selected
                .Select(h => new HolidayDay(h.Date, LocalizedName(h)))
                .ToList();
        }

        return HolidayPlanner.FromRange(StartDate, EndDate, HolidayName);
    }

    private static string LocalizedName(HolidayChoice holiday)
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return lang switch
        {
            "en" => holiday.NameEn,
            "fr" => holiday.NameFr,
            "it" => holiday.NameIt,
            _ => holiday.NameDe
        };
    }
}
