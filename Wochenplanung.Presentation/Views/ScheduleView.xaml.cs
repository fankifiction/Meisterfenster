using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Presentation.Converters;
using Wochenplanung.Presentation.Services;
using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation.Views;

public partial class ScheduleView : UserControl
{
    private const string TaskTemplateFormat = "TaskTemplate";
    private const string TaskTemplatesFormat = "TaskTemplates";
    private const string EmployeeReorderFormat = "EmployeeReorder";
    private const string TaskResizeFormat = "TaskResize";
    private const string TaskCopyFormat = "TaskCopy";
    private const string TaskMoveFormat = "TaskMove";
    private readonly TaskColorConverter _colorConverter = new();
    private readonly Dictionary<(int EmployeeId, DateTime Day), Border> _dayCellMap = new();
    private readonly Dictionary<DateTime, Border> _dayHeaderMap = new();
    private readonly Dictionary<int, Grid> _employeeRowMap = new();
    private readonly List<Border> _dayHeadersOrdered = new();
    private readonly Dictionary<int, List<Border>> _employeeDayCellsOrdered = new();
    private int _contentUpdateGeneration;
    private MainViewModel? _subscribedViewModel;
    private bool _scheduleRebuildPending;
    private bool _rangeDragActive;
    private bool _rangeDragMoved;
    private Employee? _rangeDragEmployee;
    private DateTime _rangeDragStart;
    private DateTime _rangeDragEnd;
    private readonly List<Border> _rangePreviewCells = new();
    private Point _taskDragStartPoint;
    private (WorkTask Task, DateTime AnchorDay, Employee Employee)? _pendingTaskDrag;
    private Point _templateDragStartPoint;
    private FrameworkElement? _pendingTemplateDragElement;
    private TaskTemplate? _pendingTemplateDragItem;
    private readonly List<double> _columnWidths = new();
    private const double TaskSlotMinHeight = ScheduleGridLayout.TaskSlotMinHeight;
    private const double EmployeeRowGap = ScheduleGridLayout.EmployeeRowGap;
    private int _lastDayCount;
    private Window? _hostWindow;
    private bool _layoutBusy;
    private readonly DispatcherTimer _resizeLayoutTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private const double SplitterColumnWidth = ScheduleGridLayout.SplitterColumnWidth;

    private static int DayContentColumn(int dayIndex) => ScheduleGridLayout.DayContentColumn(dayIndex);
    private static int TrailingSplitterColumn(int dayCount) => ScheduleGridLayout.TrailingSplitterColumn(dayCount);
    private static int TrailingFillerColumn(int dayCount) => ScheduleGridLayout.TrailingFillerColumn(dayCount);
    private static int TotalColumnCount(int dayCount) => ScheduleGridLayout.TotalColumnCount(dayCount);

    public ScheduleView()
    {
        InitializeComponent();
        this.Loaded += ScheduleView_Loaded;
        this.Unloaded += ScheduleView_Unloaded;
        this.DataContextChanged += ScheduleView_DataContextChanged;
        _resizeLayoutTimer.Tick += (_, _) =>
        {
            _resizeLayoutTimer.Stop();
            ApplyScheduleSizeLayout();
        };
    }

