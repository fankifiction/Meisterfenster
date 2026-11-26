using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Wochenplanung.Presentation.Localization;

namespace Wochenplanung.Presentation.Views;

public partial class ModernDatePicker : UserControl
{
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate),
        typeof(DateTime?),
        typeof(ModernDatePicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateChanged));

    public static readonly DependencyProperty DisplayCultureProperty = DependencyProperty.Register(
        nameof(DisplayCulture),
        typeof(string),
        typeof(ModernDatePicker),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TodayLabelProperty = DependencyProperty.Register(
        nameof(TodayLabel),
        typeof(string),
        typeof(ModernDatePicker),
        new PropertyMetadata(UiText.ActionToday, OnTodayLabelChanged));

    public static readonly DependencyProperty DisplayFormatProperty = DependencyProperty.Register(
        nameof(DisplayFormat),
        typeof(string),
        typeof(ModernDatePicker),
        new PropertyMetadata("ddd, d. MMM yyyy"));

    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact),
        typeof(bool),
        typeof(ModernDatePicker),
        new PropertyMetadata(false));

    private DateTime _popupClosedAtUtc = DateTime.MinValue;
    private bool _syncingCalendar;

    public ModernDatePicker()
    {
        InitializeComponent();
        TodayButton.Content = TodayLabel;
        RefreshDisplay();
        Loaded += (_, _) =>
        {
            RefreshDisplay();
            SyncCalendarFromSelectedDate();
        };
    }

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value?.Date);
    }

    public string DisplayCulture
    {
        get => (string)GetValue(DisplayCultureProperty);
        set => SetValue(DisplayCultureProperty, value);
    }

    public string TodayLabel
    {
        get => (string)GetValue(TodayLabelProperty);
        set => SetValue(TodayLabelProperty, value);
    }

    public string DisplayFormat
    {
        get => (string)GetValue(DisplayFormatProperty);
        set => SetValue(DisplayFormatProperty, value);
    }

    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModernDatePicker picker)
            picker.SyncCalendarFromSelectedDate();
    }

    private static void OnTodayLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModernDatePicker picker && picker.TodayButton != null)
            picker.TodayButton.Content = e.NewValue as string ?? UiText.ActionToday;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == DisplayCultureProperty || e.Property == SelectedDateProperty || e.Property == DisplayFormatProperty)
            RefreshDisplay();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if ((DateTime.UtcNow - _popupClosedAtUtc).TotalMilliseconds < 250)
            return;

        SyncCalendarFromSelectedDate();
        CalendarPopup.IsOpen = true;
    }

    private void CalendarPopup_Closed(object? sender, EventArgs e)
    {
        _popupClosedAtUtc = DateTime.UtcNow;
    }

    private void InnerCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingCalendar || InnerCalendar.SelectedDate is not { } date)
            return;

        SelectedDate = date.Date;
        CalendarPopup.IsOpen = false;
    }

    private void TodayButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedDate = DateTime.Today;
        CalendarPopup.IsOpen = false;
    }

    private void SyncCalendarFromSelectedDate()
    {
        if (InnerCalendar == null)
            return;

        _syncingCalendar = true;
        try
        {
            var date = SelectedDate?.Date ?? DateTime.Today;
            InnerCalendar.SelectedDate = SelectedDate?.Date;
            InnerCalendar.DisplayDate = date;
        }
        finally
        {
            _syncingCalendar = false;
        }

        RefreshDisplay();
        if (TodayButton != null)
            TodayButton.Content = string.IsNullOrWhiteSpace(TodayLabel) ? UiText.ActionToday : TodayLabel;
    }

    private void RefreshDisplay()
    {
        if (DisplayText == null)
            return;

        var culture = TryGetCulture();
        var format = string.IsNullOrWhiteSpace(DisplayFormat) ? "ddd, d. MMM yyyy" : DisplayFormat;
        DisplayText.Text = SelectedDate is { } date
            ? date.ToString(format, culture)
            : UiText.PickDate;
        if (TodayButton != null)
            TodayButton.Content = string.IsNullOrWhiteSpace(TodayLabel) ? UiText.ActionToday : TodayLabel;
    }

    private CultureInfo TryGetCulture()
    {
        if (!string.IsNullOrWhiteSpace(DisplayCulture))
        {
            try
            {
                return CultureInfo.GetCultureInfo(DisplayCulture);
            }
            catch (CultureNotFoundException)
            {
                // Fall through to current UI culture.
            }
        }

        return CultureInfo.CurrentUICulture;
    }
}
