using System.IO;
using System.Text.Json;

namespace Wochenplanung.Presentation.Services;

public sealed class UiLayoutState
{
    public double TemplatePanelWidth { get; set; } = UiLayoutSettings.DefaultTemplatePanelWidth;
    public bool? IsAutoReloadEnabled { get; set; }
    public bool? ShowFilterBar { get; set; }
    public bool? IsFilterPanelExpanded { get; set; }

    public bool? ShowPrevWeekButton { get; set; }
    public bool? ShowPrevDayButton { get; set; }
    public bool? ShowPrevMonthButton { get; set; }
    public bool? ShowTodayButton { get; set; }
    public bool? ShowCurrentWeekButton { get; set; }
    public bool? ShowNextWeekButton { get; set; }
    public bool? ShowCurrentDayButton { get; set; }
    public bool? ShowNextDayButton { get; set; }
    public bool? ShowCurrentMonthButton { get; set; }
    public bool? ShowNextMonthButton { get; set; }
    public bool? ShowViewDayButton { get; set; }
    public bool? ShowViewWeekButton { get; set; }
    public bool? ShowViewMonthButton { get; set; }
    public bool? ShowExcelButton { get; set; }
    public bool? ShowPdfButton { get; set; }
    public bool? ShowAddEmployeeButton { get; set; }
    public bool? ShowEditEmployeeButton { get; set; }
    public bool? ShowDeleteEmployeeButton { get; set; }
    public bool? ShowAddTaskButton { get; set; }
    public bool? ShowEditTaskButton { get; set; }
    public bool? ShowDeleteTaskButton { get; set; }
    public bool? ShowUndoButton { get; set; }
    public bool? ShowRedoButton { get; set; }
    public bool? ShowHolidayButton { get; set; }
    public bool? ShowCompanyVacationButton { get; set; }
    public bool? ShowImportButton { get; set; }
    public bool? ShowPrintButton { get; set; }
    public bool? ShowBackupButton { get; set; }
    public bool? ShowCopyWeekButton { get; set; }
    public bool? ShowStatisticsButton { get; set; }
    public bool? ShowAbsenceButton { get; set; }
    public bool? ShowRestoreButton { get; set; }
    public bool? ShowWeekend { get; set; }
    public bool? ShowInactiveEmployees { get; set; }
}

public static class UiLayoutSettings
{
    public const double DefaultTemplatePanelWidth = 200;
    public const double MinTemplatePanelWidth = 140;
    public const double MaxTemplatePanelWidth = 720;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static double ClampTemplatePanelWidth(double width) =>
        Math.Clamp(width, MinTemplatePanelWidth, MaxTemplatePanelWidth);

    public static double LoadTemplatePanelWidth() =>
        ClampTemplatePanelWidth(Load().TemplatePanelWidth);

    public static void SaveTemplatePanelWidth(double width)
    {
        var state = Load();
        state.TemplatePanelWidth = ClampTemplatePanelWidth(width);
        Save(state);
    }

    public static UiLayoutState Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new UiLayoutState();

            var state = JsonSerializer.Deserialize<UiLayoutState>(File.ReadAllText(FilePath), JsonOptions);
            return state ?? new UiLayoutState();
        }
        catch
        {
            return new UiLayoutState();
        }
    }

    public static void Save(UiLayoutState state)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch
        {
            // Layout merken ist optional.
        }
    }

    private static string FilePath
    {
        get
        {
            return AppPaths.Combine("ui_layout.json");
        }
    }
}