    private void ScheduleView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.EmployeeScheduleChanged -= ViewModel_EmployeeScheduleChanged;
            _subscribedViewModel.TryPrintSchedule = null;
        }
        if (_subscribedViewModel is INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        _subscribedViewModel = e.NewValue as MainViewModel;
        if (_subscribedViewModel is INotifyPropertyChanged newVm)
            newVm.PropertyChanged += ViewModel_PropertyChanged;
        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.EmployeeScheduleChanged += ViewModel_EmployeeScheduleChanged;
            var printVm = _subscribedViewModel;
            printVm.TryPrintSchedule = () =>
                SchedulePrintHelper.TryPrint(HeaderGrid, ScheduleGrid, printVm.WeekHeaderText, Window.GetWindow(this));
        }
        ApplyTemplateGrouping();
        if (_subscribedViewModel != null && IsLoaded)
        {
            _subscribedViewModel.LoadDataCommand.Execute(null);
            ScheduleRebuild();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Lokale Task-Patches: Zeilen-Refresh via EmployeeScheduleChanged.
        // Vollständiger Daten-Reload (Wochenwechsel etc.): Full-Rebuild.
        if (e.PropertyName is nameof(MainViewModel.Tasks) or nameof(MainViewModel.FilteredTasks))
        {
            if (_subscribedViewModel?.IsScheduleDataReload == true)
            {
                if (_subscribedViewModel.PreferInPlaceScheduleUpdate && TryUpdateScheduleContentInPlace(_subscribedViewModel))
                    return;
                ScheduleRebuild();
            }
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.SelectedDate))
        {
            if (_subscribedViewModel?.CurrentViewMode == ScheduleViewMode.Day)
                ScheduleRebuild();
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.SelectedEmployee)
            or nameof(MainViewModel.SelectedTask)
            or nameof(MainViewModel.ScheduleHighlightMode)
            or nameof(MainViewModel.HighlightCellEmployeeId)
            or nameof(MainViewModel.HighlightCellDay)
            or nameof(MainViewModel.HighlightColumnDay)
            or nameof(MainViewModel.HighlightRowEmployeeId))
        {
            UpdateSelectionVisuals();
            return;
        }

        if (e.PropertyName is not (nameof(MainViewModel.CurrentViewMode)
                or nameof(MainViewModel.CurrentMonthStart) or nameof(MainViewModel.Employees)
                or nameof(MainViewModel.FilterText) or nameof(MainViewModel.FilterEmployee) or nameof(MainViewModel.FilterTaskType)
                or nameof(MainViewModel.IsDarkModeEnabled) or nameof(MainViewModel.ShowWeekend)))
            return;
        ScheduleRebuild();
    }

    private void ScheduleRebuild()
    {
        if (_scheduleRebuildPending || _subscribedViewModel == null)
            return;

        _scheduleRebuildPending = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _scheduleRebuildPending = false;
            if (_subscribedViewModel != null)
                BuildScheduleTable(_subscribedViewModel);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ViewModel_EmployeeScheduleChanged(object? sender, IReadOnlyList<int> employeeIds)
    {
        if (_subscribedViewModel == null || employeeIds.Count == 0)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_subscribedViewModel != null)
                RefreshEmployeeRows(_subscribedViewModel, employeeIds);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ScheduleView_Loaded(object sender, RoutedEventArgs e)
    {
        Keyboard.Focus(this);
        ApplySavedTemplatePanelWidth();
        ApplyTemplateGrouping();
        if (FindName("ScheduleScrollViewer") is ScrollViewer scroll)
        {
            scroll.SizeChanged -= ScheduleScrollViewer_SizeChanged;
            scroll.SizeChanged += ScheduleScrollViewer_SizeChanged;
            scroll.ScrollChanged -= ScheduleScrollViewer_ScrollChanged;
            scroll.ScrollChanged += ScheduleScrollViewer_ScrollChanged;
        }
        _hostWindow = Window.GetWindow(this);
        if (_hostWindow != null)
        {
            _hostWindow.SizeChanged -= HostWindow_SizeChanged;
            _hostWindow.SizeChanged += HostWindow_SizeChanged;
        }
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.LoadDataCommand.Execute(null);
            ScheduleRebuild();
        }
    }

    private void ScheduleView_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveTemplatePanelWidth();
        _resizeLayoutTimer.Stop();
        if (_hostWindow != null)
        {
            _hostWindow.SizeChanged -= HostWindow_SizeChanged;
            _hostWindow = null;
        }
    }

    private void ApplyTemplateGrouping()
    {
        var source = DefaultTasksList.ItemsSource ?? (DataContext as MainViewModel)?.DefaultTasks;
        if (source == null)
            return;

        var view = CollectionViewSource.GetDefaultView(source);
        if (view == null)
            return;

        using (view.DeferRefresh())
        {
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TaskTemplate.IsFavorite)));
        }
    }

    private void ApplySavedTemplatePanelWidth()
    {
        var width = UiLayoutSettings.LoadTemplatePanelWidth();
        LeftPanelColumn.MinWidth = UiLayoutSettings.MinTemplatePanelWidth;
        LeftPanelColumn.MaxWidth = UiLayoutSettings.MaxTemplatePanelWidth;
        LeftPanelColumn.Width = new GridLength(width);
    }

    private void SaveTemplatePanelWidth()
    {
        var width = LeftPanelColumn.ActualWidth > 0 ? LeftPanelColumn.ActualWidth : LeftPanelColumn.Width.Value;
        if (width > 0)
            UiLayoutSettings.SaveTemplatePanelWidth(width);
    }

    private void TemplatePanelSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        SaveTemplatePanelWidth();
        QueueScheduleSizeLayout();
    }

    private void HostWindow_SizeChanged(object sender, SizeChangedEventArgs e) => QueueScheduleSizeLayout();

    private void ScheduleScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) =>
        QueueScheduleSizeLayout();

    private void ScheduleScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (FindName("HeaderGrid") is not Grid header)
            return;
        header.RenderTransform = Math.Abs(e.HorizontalOffset) < 0.5
            ? Transform.Identity
            : new TranslateTransform(-e.HorizontalOffset, 0);
    }

    private void QueueScheduleSizeLayout()
    {
        FitScheduleGridToViewport();
        _resizeLayoutTimer.Stop();
        _resizeLayoutTimer.Start();
    }

    private void FitScheduleGridToViewport()
    {
        if (_layoutBusy)
            return;
        if (FindName("ScheduleScrollViewer") is not ScrollViewer scroll)
            return;
        if (FindName("ScheduleGrid") is not Grid grid)
            return;

        var viewport = scroll.ViewportWidth;
        if (viewport < 80)
            viewport = scroll.ActualWidth;
        if (viewport < 80)
            return;

        _layoutBusy = true;
        try
        {
            var headerGrid = FindName("HeaderGrid") as Grid;
            if (ScheduleColumnLayout.UseFixedDayWidths(_lastDayCount))
            {
                ApplyFixedDayViewport(grid, viewport);
                if (headerGrid != null)
                    ApplyFixedDayViewport(headerGrid, viewport);
                scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            else
            {
                ApplyFlexibleDayViewport(grid);
                if (headerGrid != null)
                    ApplyFlexibleDayViewport(headerGrid);
                scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
        }
        finally
        {
            _layoutBusy = false;
            AlignHeaderWithSchedule();
        }
    }

    private void AlignHeaderWithSchedule()
    {
        if (FindName("HeaderGrid") is not Grid header)
            return;
        if (FindName("ScheduleScrollViewer") is not ScrollViewer scroll)
            return;

        header.HorizontalAlignment = HorizontalAlignment.Left;
        var width = 0d;
        if (FindName("ScheduleGrid") is Grid body && body.ActualWidth > 80)
            width = body.ActualWidth;
        if (width < 80)
            width = scroll.ViewportWidth;
        if (width < 80)
            width = scroll.ActualWidth;
        if (width < 80)
            return;

        header.Width = width;
        header.MinWidth = width;
        header.ClearValue(MaxWidthProperty);
    }

    private static void ApplyFixedDayViewport(Grid grid, double viewport)
    {
        grid.ClearValue(WidthProperty);
        grid.MinWidth = viewport;
        grid.ClearValue(MaxWidthProperty);
    }

    private static void ApplyFlexibleDayViewport(Grid grid)
    {
        grid.ClearValue(WidthProperty);
        grid.ClearValue(MinWidthProperty);
        grid.ClearValue(MaxWidthProperty);
        grid.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private void ApplyScheduleSizeLayout()
    {
        FitScheduleGridToViewport();
        ApplyColumnWidthsToRows();
        AlignHeaderWithSchedule();
        ResetTaskChipMinHeights();
        _ = Dispatcher.BeginInvoke(() =>
        {
            foreach (var cell in _dayCellMap.Values)
                UpdateWrappedTextWidths(cell);
            AlignHeaderWithSchedule();
            _ = Dispatcher.BeginInvoke(() =>
            {
                foreach (var cells in _employeeDayCellsOrdered.Values)
                    SyncEmployeeSlotHeights(cells);
            }, DispatcherPriority.Loaded);
        }, DispatcherPriority.Loaded);
    }

    private void ResetTaskChipMinHeights()
    {
        foreach (var cell in _dayCellMap.Values)
        {
            if (cell.Child is not Panel panel)
                continue;
            foreach (var child in panel.Children)
            {
                if (child is FrameworkElement fe)
                    fe.MinHeight = TaskSlotMinHeight;
            }
        }
    }

    private void ApplyColumnWidthsToRows()
    {
        var headerGrid = FindName("HeaderGrid") as Grid;
        var scheduleGrid = FindName("ScheduleGrid") as Grid;
        if (headerGrid == null || scheduleGrid == null)
            return;
        if (headerGrid.ColumnDefinitions.Count == 0 || _columnWidths.Count == 0)
            return;

        for (var i = 0; i < headerGrid.ColumnDefinitions.Count && i < _columnWidths.Count; i++)
            headerGrid.ColumnDefinitions[i].Width = CreateColumnWidth(i);

        foreach (var child in scheduleGrid.Children)
        {
            if (child is not Grid row || row.ColumnDefinitions.Count != headerGrid.ColumnDefinitions.Count)
                continue;

            for (var i = 0; i < row.ColumnDefinitions.Count && i < _columnWidths.Count; i++)
                row.ColumnDefinitions[i].Width = CreateColumnWidth(i);
        }
    }

    /// <summary>Vormerken für Drag; Doppelklick öffnet das Aufgabenfenster.</summary>
    private void TemplateItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not TaskTemplate template)
            return;
        if (_subscribedViewModel == null)
            return;
        if (IsUnderFavoriteToggle(e.OriginalSource as DependencyObject))
            return;

        if (e.ClickCount >= 2)
        {
            ClearPendingTemplateDrag();
            _ = _subscribedViewModel.OpenNewTaskDialogAsync(
                _subscribedViewModel.SelectedEmployee,
                _subscribedViewModel.SelectedDate,
                template);
            e.Handled = true;
            return;
        }

        _templateDragStartPoint = e.GetPosition(null);
        _pendingTemplateDragElement = fe;
        _pendingTemplateDragItem = template;
    }

    private void TemplateItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _pendingTemplateDragElement == null
            || _pendingTemplateDragItem == null
            || _subscribedViewModel == null)
            return;

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _templateDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _templateDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var template = _pendingTemplateDragItem;
        var fe = _pendingTemplateDragElement;
        ClearPendingTemplateDrag();

        var templates = _subscribedViewModel.SelectedTaskTemplates.Count > 0 &&
                        _subscribedViewModel.SelectedTaskTemplates.Contains(template)
            ? _subscribedViewModel.SelectedTaskTemplates.ToList()
            : new List<TaskTemplate> { template };

        try
        {
            var data = new DataObject();
            data.SetData(TaskTemplatesFormat, templates);
            if (templates.Count == 1)
                data.SetData(TaskTemplateFormat, templates[0]);
            DragDrop.DoDragDrop(fe, data, DragDropEffects.Copy);
        }
        catch { /* DragDrop kann in manchen Fällen fehlschlagen */ }
    }

    private void TemplateItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ClearPendingTemplateDrag();
    }

    private void ClearPendingTemplateDrag()
    {
        _pendingTemplateDragElement = null;
        _pendingTemplateDragItem = null;
    }

    private void FavoriteToggleButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
    }

    private static bool IsUnderFavoriteToggle(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement { Name: "FavoriteToggleButton" })
                return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private static List<TaskTemplate> GetActiveTemplates(MainViewModel viewModel)
    {
        if (viewModel.SelectedTaskTemplates.Count > 0)
            return viewModel.SelectedTaskTemplates.ToList();
        if (viewModel.SelectedTaskTemplate != null)
            return new List<TaskTemplate> { viewModel.SelectedTaskTemplate };
        return new List<TaskTemplate>();
    }

    private void BuildScheduleTable(MainViewModel viewModel)
    {
        var scheduleGrid = FindName("ScheduleGrid") as Grid;
        var headerGrid = FindName("HeaderGrid") as Grid;
        
        if (scheduleGrid == null || headerGrid == null) return;

        var days = viewModel.GetDisplayDays();
        _lastDayCount = days.Count;
        CaptureColumnWidths(headerGrid, days.Count);
        
        // Lösche vorhandene Inhalte
        headerGrid.Children.Clear();
        headerGrid.ColumnDefinitions.Clear();
        scheduleGrid.Children.Clear();
        scheduleGrid.RowDefinitions.Clear();
        _dayCellMap.Clear();
        _dayHeaderMap.Clear();
        _employeeRowMap.Clear();
        _dayHeadersOrdered.Clear();
        _employeeDayCellsOrdered.Clear();
        _contentUpdateGeneration++;
        ClearRangePreview();

        EnsureColumnWidths(days.Count);
        FitScheduleGridToViewport();

        // Spalten: Grip | Mitarbeiter | Split | Tag0 | Split | Tag1 | ... | Split
        for (int i = 0; i < TotalColumnCount(days.Count); i++)
            headerGrid.ColumnDefinitions.Add(CreateColumnDefinition(i));
        
        // Theme-Farben aus Ressourcen (Light/Dark konsistent)
        var headerBg = (Brush)(TryFindResource("HeaderBrush") ?? Brushes.LightGray);
        var surfaceBg = (Brush)(TryFindResource("SurfaceBrush") ?? Brushes.White);
        var borderBrush = (Brush)(TryFindResource("BorderBrush") ?? Brushes.LightGray);
        var selectedEmployeeBg = (Brush)(TryFindResource("AccentLightBrush") ?? Brushes.LightBlue);
        var selectedDayBg = (Brush)(TryFindResource("AccentLightBrush") ?? Brushes.LightBlue);
        var selectedTaskBorder = (Brush)(TryFindResource("PrimaryBrush") ?? Brushes.SteelBlue);
        var textSecondary = (Brush)(TryFindResource("TextSecondaryBrush") ?? Brushes.Gray);
        
        var textPrimary = (Brush)(TryFindResource("TextPrimaryBrush") ?? Brushes.Black);

        // Spalte 0: Grip-Platzhalter (leer, damit Spalten mit Datenzeilen übereinstimmen)
        var gripHeader = new Border
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Background = headerBg
        };
        Grid.SetColumn(gripHeader, 0);
        headerGrid.Children.Add(gripHeader);
        
        // Spalte 1: Mitarbeiter (Klick sortiert Nr. ↔ Name)
        var mitarbeiterHeader = new Border
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(12),
            Background = headerBg,
            Cursor = Cursors.Hand,
            ToolTip = viewModel.TxtEmployeeSortTooltip
        };
        mitarbeiterHeader.Child = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = viewModel.TxtEmployeeSortLabel, FontWeight = FontWeights.SemiBold, Foreground = textPrimary },
                new TextBlock
                {
                    Text = viewModel.TxtEmployeeSortHint,
                    FontSize = 10,
                    Foreground = textSecondary,
                    Margin = new Thickness(0, 2, 0, 0)
                }
            }
        };
        mitarbeiterHeader.MouseLeftButtonUp += async (_, _) =>
        {
            if (viewModel.CycleEmployeeSortCommand.CanExecute(null))
                await viewModel.CycleEmployeeSortCommand.ExecuteAsync(null);
        };
        Grid.SetColumn(mitarbeiterHeader, 1);
        headerGrid.Children.Add(mitarbeiterHeader);
        
        // Spaltenüberschriften für die Tage (ab Spalte 2)
        for (int i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var shortName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day.DayOfWeek);
            var headerBorder = new Border
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(10, 10, 10, 10),
                Cursor = Cursors.Hand,
                Tag = day.Date
            };
            
            var holiday = viewModel.GetHolidayLabel(day.Date);
            var headerText = new TextBlock
            {
                Text = string.IsNullOrEmpty(holiday)
                    ? $"{shortName}\n{day:dd.MM.yyyy}"
                    : $"{shortName}\n{day:dd.MM.yyyy}\n{holiday}",
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                ToolTip = holiday
            };
            ApplyDayHeaderAppearance(headerBorder, headerText, day.Date, viewModel, selectedDayBg, headerBg, textPrimary);
            
            headerBorder.Child = headerText;
            headerBorder.SizeChanged += (_, _) =>
            {
                headerText.MaxWidth = Math.Max(20, headerBorder.ActualWidth - headerBorder.Padding.Left - headerBorder.Padding.Right);
            };
            headerBorder.MouseLeftButtonDown += (_, _) =>
            {
                if (headerBorder.Tag is DateTime headerDay)
                {
                    viewModel.SelectScheduleColumn(headerDay);
                    UpdateSelectionVisuals();
                }
            };
            Grid.SetColumn(headerBorder, DayContentColumn(i));
            headerGrid.Children.Add(headerBorder);
            _dayHeaderMap[day.Date] = headerBorder;
            _dayHeadersOrdered.Add(headerBorder);
        }

        for (int i = 2; i < TrailingSplitterColumn(days.Count); i += 2)
            AddHeaderColumnSplitter(headerGrid, i);
        
        // Erstelle Datenzeilen für jeden Mitarbeiter (mit Drag-Handle für Reihenfolge)
        var rowIndex = 0;
        if (viewModel.Employees.Count == 0)
        {
            var emptyBorder = new Border
            {
                Margin = new Thickness(12),
                Padding = new Thickness(16),
                Background = surfaceBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = viewModel.TxtNoEmployees, FontWeight = FontWeights.SemiBold, Foreground = textPrimary, FontSize = 15 },
                        new TextBlock { Text = viewModel.TxtNoEmployeesCta, Foreground = (Brush)(TryFindResource("TextSecondaryBrush") ?? Brushes.Gray), Margin = new Thickness(0,4,0,0) }
                    }
                }
            };
            Grid.SetColumnSpan(emptyBorder, TotalColumnCount(days.Count));
            Grid.SetRow(emptyBorder, 0);
            scheduleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            scheduleGrid.Children.Add(emptyBorder);
            QueueScheduleSizeLayout();
            return;
        }

        var filteredTasks = viewModel.FilteredTasks;
        var tasksByEmployee = filteredTasks.ToLookup(t => t.EmployeeId);

        foreach (var employee in viewModel.Employees)
        {
            var row = CreateEmployeeScheduleRow(
                viewModel, employee, days, tasksByEmployee[employee.Id].ToList(),
                rowIndex, headerBg, surfaceBg, borderBrush, selectedEmployeeBg, selectedDayBg, selectedTaskBorder, textPrimary);
            
            Grid.SetRow(row, rowIndex);
            scheduleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (rowIndex > 0)
                row.Margin = new Thickness(0, EmployeeRowGap, 0, 0);
            scheduleGrid.Children.Add(row);
            _employeeRowMap[employee.Id] = row;
            rowIndex++;
        }

        QueueScheduleSizeLayout();
    }

    /// <summary>
    /// Schneller Wochen-/Monatswechsel: Header sofort, Aufgaben asynchron nachfüllen.
    /// </summary>
    private bool TryUpdateScheduleContentInPlace(MainViewModel viewModel)
    {
        var days = viewModel.GetDisplayDays();
        if (days.Count == 0 || days.Count != _lastDayCount || _dayHeadersOrdered.Count != days.Count)
            return false;
        if (_employeeRowMap.Count == 0 || _employeeRowMap.Count != viewModel.Employees.Count)
            return false;
        if (viewModel.Employees.Any(e => !_employeeDayCellsOrdered.ContainsKey(e.Id)))
            return false;

        var surfaceBg = (Brush)(TryFindResource("SurfaceBrush") ?? Brushes.White);
        var selectedDayBg = (Brush)(TryFindResource("AccentLightBrush") ?? Brushes.LightBlue);
        var headerBg = (Brush)(TryFindResource("HeaderBrush") ?? Brushes.LightGray);
        var textPrimary = (Brush)(TryFindResource("TextPrimaryBrush") ?? Brushes.Black);

        var generation = ++_contentUpdateGeneration;
        _dayHeaderMap.Clear();
        _dayCellMap.Clear();
        ClearRangePreview();

        // 1) Header + leere Zellen sofort → Wochenwechsel wirkt instant
        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var headerBorder = _dayHeadersOrdered[i];
            headerBorder.Tag = day.Date;
            if (headerBorder.Child is TextBlock headerText)
            {
                var shortName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day.DayOfWeek);
                headerText.Text = $"{shortName}\n{day:dd.MM.yyyy}";
                ApplyDayHeaderAppearance(headerBorder, headerText, day.Date, viewModel, selectedDayBg, headerBg, textPrimary);
            }
            else
            {
                ApplyDayHeaderAppearance(headerBorder, null, day.Date, viewModel, selectedDayBg, headerBg, textPrimary);
            }
            _dayHeaderMap[day.Date] = headerBorder;
        }

        var employees = viewModel.Employees.ToList();
        for (var empIndex = 0; empIndex < employees.Count; empIndex++)
        {
            var employee = employees[empIndex];
            if (!_employeeDayCellsOrdered.TryGetValue(employee.Id, out var cells) || cells.Count != days.Count)
                return false;

            var isAlt = empIndex % 2 == 1;
            var bandBg = (Brush)(TryFindResource(isAlt ? "BandBrushB" : "BandBrushA") ?? surfaceBg);

            for (var dayIndex = 0; dayIndex < days.Count; dayIndex++)
            {
                var day = days[dayIndex];
                var dayBorder = cells[dayIndex];
                dayBorder.Tag = (employee, day);
                dayBorder.Background = ResolveDayColumnBackground(day, employee.Id, bandBg, selectedDayBg, viewModel);
                if (dayBorder.Child is StackPanel panel)
                    panel.Children.Clear();
                else
                    dayBorder.Child = new StackPanel { ClipToBounds = true };
                _dayCellMap[(employee.Id, day.Date)] = dayBorder;
            }
        }

        // Snapshot für Hintergrund-Layout (UI bleibt responsiv)
        var tasksByEmployee = viewModel.FilteredTasks
            .GroupBy(t => t.EmployeeId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<WorkTask>)g.ToList());
        var daysCopy = days.ToList();

        _ = FillScheduleContentAsync(viewModel, employees, daysCopy, tasksByEmployee, generation);
        return true;
    }

    private async Task FillScheduleContentAsync(
        MainViewModel viewModel,
        List<Employee> employees,
        List<DateTime> days,
        Dictionary<int, IReadOnlyList<WorkTask>> tasksByEmployee,
        int generation)
    {
        // Layout-Berechnung off UI-Thread
        Dictionary<int, Dictionary<DateTime, List<WorkTask?>>>? layouts = null;
        try
        {
            layouts = await Task.Run(() =>
            {
                var result = new Dictionary<int, Dictionary<DateTime, List<WorkTask?>>>(employees.Count);
                foreach (var employee in employees)
                {
                    if (generation != _contentUpdateGeneration)
                        return null;
                    tasksByEmployee.TryGetValue(employee.Id, out var empTasks);
                    empTasks ??= Array.Empty<WorkTask>();
                    result[employee.Id] = ScheduleTableLayout.BuildEmployeeWeekLayout(employee, empTasks, days);
                }
                return result;
            }).ConfigureAwait(true);
        }
        catch
        {
            if (generation == _contentUpdateGeneration)
                BuildScheduleTable(viewModel);
            return;
        }

        if (layouts == null || generation != _contentUpdateGeneration || _subscribedViewModel != viewModel)
            return;

        var selectedEmployeeBg = (Brush)(TryFindResource("AccentLightBrush") ?? Brushes.LightBlue);
        var selectedTaskBorder = (Brush)(TryFindResource("PrimaryBrush") ?? Brushes.SteelBlue);

        // Zeilenweise einfüllen, dazwischen UI atmen lassen
        for (var empIndex = 0; empIndex < employees.Count; empIndex++)
        {
            var employee = employees[empIndex];
            if (generation != _contentUpdateGeneration)
                return;
            if (!layouts.TryGetValue(employee.Id, out var layout))
                continue;
            if (!_employeeDayCellsOrdered.TryGetValue(employee.Id, out var cells))
                continue;

            var isAlt = empIndex % 2 == 1;
            var bandBg = (Brush)(TryFindResource(isAlt ? "BandBrushB" : "BandBrushA") ?? Brushes.White);

            for (var dayIndex = 0; dayIndex < days.Count; dayIndex++)
            {
                var day = days[dayIndex];
                var dayBorder = cells[dayIndex];
                if (!layout.TryGetValue(day.Date, out var orderedSlots))
                    orderedSlots = new List<WorkTask?>();
                FillDayCellTasks(dayBorder, viewModel, employee, day, days, dayIndex, orderedSlots, selectedEmployeeBg, selectedTaskBorder, bandBg);
            }

            var cellsForSync = cells;
            _ = Dispatcher.BeginInvoke(() =>
            {
                foreach (var cell in cellsForSync)
                    UpdateWrappedTextWidths(cell);
                SyncEmployeeSlotHeights(cellsForSync);
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void RefreshEmployeeRows(MainViewModel viewModel, IReadOnlyList<int> employeeIds)
    {
        var days = viewModel.GetDisplayDays();
        if (days.Count != _lastDayCount || _employeeRowMap.Count == 0)
        {
            BuildScheduleTable(viewModel);
            return;
        }

        FitScheduleGridToViewport();

        var filteredTasks = viewModel.FilteredTasks;
        var selectedEmployeeBg = (Brush)(TryFindResource("AccentLightBrush") ?? Brushes.LightBlue);
        var selectedTaskBorder = (Brush)(TryFindResource("PrimaryBrush") ?? Brushes.SteelBlue);

        foreach (var employeeId in employeeIds.Distinct())
        {
            var employee = viewModel.Employees.FirstOrDefault(e => e.Id == employeeId);
            if (employee == null ||
                !_employeeDayCellsOrdered.TryGetValue(employeeId, out var cells) ||
                cells.Count != days.Count)
            {
                BuildScheduleTable(viewModel);
                return;
            }

            var empIndex = 0;
            for (var i = 0; i < viewModel.Employees.Count; i++)
            {
                if (viewModel.Employees[i].Id == employeeId)
                {
                    empIndex = i;
                    break;
                }
            }

            var isAlt = empIndex % 2 == 1;
            var bandBg = (Brush)(TryFindResource(isAlt ? "BandBrushB" : "BandBrushA") ?? Brushes.White);
            var employeeTasks = filteredTasks.Where(t => t.EmployeeId == employeeId).ToList();
            var layout = ScheduleTableLayout.BuildEmployeeWeekLayout(employee, employeeTasks, days);

            for (var dayIndex = 0; dayIndex < days.Count; dayIndex++)
            {
                var day = days[dayIndex];
                var dayBorder = cells[dayIndex];
                if (!layout.TryGetValue(day.Date, out var orderedSlots))
                    orderedSlots = new List<WorkTask?>();
                FillDayCellTasks(dayBorder, viewModel, employee, day, days, dayIndex, orderedSlots, selectedEmployeeBg, selectedTaskBorder, bandBg);
            }

            var cellsForSync = cells;
            _ = Dispatcher.BeginInvoke(() =>
            {
                foreach (var cell in cellsForSync)
                    UpdateWrappedTextWidths(cell);
                SyncEmployeeSlotHeights(cellsForSync);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        QueueScheduleSizeLayout();
    }

    private Grid CreateEmployeeScheduleRow(
        MainViewModel viewModel,
        Employee employee,
        List<DateTime> days,
        List<WorkTask> filteredTasks,
        int rowIndex,
        Brush headerBg,
        Brush surfaceBg,
        Brush borderBrush,
        Brush selectedEmployeeBg,
        Brush selectedDayBg,
        Brush selectedTaskBorder,
        Brush textPrimary)
    {
            var isAlt = rowIndex % 2 == 1;
            var bandBg = (Brush)(TryFindResource(isAlt ? "BandBrushB" : "BandBrushA") ?? surfaceBg);
            var nameBg = (Brush)(TryFindResource(isAlt ? "NameBandBrushB" : "NameBandBrushA") ?? headerBg);
            var accentStripe = (Brush)(TryFindResource(isAlt ? "AccentStripeBrushB" : "AccentStripeBrushA") ?? selectedTaskBorder);
            var outlineBrush = (Brush)(TryFindResource("EmployeeOutlineBrush") ?? borderBrush);
            var softBorder = (Brush)(TryFindResource("BorderBrush") ?? borderBrush);

            var row = new Grid();
            for (int i = 0; i < TotalColumnCount(days.Count); i++)
                row.ColumnDefinitions.Add(CreateColumnDefinition(i));
            row.AllowDrop = true;
            row.Tag = employee;
            row.DragOver += EmployeeRow_DragOver;
            row.Drop += EmployeeRow_Drop;
            
            // Grip – Drag starten für Reihenfolge
            var gripBorder = new Border
            {
                BorderBrush = softBorder,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(4),
                Background = nameBg,
                Cursor = System.Windows.Input.Cursors.SizeAll,
                ToolTip = viewModel.TxtReorderEmployeeTooltip
            };
            gripBorder.Child = new Grid
            {
                Children =
                {
                    new Border
                    {
                        Width = 4,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Background = accentStripe
                    },
                    new TextBlock
                    {
                        Text = "≡",
                        FontSize = 14,
                        Foreground = textPrimary,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            };
            gripBorder.PreviewMouseLeftButtonDown += (s, ev) =>
            {
                if (s is not FrameworkElement fe)
                    return;
                var emp = (fe.Parent as Grid)?.Tag as Employee ?? fe.DataContext as Employee;
                if (emp == null)
                    return;
                ev.Handled = true;
                try
                {
                    DragDrop.DoDragDrop(fe, new DataObject(EmployeeReorderFormat, emp), DragDropEffects.Move);
                }
                catch { /* DragDrop kann fehlschlagen */ }
            };
            Grid.SetColumn(gripBorder, 0);
            row.Children.Add(gripBorder);
            gripBorder.DataContext = employee;
            
            // Mitarbeitername – gestapelt: Nr. / Vorname / Nachname (Mockup Sven)
            var isSelectedEmployee = viewModel.IsScheduleEmployeeNameHighlighted(employee.Id);
            var nameCardBg = (Brush)(TryFindResource("SurfaceBrush") ?? Brushes.White);
            var nameBorder = new Border
            {
                BorderBrush = softBorder,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8, 8, 8, 8),
                Background = isSelectedEmployee ? selectedEmployeeBg : nameBg,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            nameBorder.Child = CreateEmployeeNameCard(employee, textPrimary, nameCardBg, softBorder);
            nameBorder.MouseLeftButtonDown += (_, _) =>
            {
                viewModel.SelectScheduleRow(employee);
                UpdateSelectionVisuals();
            };
            Grid.SetColumn(nameBorder, 1);
            row.Children.Add(nameBorder);

            var employeeWeekLayout = ScheduleTableLayout.BuildEmployeeWeekLayout(
                employee, filteredTasks, days);
            var dayCellsOrdered = new List<Border>(days.Count);
            
            // Tage – Mehrfachtages-Aufgaben werden an jedem Tag einzeln angezeigt
            for (int dayIndex = 0; dayIndex < days.Count; dayIndex++)
            {
                var day = days[dayIndex];
                
                var dayBorder = new Border
                {
                    BorderBrush = softBorder,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(5),
                    Background = ResolveDayColumnBackground(day, employee.Id, bandBg, selectedDayBg, viewModel),
                    MinHeight = 60,
                    AllowDrop = true,
                    ClipToBounds = true,
                    Tag = (employee, day)
                };
                dayBorder.DragOver += DayCell_DragOver;
                dayBorder.DragLeave += DayCell_DragLeave;
                dayBorder.Drop += DayCell_Drop;
                dayBorder.PreviewMouseLeftButtonDown += DayCell_PreviewMouseLeftButtonDown;
                dayBorder.MouseEnter += DayCell_MouseEnter;
                dayBorder.PreviewMouseLeftButtonUp += DayCell_PreviewMouseLeftButtonUp;
                // Rechtsklick: Menü liest Tag (wichtig für In-Place-Wochenwechsel)
                var dayCtxMenu = CreateThemeContextMenu();
                dayCtxMenu.Opened += (_, _) =>
                {
                    dayCtxMenu.Items.Clear();
                    if (dayBorder.Tag is not ValueTuple<Employee, DateTime> cellTag)
                        return;
                    var menuEmployee = cellTag.Item1;
                    var menuDay = cellTag.Item2;
                    viewModel.SelectedEmployee = menuEmployee;
                    viewModel.SelectedDate = menuDay.Date;
                    foreach (var tpl in viewModel.DefaultTasks)
                    {
                        var tplCopy = tpl;
                        var tplColor = (Brush)_colorConverter.Convert(tpl.Color, typeof(Brush), null, System.Globalization.CultureInfo.CurrentCulture)!;
                        var item = CreateThemeMenuItem(tpl.Description, colorDot: tplColor);
                        item.Tag = tpl;
                        item.Click += async (_, _) =>
                        {
                            if (_subscribedViewModel != null)
                            {
                                await _subscribedViewModel.CreateTaskFromTemplateAsync(menuEmployee, menuDay, tplCopy);
                                _ = FlashCellAsync(dayBorder);
                            }
                        };
                        dayCtxMenu.Items.Add(item);
                    }
                    if (viewModel.HasCopiedTask)
                    {
                        dayCtxMenu.Items.Add(new Separator());
                        var pasteItem = CreateThemeMenuItem(viewModel.TxtPasteCopiedTaskHere, ContextMenuIcon.Paste);
                        pasteItem.Click += async (_, _) =>
                        {
                            if (_subscribedViewModel != null)
                            {
                                await _subscribedViewModel.PasteCopiedTaskAsync(menuEmployee, menuDay);
                                _ = FlashCellAsync(dayBorder);
                            }
                        };
                        dayCtxMenu.Items.Add(pasteItem);
                    }
                };
                dayBorder.ContextMenu = dayCtxMenu;
                _dayCellMap[(employee.Id, day.Date)] = dayBorder;
                dayCellsOrdered.Add(dayBorder);
                
                if (!employeeWeekLayout.TryGetValue(day.Date, out var orderedSlots))
                    orderedSlots = new List<WorkTask?>();

                FillDayCellTasks(dayBorder, viewModel, employee, day, days, dayIndex, orderedSlots, selectedEmployeeBg, selectedTaskBorder, bandBg);
                Grid.SetColumn(dayBorder, DayContentColumn(dayIndex));
                row.Children.Add(dayBorder);
            }

            SyncEmployeeSlotHeights(dayCellsOrdered);
            row.Loaded += (_, _) =>
            {
                foreach (var cell in dayCellsOrdered)
                    UpdateWrappedTextWidths(cell);
                SyncEmployeeSlotHeights(dayCellsOrdered);
            };

            // Aussenrahmen um den Mitarbeiter-Block
            row.ClipToBounds = true;
            var outline = new Border
            {
                BorderBrush = outlineBrush,
                BorderThickness = new Thickness(1.5),
                IsHitTestVisible = false
            };
            Grid.SetColumnSpan(outline, TotalColumnCount(days.Count));
            Panel.SetZIndex(outline, 10);
            row.Children.Add(outline);

            _employeeDayCellsOrdered[employee.Id] = dayCellsOrdered;
            return row;
    }

    /// <summary>Namenskarte: Nummer oben, Vorname/Nachname darunter – zentriert.</summary>
    private static FrameworkElement CreateEmployeeNameCard(
        Employee employee,
        Brush textPrimary,
        Brush cardBg,
        Brush cardBorder)
    {
        var number = string.IsNullOrWhiteSpace(employee.EmployeeNumber)
            ? "–"
            : employee.EmployeeNumber.Trim();
        var first = string.IsNullOrWhiteSpace(employee.FirstName) ? "" : employee.FirstName.Trim();
        var last = string.IsNullOrWhiteSpace(employee.LastName) ? "" : employee.LastName.Trim();

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.Children.Add(new TextBlock
        {
            Text = number,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = textPrimary,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        });
        if (!string.IsNullOrEmpty(first))
        {
            stack.Children.Add(new TextBlock
            {
                Text = first,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPrimary,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
        }
        if (!string.IsNullOrEmpty(last))
        {
            stack.Children.Add(new TextBlock
            {
                Text = last,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPrimary,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return new Border
        {
            Background = cardBg,
            BorderBrush = cardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 10, 8, 10),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 72,
            Child = stack
        };
    }

    private void FillDayCellTasks(
        Border dayBorder,
        MainViewModel viewModel,
        Employee employee,
        DateTime day,
        List<DateTime> days,
        int dayIndex,
        List<WorkTask?> orderedSlots,
        Brush selectedEmployeeBg,
        Brush selectedTaskBorder,
        Brush? emptySlotBg = null)
    {
        var selectedDayBg = (Brush)(TryFindResource("AccentLightBrush") ?? Brushes.LightBlue);
        var bandBg = emptySlotBg ?? Brushes.Transparent;
        var cellBg = ResolveDayColumnBackground(day, employee.Id, bandBg, selectedDayBg, viewModel);
        dayBorder.Background = cellBg;

        var tasksPanel = dayBorder.Child as StackPanel ?? new StackPanel();
        tasksPanel.Children.Clear();
        tasksPanel.ClipToBounds = true;
        var emptyBg = cellBg;

        foreach (var task in orderedSlots)
        {
            if (task == null)
            {
                tasksPanel.Children.Add(new Border
                {
                    MinHeight = TaskSlotMinHeight,
                    Margin = new Thickness(2),
                    Background = emptyBg,
                    Opacity = 0.35,
                    CornerRadius = new CornerRadius(4)
                });
                continue;
            }

            tasksPanel.Children.Add(CreateLightweightTaskChip(
                viewModel, task, day, employee, days, dayIndex, selectedEmployeeBg, selectedTaskBorder));
        }

        dayBorder.Child = tasksPanel;
        if (dayBorder.IsLoaded || dayBorder.ActualWidth > 0)
            UpdateWrappedTextWidths(dayBorder);
    }

    private static string InsertSoftWrapHints(string text) => SoftWrapText.InsertHints(text);

    private Border CreateLightweightTaskChip(
        MainViewModel viewModel,
        WorkTask task,
        DateTime day,
        Employee employee,
        List<DateTime> days,
        int dayIndex,
        Brush selectedEmployeeBg,
        Brush selectedTaskBorder)
    {
        var isSelectedTask = viewModel.SelectedTask?.Id == task.Id;
        var isSelectedTaskDay = viewModel.IsTaskDaySelected(task, day);
        var taskBrush = (Brush)_colorConverter.Convert(task.Color, typeof(Brush), null, CultureInfo.CurrentCulture)!;

        var label = InsertSoftWrapHints(task.Description);
        if (task.StartTime.HasValue)
            label = $"{task.StartTime.Value:hh\\:mm} {label}";
        if (task.IsMultiDay && task.EndDate.HasValue)
        {
            var taskStartInWeek = task.StartDate.Date < day.Date ? day.Date : task.StartDate.Date;
            var taskEndInWeek = task.EndDate.Value.Date > days[^1].Date ? days[^1].Date : task.EndDate.Value.Date;
            var spanDays = Math.Max(1, (taskEndInWeek - taskStartInWeek).Days + 1);
            spanDays = Math.Min(spanDays, days.Count - dayIndex);
            if (spanDays > 1)
                label += " →";
        }

        var textBlock = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = (Brush)(TryFindResource("TaskChipTextBrush") ?? new SolidColorBrush(Color.FromRgb(0x1A, 0x23, 0x32))),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        textBlock.MaxWidth = GetDayChipWrapWidth(dayIndex);

        var taskBorder = new Border
        {
            Background = isSelectedTask ? selectedEmployeeBg : taskBrush,
            Margin = new Thickness(2),
            Padding = new Thickness(7, 5, 7, 5),
            MinHeight = TaskSlotMinHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(7),
            Cursor = isSelectedTask ? Cursors.SizeAll : Cursors.Hand,
            BorderThickness = isSelectedTaskDay ? new Thickness(3) : (isSelectedTask ? new Thickness(1) : new Thickness(0)),
            BorderBrush = selectedTaskBorder,
            Tag = (task, day),
            ToolTip = TaskAuditText.FormatTooltip(task),
            Child = CreateTaskChipBody(viewModel, task, day, textBlock)
        };

        taskBorder.MouseLeftButtonDown += (_, ev) =>
        {
            if (taskBorder.Tag is not ValueTuple<WorkTask, DateTime> tag)
                return;
            var clickTask = tag.Item1;
            var clickDay = tag.Item2;
            if (ev.ClickCount == 2)
            {
                viewModel.SelectedTask = clickTask;
                viewModel.EditTaskCommand.Execute(null);
                ev.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && clickTask.IsMultiDay)
            {
                viewModel.SelectedTask = clickTask;
                viewModel.ToggleSelectedTaskDay(clickDay);
                UpdateSelectionVisuals();
                ev.Handled = true;
                return;
            }
            viewModel.SelectedTask = clickTask;
            _taskDragStartPoint = ev.GetPosition(null);
            _pendingTaskDrag = (clickTask, clickDay.Date, employee);
        };
        taskBorder.MouseMove += TaskBorder_MouseMove;
        taskBorder.MouseLeftButtonUp += (_, _) => _pendingTaskDrag = null;
        taskBorder.MouseEnter += (_, _) => taskBorder.Opacity = 0.92;
        taskBorder.MouseLeave += (_, _) => taskBorder.Opacity = 1.0;

        var ctxMenu = CreateThemeContextMenu();
        ctxMenu.Opened += (_, _) =>
        {
            ctxMenu.Items.Clear();
            if (taskBorder.Tag is not ValueTuple<WorkTask, DateTime> tag)
                return;
            var menuTask = tag.Item1;
            viewModel.SelectedTask = menuTask;
            var editItem = CreateThemeMenuItem(viewModel.TxtContextEdit, ContextMenuIcon.Edit);
            editItem.Click += (_, _) =>
            {
                viewModel.SelectedTask = menuTask;
                viewModel.EditTaskCommand.Execute(null);
            };
            var deleteItem = CreateThemeMenuItem(viewModel.TxtContextDelete, ContextMenuIcon.Delete);
            deleteItem.Click += (_, _) =>
            {
                viewModel.SelectedTask = menuTask;
                viewModel.DeleteTaskCommand.Execute(null);
            };
            var copyItem = CreateThemeMenuItem(viewModel.TxtContextCopy, ContextMenuIcon.Copy);
            copyItem.Click += (_, _) =>
            {
                viewModel.SelectedTask = menuTask;
                viewModel.CopySelectedTaskCommand.Execute(null);
            };
            var alreadyFavorite = viewModel.IsTaskAlreadyFavorite(menuTask);
            var favoriteItem = CreateThemeMenuItem(
                alreadyFavorite ? viewModel.TxtContextAlreadyFavorite : viewModel.TxtContextAddFavorite,
                ContextMenuIcon.Favorite);
            favoriteItem.IsEnabled = !alreadyFavorite;
            favoriteItem.Click += async (_, _) =>
            {
                viewModel.SelectedTask = menuTask;
                await viewModel.AddTaskAsFavoriteAsync(menuTask);
            };
            ctxMenu.Items.Add(editItem);
            ctxMenu.Items.Add(deleteItem);
            ctxMenu.Items.Add(copyItem);
            ctxMenu.Items.Add(favoriteItem);
        };
        taskBorder.ContextMenu = ctxMenu;
        return taskBorder;
    }

    /// <summary>
    /// Icons liegen oben rechts über dem Text – die Kartenhöhe ändert sich
    /// beim Darüberfahren nicht mehr, die Zeilen bleiben über die Woche gerade.
    /// </summary>
    private FrameworkElement CreateTaskChipBody(
        MainViewModel viewModel,
        WorkTask task,
        DateTime day,
        TextBlock textBlock)
    {
        var actionIconBg = (Brush)(TryFindResource("HeaderBrush") ?? Brushes.LightGray);
        var actionIconFg = App.IsDarkMode
            ? (Brush)(TryFindResource("TextPrimaryBrush") ?? Brushes.WhiteSmoke)
            : (Brush)(TryFindResource("TextSecondaryBrush") ?? Brushes.DimGray);
        var iconStrokeThickness = App.IsDarkMode ? 1.4 : 1.2;

        var copyGrip = new Border
        {
            Width = 18,
            Height = 18,
            Cursor = Cursors.Hand,
            ToolTip = viewModel.TxtCopyGripTooltip,
            Tag = task,
            Margin = new Thickness(2, 0, 0, 0),
            CornerRadius = new CornerRadius(9),
            Background = actionIconBg,
            BorderBrush = (Brush)(TryFindResource("BorderBrush") ?? Brushes.Gray),
            BorderThickness = new Thickness(1),
            Opacity = 0.9
        };
        copyGrip.Child = CreateCopyIcon(actionIconFg, iconStrokeThickness);
        copyGrip.MouseEnter += (_, _) => copyGrip.Opacity = 1;
        copyGrip.MouseLeave += (_, _) => copyGrip.Opacity = 0.9;
        copyGrip.PreviewMouseLeftButtonDown += CopyGrip_PreviewMouseLeftButtonDown;

        var icons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };

        var taskStartDay = task.StartDate.Date;
        var taskEndDay = (task.EndDate ?? task.StartDate).Date;
        var isTaskStartDay = day.Date == taskStartDay;
        var isTaskEndDay = day.Date == taskEndDay;
        if (isTaskStartDay || isTaskEndDay)
        {
            var resizeGrip = new Border
            {
                Width = 18,
                Height = 18,
                Cursor = Cursors.SizeWE,
                ToolTip = isTaskStartDay && isTaskEndDay
                    ? viewModel.TxtResizeGripTooltip
                    : isTaskStartDay
                        ? viewModel.TxtResizeStartGripTooltip
                        : viewModel.TxtResizeEndGripTooltip,
                Tag = (task, day.Date),
                CornerRadius = new CornerRadius(9),
                Background = actionIconBg,
                BorderBrush = (Brush)(TryFindResource("BorderBrush") ?? Brushes.Gray),
                BorderThickness = new Thickness(1),
                Opacity = 0.9
            };
            resizeGrip.Child = CreateResizeIcon(actionIconFg, iconStrokeThickness);
            resizeGrip.MouseEnter += (_, _) => resizeGrip.Opacity = 1;
            resizeGrip.MouseLeave += (_, _) => resizeGrip.Opacity = 0.9;
            resizeGrip.PreviewMouseLeftButtonDown += ResizeGrip_PreviewMouseLeftButtonDown;
            icons.Children.Add(resizeGrip);
        }

        icons.Children.Add(copyGrip);
        textBlock.Margin = new Thickness(0, 0, 44, 0);

        var body = new Grid();
        body.Children.Add(textBlock);
        body.Children.Add(icons);
        return body;
    }

    private static void SyncEmployeeSlotHeights(IReadOnlyList<Border> dayCells)
    {
        if (dayCells.Count == 0)
            return;

        var panels = new List<Panel>(dayCells.Count);
        foreach (var cell in dayCells)
        {
            if (cell.Child is Panel panel)
                panels.Add(panel);
        }

        if (panels.Count == 0)
            return;

        var slotCount = panels.Max(p => p.Children.Count);
        if (slotCount == 0)
            return;

        var maxHeights = new double[slotCount];
        for (var i = 0; i < slotCount; i++)
            maxHeights[i] = TaskSlotMinHeight;

        foreach (var panel in panels)
        {
            for (var i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is not FrameworkElement fe)
                    continue;
                var height = fe.ActualHeight > 1 ? fe.ActualHeight : fe.MinHeight;
                if (height < 1)
                    height = TaskSlotMinHeight;
                maxHeights[i] = Math.Max(maxHeights[i], height);
            }
        }

        foreach (var panel in panels)
        {
            for (var i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is FrameworkElement fe && Math.Abs(fe.MinHeight - maxHeights[i]) > 0.5)
                    fe.MinHeight = maxHeights[i];
            }
        }
    }

    private void UpdateSelectionVisuals()
    {
        if (_subscribedViewModel == null || _dayCellMap.Count == 0)
            return;

        var viewModel = _subscribedViewModel;
        var selectedDayBg = (Brush)(TryFindResource("AccentLightBrush") ?? Brushes.LightBlue);
        var headerBg = (Brush)(TryFindResource("HeaderBrush") ?? Brushes.LightGray);
        var selectedTaskBorder = (Brush)(TryFindResource("PrimaryBrush") ?? Brushes.SteelBlue);
        var selectedEmployeeBg = (Brush)(TryFindResource("AccentLightBrush") ?? Brushes.LightBlue);
        var employees = viewModel.Employees;

        foreach (var header in _dayHeadersOrdered)
        {
            if (header.Tag is DateTime day)
                ApplyDayHeaderAppearance(header, header.Child as TextBlock, day, viewModel, selectedDayBg, headerBg, ThemeBrush("TextPrimaryBrush", Brushes.Black));
        }

        for (var empIndex = 0; empIndex < employees.Count; empIndex++)
        {
            var employee = employees[empIndex];
            var isAlt = empIndex % 2 == 1;
            var bandBg = (Brush)(TryFindResource(isAlt ? "BandBrushB" : "BandBrushA") ?? Brushes.White);
            var nameBg = (Brush)(TryFindResource(isAlt ? "NameBandBrushB" : "NameBandBrushA") ?? headerBg);

            if (_employeeRowMap.TryGetValue(employee.Id, out var row))
            {
                foreach (var child in row.Children)
                {
                    if (child is Border nameBorder && Grid.GetColumn(nameBorder) == 1)
                        nameBorder.Background = viewModel.IsScheduleEmployeeNameHighlighted(employee.Id) ? selectedEmployeeBg : nameBg;
                }
            }

            if (!_employeeDayCellsOrdered.TryGetValue(employee.Id, out var cells))
                continue;

            foreach (var dayBorder in cells)
            {
                if (dayBorder.Tag is not ValueTuple<Employee, DateTime> tag)
                    continue;
                dayBorder.Background = ResolveDayColumnBackground(tag.Item2.Date, tag.Item1.Id, bandBg, selectedDayBg, viewModel);
                if (dayBorder.Child is not Panel panel)
                    continue;
                foreach (var child in panel.Children)
                {
                    if (child is not Border chip || chip.Tag is not ValueTuple<WorkTask, DateTime> chipTag)
                        continue;
                    var isSelectedTask = viewModel.SelectedTask?.Id == chipTag.Item1.Id;
                    var isSelectedDay = viewModel.IsTaskDaySelected(chipTag.Item1, chipTag.Item2);
                    chip.BorderThickness = isSelectedDay ? new Thickness(3) : (isSelectedTask ? new Thickness(1) : new Thickness(0));
                    chip.BorderBrush = selectedTaskBorder;
                    chip.Cursor = isSelectedTask ? Cursors.SizeAll : Cursors.Hand;
                    chip.Background = isSelectedTask
                        ? selectedEmployeeBg
                        : (Brush)_colorConverter.Convert(chipTag.Item1.Color, typeof(Brush), null, CultureInfo.CurrentCulture)!;
                }
            }
        }
    }

    private void EmployeeRow_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(EmployeeReorderFormat))
            return;

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private async void EmployeeRow_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(EmployeeReorderFormat) || e.Data.GetData(EmployeeReorderFormat) is not Employee dragged)
            return;
        if (sender is not Grid row || row.Tag is not Employee target)
            return;
        var viewModel = _subscribedViewModel;
        if (viewModel == null) return;
        var list = viewModel.Employees.ToList();
        int fromIdx = list.FindIndex(e => e.Id == dragged.Id);
        int toIdx = list.FindIndex(e => e.Id == target.Id);
        if (fromIdx < 0 || toIdx < 0 || fromIdx == toIdx) return;
        list.RemoveAt(fromIdx);
        list.Insert(toIdx, dragged);
        await viewModel.ReorderEmployeesAsync(list);
        e.Handled = true;
    }

    private void DayCell_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(TaskTemplatesFormat) || e.Data.GetDataPresent(TaskTemplateFormat))
            e.Effects = DragDropEffects.Copy;
        else if (e.Data.GetDataPresent(TaskCopyFormat))
            e.Effects = DragDropEffects.Copy;
        else if (e.Data.GetDataPresent(TaskResizeFormat))
            e.Effects = DragDropEffects.Move;
        else if (e.Data.GetDataPresent(TaskMoveFormat))
            e.Effects = DragDropEffects.Move;
        else
            e.Effects = DragDropEffects.None;

        if (e.Effects != DragDropEffects.None && sender is Border border)
            UpdateDragPreview(border, e.Data);

        e.Handled = true;
    }

    private void DayCell_DragLeave(object sender, DragEventArgs e)
    {
        if (_rangePreviewCells.Count > 0 && sender is Border border && _rangePreviewCells.Contains(border))
            return;
        ClearRangePreview();
        e.Handled = true;
    }

    private async void DayCell_Drop(object sender, DragEventArgs e)
    {
        ClearRangePreview();

        if (e.Data.GetDataPresent(TaskMoveFormat) && e.Data.GetData(TaskMoveFormat) is ValueTuple<WorkTask, DateTime, Employee> movePayload)
        {
            if (sender is not Border moveBorder || moveBorder.Tag is not ValueTuple<Employee, DateTime> moveTag)
                return;
            if (_subscribedViewModel == null)
                return;
            await _subscribedViewModel.MoveTaskToDayAsync(movePayload.Item1, moveTag.Item1, moveTag.Item2, movePayload.Item2);
            _ = FlashCellAsync(moveBorder);
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(TaskCopyFormat) && e.Data.GetData(TaskCopyFormat) is WorkTask copiedTask)
        {
            if (sender is not Border copyBorder || copyBorder.Tag is not ValueTuple<Employee, DateTime> copyTag)
                return;
            if (_subscribedViewModel == null)
                return;
            await _subscribedViewModel.CopyTaskToAsync(copiedTask, copyTag.Item1, copyTag.Item2);
            _ = FlashCellAsync(copyBorder);
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(TaskResizeFormat) && e.Data.GetData(TaskResizeFormat) is ValueTuple<WorkTask, DateTime> resizePayload)
        {
            if (sender is not Border resizeBorder || resizeBorder.Tag is not ValueTuple<Employee, DateTime> resizeTag)
                return;
            if (_subscribedViewModel == null || resizePayload.Item1.EmployeeId != resizeTag.Item1.Id)
                return;
            await _subscribedViewModel.AdjustTaskRangeToDayAsync(
                resizePayload.Item1.Id, resizeTag.Item2, resizePayload.Item2);
            _ = FlashCellAsync(resizeBorder);
            e.Handled = true;
            return;
        }

        if (TryGetTemplatesFromDrag(e.Data, out var templates))
        {
            if (sender is not Border templateBorder || templateBorder.Tag is not ValueTuple<Employee, DateTime> templateTag)
                return;
            if (_subscribedViewModel == null) return;
            await _subscribedViewModel.CreateTasksFromTemplatesAsync(templateTag.Item1, templateTag.Item2, templates);
            _ = FlashCellAsync(templateBorder);
            e.Handled = true;
        }
    }

    private static bool TryGetTemplatesFromDrag(IDataObject data, out List<TaskTemplate> templates)
    {
        if (data.GetDataPresent(TaskTemplatesFormat) && data.GetData(TaskTemplatesFormat) is List<TaskTemplate> list)
        {
            templates = list;
            return templates.Count > 0;
        }

        if (data.GetDataPresent(TaskTemplateFormat) && data.GetData(TaskTemplateFormat) is TaskTemplate single)
        {
            templates = new List<TaskTemplate> { single };
            return true;
        }

        templates = new List<TaskTemplate>();
        return false;
    }

    private static async Task FlashCellAsync(Border cell)
    {
        var original = cell.Background;
        cell.Background = new SolidColorBrush(Color.FromRgb(186, 230, 253));
        await Task.Delay(220);
        cell.Background = original;
    }

    private async void DayCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2
            && sender is Border doubleClickCell
            && doubleClickCell.Tag is ValueTuple<Employee, DateTime> doubleClickTag
            && _subscribedViewModel != null
            && FindAncestorTaskBorder(e.OriginalSource as DependencyObject) == null)
        {
            EndRangeDrag(doubleClickCell);
            await _subscribedViewModel.OpenNewTaskDialogAsync(doubleClickTag.Item1, doubleClickTag.Item2);
            e.Handled = true;
            return;
        }

        if (sender is Border cellBorder && cellBorder.Tag is ValueTuple<Employee, DateTime> cellTag && _subscribedViewModel != null)
        {
            if (e.OriginalSource is DependencyObject clickSource && FindAncestorTaskBorder(clickSource) == null)
            {
                _subscribedViewModel.SelectScheduleCell(cellTag.Item1, cellTag.Item2);
                _subscribedViewModel.SelectedTask = null;
                UpdateSelectionVisuals();
            }
            else
            {
                _subscribedViewModel.SelectedEmployee = cellTag.Item1;
                _subscribedViewModel.SelectedDate = cellTag.Item2.Date;
            }
        }

        if (_subscribedViewModel?.SelectedTask is { } selectedTask && sender is Border ctrlBorder && ctrlBorder.Tag is ValueTuple<Employee, DateTime> ctrlTag)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                ctrlTag.Item1.Id == selectedTask.EmployeeId &&
                selectedTask.StartDate.Date <= ctrlTag.Item2.Date &&
                (selectedTask.EndDate ?? selectedTask.StartDate).Date >= ctrlTag.Item2.Date)
            {
                _subscribedViewModel.ToggleSelectedTaskDay(ctrlTag.Item2);
                UpdateSelectionVisuals();
                e.Handled = true;
                return;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift &&
                ctrlTag.Item1.Id == selectedTask.EmployeeId)
            {
                await _subscribedViewModel.AdjustSelectedTaskRangeToDayAsync(ctrlTag.Item2, ctrlTag.Item2);
                e.Handled = true;
                return;
            }
        }

        if (_subscribedViewModel == null || GetActiveTemplates(_subscribedViewModel).Count == 0)
            return;
        if (sender is not Border border || border.Tag is not ValueTuple<Employee, DateTime> tag)
            return;
        if (e.OriginalSource is DependencyObject templateSource && FindAncestorTaskBorder(templateSource) != null)
            return;

        _rangeDragActive = true;
        _rangeDragMoved = false;
        _rangeDragEmployee = tag.Item1;
        _rangeDragStart = tag.Item2.Date;
        _rangeDragEnd = tag.Item2.Date;
        border.CaptureMouse();
        UpdateRangePreview();
        e.Handled = true;
    }

    private void DayCell_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_rangeDragActive || _rangeDragEmployee == null || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (sender is not Border border || border.Tag is not ValueTuple<Employee, DateTime> tag)
            return;
        if (tag.Item1.Id != _rangeDragEmployee.Id)
            return;

        _rangeDragEnd = tag.Item2.Date;
        if (_rangeDragEnd != _rangeDragStart)
            _rangeDragMoved = true;
        UpdateRangePreview();
    }

    private async void DayCell_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_rangeDragActive || _subscribedViewModel == null || _rangeDragEmployee == null)
            return;
        if (sender is not Border border || border.Tag is not ValueTuple<Employee, DateTime>)
            return;

        var templates = GetActiveTemplates(_subscribedViewModel);
        if (templates.Count == 0)
        {
            EndRangeDrag(border);
            return;
        }

        if (!_rangeDragMoved)
        {
            EndRangeDrag(border);
            return;
        }

        var employee = _rangeDragEmployee;
        var start = _rangeDragStart <= _rangeDragEnd ? _rangeDragStart : _rangeDragEnd;
        var end = _rangeDragEnd >= _rangeDragStart ? _rangeDragEnd : _rangeDragStart;

        EndRangeDrag(border);
        await _subscribedViewModel.CreateTasksFromTemplateRangeAsync(employee, start, end, templates);
        e.Handled = true;
    }

    private void EndRangeDrag(Border border)
    {
        border.ReleaseMouseCapture();
        _rangeDragActive = false;
        _rangeDragEmployee = null;
        ClearRangePreview();
    }

    private void UpdateRangePreview()
    {
        ClearRangePreview();
        if (_rangeDragEmployee == null)
            return;

        var start = _rangeDragStart <= _rangeDragEnd ? _rangeDragStart : _rangeDragEnd;
        var end = _rangeDragEnd >= _rangeDragStart ? _rangeDragEnd : _rangeDragStart;
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
        {
            if (!_dayCellMap.TryGetValue((_rangeDragEmployee.Id, d), out var cell))
                continue;

            cell.BorderThickness = new Thickness(2);
            cell.BorderBrush = Brushes.DodgerBlue;
            _rangePreviewCells.Add(cell);
        }
    }

    private void ClearRangePreview()
    {
        foreach (var cell in _rangePreviewCells)
        {
            cell.BorderThickness = new Thickness(0, 0, 1, 1);
            cell.BorderBrush = (Brush)(TryFindResource("BorderBrush") ?? Brushes.LightGray);
        }
        _rangePreviewCells.Clear();
    }

    private Brush ThemeBrush(string resourceKey, Brush fallback) =>
        (Brush)(TryFindResource(resourceKey) ?? fallback);

    private static Brush TodayColumnBackground() =>
        new SolidColorBrush(App.IsDarkMode
            ? Color.FromRgb(0x8B, 0x4A, 0x4A)
            : Color.FromRgb(0xF8, 0xD0, 0xCC));

    private Brush ResolveDayColumnBackground(
        DateTime day,
        int employeeId,
        Brush bandBg,
        Brush selectedDayBg,
        MainViewModel viewModel)
    {
        if (day.Date == DateTime.Today)
            return TodayColumnBackground();
        if (viewModel.IsScheduleCellHighlighted(employeeId, day.Date))
            return selectedDayBg;
        return bandBg;
    }

    private void ApplyDayHeaderAppearance(
        Border header,
        TextBlock? headerText,
        DateTime day,
        MainViewModel viewModel,
        Brush selectedDayBg,
        Brush headerBg,
        Brush textPrimary)
    {
        var isToday = day.Date == DateTime.Today;
        var isSelected = viewModel.IsScheduleColumnHeaderHighlighted(day.Date);
        var isHoliday = !string.IsNullOrEmpty(viewModel.GetHolidayLabel(day.Date));
        if (isToday)
            header.Background = TodayColumnBackground();
        else if (isSelected)
            header.Background = selectedDayBg;
        else if (isHoliday)
            header.Background = new SolidColorBrush(Color.FromRgb(255, 236, 179));
        else
            header.Background = headerBg;

        if (headerText != null)
        {
            headerText.FontWeight = isToday ? FontWeights.Bold : FontWeights.SemiBold;
            headerText.Foreground = textPrimary;
        }
    }

    private enum ContextMenuIcon { Edit, Delete, Copy, Paste, Favorite }

    private ContextMenu CreateThemeContextMenu() =>
        new() { MinWidth = 200 };

    private MenuItem CreateThemeMenuItem(string header, ContextMenuIcon? icon = null, Brush? colorDot = null) =>
        new() { Header = CreateMenuItemHeader(header, icon, colorDot) };

    private UIElement CreateMenuItemHeader(string text, ContextMenuIcon? icon, Brush? colorDot)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var fontSize = (double)(TryFindResource("FontSizeBody") ?? 14d);
        var textBrush = ThemeBrush("TextPrimaryBrush", Brushes.Black);

        if (colorDot != null)
        {
            row.Children.Add(new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = colorDot,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                BorderBrush = ThemeBrush("BorderBrush", Brushes.Gray),
                BorderThickness = new Thickness(0.5)
            });
        }
        else if (icon != null)
            row.Children.Add(CreateContextMenuIcon(icon.Value));

        row.Children.Add(new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = fontSize,
            Foreground = textBrush
        });
        return row;
    }

    private UIElement CreateContextMenuIcon(ContextMenuIcon icon)
    {
        var stroke = ThemeBrush("TextSecondaryBrush", Brushes.DimGray);
        const double thickness = 1.4;

        if (icon == ContextMenuIcon.Favorite)
        {
            return new TextBlock
            {
                Text = "★",
                FontSize = 14,
                Foreground = stroke,
                Width = 16,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        if (icon == ContextMenuIcon.Copy)
        {
            return new Viewbox
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = CreateCopyIcon(stroke, thickness)
            };
        }

        UIElement glyph = icon switch
        {
            ContextMenuIcon.Edit => new Path
            {
                Data = Geometry.Parse("M2,10 L2,12 L4,12 L10,6 L8,4 Z M9,5 L11,7"),
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent
            },
            ContextMenuIcon.Delete => new Path
            {
                Data = Geometry.Parse("M4,4.5 H10 M5,4.5 V3.5 H9 V4.5 M5,7 V10 M7,7 V10 M9,7 V10 M4.5,4.5 L5.5,10.5 H8.5 L9.5,4.5"),
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent
            },
            ContextMenuIcon.Paste => new Path
            {
                Data = Geometry.Parse("M4,3 H9 C9.5,3 10,3.5 10,4 V9 C10,9.5 9.5,10 9,10 H4 C3.5,10 3,9.5 3,9 V4 C3,3.5 3.5,3 4,3 Z M5,1.5 H8 C8.3,1.5 8.5,1.7 8.5,2 V3 H4.5 V2 C4.5,1.7 4.7,1.5 5,1.5"),
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent
            },
            _ => new Border()
        };

        return new Viewbox
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = glyph
        };
    }

    private static Border? FindAncestorTaskBorder(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is Border border && (border.Tag is WorkTask || border.Tag is ValueTuple<WorkTask, DateTime>))
                return border;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void TaskBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pendingTaskDrag is not { } pending || sender is not Border taskBorder)
            return;
        if (_subscribedViewModel?.SelectedTask?.Id != pending.Task.Id)
            return;

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _taskDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _taskDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _pendingTaskDrag = null;
        try
        {
            DragDrop.DoDragDrop(taskBorder,
                new DataObject(TaskMoveFormat, (pending.Task, pending.AnchorDay, pending.Employee)),
                DragDropEffects.Move);
        }
        catch
        {
            // no-op
        }
        finally
        {
            ClearRangePreview();
        }
    }

    private void UpdateDragPreview(Border hoverCell, IDataObject data)
    {
        if (hoverCell.Tag is not ValueTuple<Employee, DateTime> hoverTag)
            return;

        ClearRangePreview();

        if (data.GetDataPresent(TaskResizeFormat) && data.GetData(TaskResizeFormat) is ValueTuple<WorkTask, DateTime> resizePayload)
        {
            var task = resizePayload.Item1;
            var anchor = resizePayload.Item2;
            if (task.EmployeeId != hoverTag.Item1.Id)
                return;

            var start = task.StartDate.Date;
            var end = (task.EndDate ?? task.StartDate).Date;
            var (newStart, newEnd) = MainViewModel.ComputeResizedRange(start, end, hoverTag.Item2, anchor);
            HighlightDayRange(hoverTag.Item1.Id, newStart, newEnd);
            return;
        }

        if (data.GetDataPresent(TaskMoveFormat) && data.GetData(TaskMoveFormat) is ValueTuple<WorkTask, DateTime, Employee> movePayload)
        {
            var task = movePayload.Item1;
            var offsetDays = (hoverTag.Item2.Date - movePayload.Item2.Date).Days;
            var start = task.StartDate.Date.AddDays(offsetDays);
            var end = (task.EndDate ?? task.StartDate).Date.AddDays(offsetDays);
            HighlightDayRange(hoverTag.Item1.Id, start, end);
            return;
        }

        HighlightDayRange(hoverTag.Item1.Id, hoverTag.Item2.Date, hoverTag.Item2.Date);
    }

    private void HighlightDayRange(int employeeId, DateTime start, DateTime end)
    {
        var rangeStart = start <= end ? start : end;
        var rangeEnd = end >= start ? end : start;
        for (var day = rangeStart.Date; day <= rangeEnd.Date; day = day.AddDays(1))
        {
            if (!_dayCellMap.TryGetValue((employeeId, day), out var cell))
                continue;

            cell.BorderThickness = new Thickness(2);
            cell.BorderBrush = Brushes.DodgerBlue;
            _rangePreviewCells.Add(cell);
        }
    }

    private void DefaultTasksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_subscribedViewModel == null || sender is not ListBox listBox)
            return;

        var selected = listBox.SelectedItems.Cast<TaskTemplate>().ToList();
        _subscribedViewModel.SyncSelectedTaskTemplates(selected);
    }

    private void ScheduleScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_subscribedViewModel == null || e.OriginalSource is not DependencyObject source)
            return;
        if (FindAncestorTaskBorder(source) != null)
            return;
        if (FindAncestor<Grid>(source, g => g.Tag is Employee) != null)
            return;
        if (FindAncestor<Border>(source, b => b.Tag is ValueTuple<Employee, DateTime>) != null)
            return;
        if (FindAncestor<Border>(source, b => b.Tag is DateTime) != null)
            return;

        _subscribedViewModel.ClearScheduleSelection();
        UpdateSelectionVisuals();
    }

    private static T? FindAncestor<T>(DependencyObject? element, Func<T, bool>? predicate = null) where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T match && (predicate == null || predicate(match)))
                return match;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void ResizeGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_subscribedViewModel == null)
            return;
        if (sender is not Border grip || grip.Tag is not ValueTuple<WorkTask, DateTime> tag)
            return;

        _subscribedViewModel.SelectedTask = tag.Item1;
        try
        {
            DragDrop.DoDragDrop(grip, new DataObject(TaskResizeFormat, (tag.Item1, tag.Item2)), DragDropEffects.Move);
        }
        catch
        {
            // no-op
        }
        e.Handled = true;
    }

    private void CopyGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border grip || grip.Tag is not WorkTask task)
            return;

        if (_subscribedViewModel != null)
            _subscribedViewModel.SelectedTask = task;
        try
        {
            DragDrop.DoDragDrop(grip, new DataObject(TaskCopyFormat, task), DragDropEffects.Copy);
        }
        catch
        {
            // no-op
        }
        e.Handled = true;
    }

    private void HamburgerOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel == null)
            return;
        _subscribedViewModel.IsOptionsPanelOpen = !_subscribedViewModel.IsOptionsPanelOpen;
    }

    private void ScheduleView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _subscribedViewModel == null || !_subscribedViewModel.IsOptionsPanelOpen)
            return;
        _subscribedViewModel.IsOptionsPanelOpen = false;
        e.Handled = true;
    }

    private static UIElement CreateCopyIcon(Brush foreground, double strokeThickness)
    {
        var canvas = new Canvas { Width = 12, Height = 12 };
        var back = new Rectangle
        {
            Width = 6,
            Height = 7,
            Stroke = foreground,
            StrokeThickness = strokeThickness,
            RadiusX = 1,
            RadiusY = 1
        };
        Canvas.SetLeft(back, 2);
        Canvas.SetTop(back, 1);

        var front = new Rectangle
        {
            Width = 6,
            Height = 7,
            Stroke = foreground,
            StrokeThickness = strokeThickness,
            Fill = Brushes.Transparent,
            RadiusX = 1,
            RadiusY = 1
        };
        Canvas.SetLeft(front, 4);
        Canvas.SetTop(front, 3);

        canvas.Children.Add(back);
        canvas.Children.Add(front);
        return new Viewbox { Width = 11, Height = 11, Child = canvas };
    }

    private static UIElement CreateResizeIcon(Brush foreground, double strokeThickness)
    {
        var path = new Path
        {
            Stroke = foreground,
            StrokeThickness = strokeThickness,
            Data = Geometry.Parse("M1,6 L11,6 M1,6 L3,4 M1,6 L3,8 M11,6 L9,4 M11,6 L9,8"),
            Stretch = Stretch.Uniform,
            Width = 12,
            Height = 12
        };

        return new Viewbox { Width = 11, Height = 11, Child = path };
    }

    private void CaptureColumnWidths(Grid headerGrid, int dayCount)
    {
        var expected = TotalColumnCount(dayCount);
        if (headerGrid.ColumnDefinitions.Count != expected)
            return;

        for (var i = 0; i < expected; i++)
        {
            var width = headerGrid.ColumnDefinitions[i].ActualWidth;
            if (IsSplitterColumn(i))
            {
                _columnWidths[i] = SplitterColumnWidth;
                continue;
            }

            if (IsFillerColumn(i))
                continue;

            if (width > 20)
            {
                if (_columnWidths.Count <= i)
                    _columnWidths.Add(width);
                else
                    _columnWidths[i] = width;
            }
        }
    }

    private void EnsureColumnWidths(int dayCount)
    {
        var expected = TotalColumnCount(dayCount);
        while (_columnWidths.Count < expected)
        {
            var index = _columnWidths.Count;
            _columnWidths.Add(GetDefaultColumnWidth(index, dayCount));
        }

        while (_columnWidths.Count > expected)
            _columnWidths.RemoveAt(_columnWidths.Count - 1);

        for (var i = 0; i < expected; i++)
        {
            if (IsSplitterColumn(i))
                _columnWidths[i] = SplitterColumnWidth;
        }
    }

    private static bool IsSplitterColumn(int index) => ScheduleGridLayout.IsSplitterColumn(index);

    private bool IsFillerColumn(int index) => ScheduleGridLayout.IsFillerColumn(index, _lastDayCount);

    private bool IsDayContentColumn(int index) => ScheduleGridLayout.IsDayContentColumn(index, _lastDayCount);

    private static double GetDefaultColumnWidth(int index, int dayCount) =>
        ScheduleGridLayout.GetDefaultColumnWidth(index, dayCount);

    private ColumnDefinition CreateColumnDefinition(int index)
    {
        if (IsFillerColumn(index))
        {
            return new ColumnDefinition
            {
                Width = new GridLength(0),
                MinWidth = 0,
                MaxWidth = 0
            };
        }

        if (IsSplitterColumn(index))
        {
            return new ColumnDefinition
            {
                Width = new GridLength(SplitterColumnWidth),
                MinWidth = SplitterColumnWidth
            };
        }

        if (IsDayContentColumn(index))
        {
            return new ColumnDefinition
            {
                Width = CreateColumnWidth(index),
                MinWidth = ScheduleColumnLayout.DayColumnMinWidth(_lastDayCount)
            };
        }

        return new ColumnDefinition
        {
            Width = CreateColumnWidth(index),
            MinWidth = 48
        };
    }

    private GridLength CreateColumnWidth(int index)
    {
        if (IsFillerColumn(index))
            return new GridLength(0);
        if (IsSplitterColumn(index))
            return new GridLength(SplitterColumnWidth);
        if (index >= _columnWidths.Count)
            return new GridLength(1, GridUnitType.Star);
        if (IsDayContentColumn(index))
            return ScheduleColumnLayout.DayColumnWidth(_columnWidths[index], _lastDayCount);
        return new GridLength(Math.Max(1, _columnWidths[index]));
    }

    private void AddHeaderColumnSplitter(Grid headerGrid, int splitterColumnIndex)
    {
        var splitter = new GridSplitter
        {
            Width = SplitterColumnWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            ShowsPreview = false,
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeWE
        };
        splitter.DragCompleted += (_, _) =>
        {
            SyncColumnWidthsToRows();
            QueueScheduleSizeLayout();
        };
        splitter.MouseEnter += (_, _) => splitter.Background = (Brush)(TryFindResource("BorderBrush") ?? Brushes.Gray);
        splitter.MouseLeave += (_, _) => splitter.Background = Brushes.Transparent;
        Panel.SetZIndex(splitter, 1000);
        Grid.SetColumn(splitter, splitterColumnIndex);
        headerGrid.Children.Add(splitter);
    }

    private void SyncColumnWidthsToRows()
    {
        var headerGrid = FindName("HeaderGrid") as Grid;
        if (headerGrid == null)
            return;

        for (var i = 0; i < headerGrid.ColumnDefinitions.Count; i++)
        {
            if (IsSplitterColumn(i))
            {
                if (i < _columnWidths.Count)
                    _columnWidths[i] = SplitterColumnWidth;
                continue;
            }

            if (IsFillerColumn(i))
                continue;

            var width = headerGrid.ColumnDefinitions[i].ActualWidth;
            if (width > 20)
            {
                if (_columnWidths.Count <= i)
                    _columnWidths.Add(width);
                else
                    _columnWidths[i] = width;
            }
        }

        ApplyColumnWidthsToRows();
    }

    private static void UpdateWrappedTextWidths(FrameworkElement container)
    {
        var width = container.ActualWidth;
        if (container is Border border)
            width = border.ActualWidth - border.Padding.Left - border.Padding.Right - 4;
        else
            width = width - container.Margin.Left - container.Margin.Right;

        if (width < 20)
            return;
        // Bei unendlicher Messung (ScrollViewer Auto) nicht die Riesenkarte zementieren.
        if (width > 600)
            return;

        ApplyWrappedTextMaxWidth(container, width);
    }

    private double GetDayChipWrapWidth(int dayIndex)
    {
        var colIndex = DayContentColumn(dayIndex);
        if (colIndex < _columnWidths.Count)
        {
            var columnWidth = _columnWidths[colIndex];
            if (columnWidth > 40)
                return Math.Clamp(columnWidth - 28, 48, 400);
        }

        return 140;
    }

    private static void ApplyWrappedTextMaxWidth(DependencyObject parent, double maxWidth)
    {
        if (parent is TextBlock textBlock && textBlock.TextWrapping == TextWrapping.Wrap)
            textBlock.MaxWidth = maxWidth;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            ApplyWrappedTextMaxWidth(VisualTreeHelper.GetChild(parent, i), maxWidth);
    }
}
