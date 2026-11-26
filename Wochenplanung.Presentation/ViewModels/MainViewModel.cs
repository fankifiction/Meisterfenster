using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Threading;
using System.Globalization;
using System.Threading;
using ClosedXML.Excel;
using Wochenplanung.Application.Interfaces;
using Wochenplanung.Application.Services;
using Wochenplanung.Application.Exceptions;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Domain.ValueObjects;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.Services;
using Wochenplanung.Presentation.Views;
using App = Wochenplanung.Presentation.App;
using MessageBox = Wochenplanung.Presentation.Services.ModernMessageBox;

namespace Wochenplanung.Presentation.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ScheduleService _scheduleService;
    private readonly TaskHistory _history = new();
    private readonly ILogger<MainViewModel> _logger;
    private readonly Presentation.Services.ExportService _exportService;
    private readonly Presentation.Services.TaskTemplateStorage _templateStorage;
    private readonly Presentation.Services.UpdateCheckService _updateCheckService;
    private readonly Presentation.Services.DemoDataService _demoDataService;
    private readonly IDatabaseInfo _databaseOptions;
    private readonly ScheduleBackupService _backupService;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    [ObservableProperty]
    private WeekInfo _currentWeek;

    [ObservableProperty]
    private ScheduleViewMode _currentViewMode = ScheduleViewMode.Week;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Now;

    [ObservableProperty]
    private DateTime _currentMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterEmployeeOptions))]
    private List<Employee> _employees = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredTasks))]
    private List<WorkTask> _tasks = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredTasks))]
    private string _filterText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredTasks))]
    private Employee? _filterEmployee;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredTasks))]
    private TaskType? _filterTaskType;

    /// <summary>Für Filter-ComboBox: (Alle) + alle Mitarbeiter.</summary>
    public IEnumerable<Employee?> FilterEmployeeOptions => Enumerable.Repeat<Employee?>(null, 1).Concat(Employees);

    /// <summary>Für Filter-ComboBox: (Alle) + alle Aufgabentypen.</summary>
    public IEnumerable<TaskType?> FilterTaskTypeOptions => Enumerable.Repeat<TaskType?>(null, 1).Concat(Enum.GetValues(typeof(TaskType)).Cast<TaskType>().Select(t => (TaskType?)t));

    /// <summary>Tasks nach Filter (Suche, Mitarbeiter, Aufgabentyp) gefiltert.</summary>
        public List<WorkTask> FilteredTasks =>
        TaskFilter.Apply(Tasks, FilterEmployee, FilterTaskType, FilterText).ToList();

    [ObservableProperty]
    private Employee? _selectedEmployee;

    [ObservableProperty]
    private ScheduleHighlightMode _scheduleHighlightMode;

    [ObservableProperty]
    private int? _highlightCellEmployeeId;

    [ObservableProperty]
    private DateTime? _highlightCellDay;

    [ObservableProperty]
    private DateTime? _highlightColumnDay;

    [ObservableProperty]
    private int? _highlightRowEmployeeId;

    [ObservableProperty]
    private WorkTask? _selectedTask;
    private readonly HashSet<DateTime> _selectedTaskDays = new();
    private CopiedTaskPayload? _copiedTask;

    [ObservableProperty]
    private bool _isFilterPanelExpanded;
    [ObservableProperty]
    private bool _showFilterBar = true;
    [ObservableProperty]
    private bool _isOptionsPanelOpen;

    [ObservableProperty]
    private bool _isAutoUpdateEnabled;

    [ObservableProperty]
    private bool _isAutoReloadEnabled = true;

    private readonly DispatcherTimer _autoReloadTimer = new() { Interval = TimeSpan.FromSeconds(90) };
    private bool _loadingUiLayout;

    [ObservableProperty] private bool _showPrevWeekButton = true;
    [ObservableProperty] private bool _showPrevDayButton = true;
    [ObservableProperty] private bool _showPrevMonthButton = true;
    [ObservableProperty] private bool _showTodayButton = true;
    [ObservableProperty] private bool _showCurrentWeekButton = false;
    [ObservableProperty] private bool _showNextWeekButton = true;
    [ObservableProperty] private bool _showCurrentDayButton = true;
    [ObservableProperty] private bool _showNextDayButton = true;
    [ObservableProperty] private bool _showCurrentMonthButton = true;
    [ObservableProperty] private bool _showNextMonthButton = true;
    [ObservableProperty] private bool _showViewDayButton = true;
    [ObservableProperty] private bool _showViewWeekButton = true;
    [ObservableProperty] private bool _showViewMonthButton = true;
    [ObservableProperty] private bool _showExcelButton = true;
    [ObservableProperty] private bool _showPdfButton = true;
    [ObservableProperty] private bool _showAddEmployeeButton = true;
    [ObservableProperty] private bool _showEditEmployeeButton = true;
    [ObservableProperty] private bool _showDeleteEmployeeButton = true;
    [ObservableProperty] private bool _showAddTaskButton = true;
    [ObservableProperty] private bool _showEditTaskButton = true;
    [ObservableProperty] private bool _showDeleteTaskButton = true;
    [ObservableProperty] private bool _showUndoButton = true;
    [ObservableProperty] private bool _showRedoButton = true;
    [ObservableProperty] private bool _showHolidayButton = true;
    [ObservableProperty] private bool _showCompanyVacationButton = true;
    [ObservableProperty] private bool _showImportButton = true;
    [ObservableProperty] private bool _showPrintButton = true;
    [ObservableProperty] private bool _showBackupButton = true;
    [ObservableProperty] private bool _showCopyWeekButton = true;
    [ObservableProperty] private bool _showStatisticsButton = true;
    [ObservableProperty] private bool _showAbsenceButton = true;
    [ObservableProperty] private bool _showRestoreButton = true;
    [ObservableProperty] private bool _showWeekend;
    [ObservableProperty] private bool _showInactiveEmployees;
    [ObservableProperty] private bool _isPlanShareRunning;
    [ObservableProperty] private string _planShareUrl = string.Empty;

    private readonly PlanShareHost _planShare = new();
    private List<Employee> _allEmployees = new();

    public bool IsReadOnly => !_databaseOptions.CanWrite;
    public bool ShowRestoreButtonOnToolbar => ShowRestoreButton && !IsServerMode;

    /// <summary>Von der Ansicht gesetzt: echter Druckdialog über das sichtbare Raster.</summary>
    public Func<bool>? TryPrintSchedule { get; set; }

    [ObservableProperty] private string _selectedLanguageCode = "de-CH";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteTemplateCommand))]
    private TaskTemplate? _selectedTaskTemplate;

    public ObservableCollection<TaskTemplate> SelectedTaskTemplates { get; } = new();

    partial void OnSelectedTaskChanged(WorkTask? value)
    {
        _selectedTaskDays.Clear();
    }

    public bool HasCopiedTask => _copiedTask != null;
    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;
    public bool IsGermanLanguage => SelectedLanguageCode == "de-CH";
    public bool IsEnglishLanguage => SelectedLanguageCode == "en-US";
    public bool IsFrenchLanguage => SelectedLanguageCode == "fr-FR";
    public bool IsItalianLanguage => SelectedLanguageCode == "it-IT";
    public bool IsDarkModeEnabled => App.IsDarkMode;

    public string TxtOptions => Tr("Optionen", "Options", "Options", "Opzioni");
    public string TxtButtons => Tr("Buttons anzeigen", "Show buttons", "Afficher les boutons", "Mostra pulsanti");
    public string TxtDatabaseSettings => Tr("Datenbank / Server", "Database / Server", "Base de données / Serveur", "Database / Server");
    public string TxtRefresh => Tr("Aktualisieren", "Refresh", "Actualiser", "Aggiorna");
    public string TxtRefreshTooltip => Tr("Plan vom Server neu laden (F5)", "Reload schedule from server (F5)", "Recharger le planning depuis le serveur (F5)", "Ricarica il piano dal server (F5)");
    public string TxtAutoReload => Tr("Plan automatisch aktualisieren", "Auto-refresh schedule", "Actualiser le planning automatiquement", "Aggiorna il piano automaticamente");
    public string TxtAutoReloadHint => Tr("Alle 90 Sekunden vom Server neu laden, wenn zwei PCs gleichzeitig planen.", "Reload from the server every 90 seconds when two PCs plan at the same time.", "Recharger depuis le serveur toutes les 90 secondes.", "Ricarica dal server ogni 90 secondi.");
    public bool IsServerMode => _databaseOptions.IsServer;
    public string TxtDatabaseStatus => IsServerMode
        ? $"{Tr("Server", "Server", "Serveur", "Server")}: {_databaseOptions.DisplayName}"
        : Tr("Lokal", "Local", "Local", "Locale");
    public string TxtLanguage => Tr("Sprache", "Language", "Langue", "Lingua");
    public string TxtFilter => Tr("Filter", "Filter", "Filtre", "Filtro");
    public string TxtFilterReset => Tr("Filter zurücksetzen", "Reset filter", "Réinitialiser le filtre", "Reimposta filtro");
    public string TxtEmployeesGroup => Tr("Mitarbeiter", "Employees", "Employés", "Dipendenti");
    public string TxtEmployeeColumn => Tr("Mitarbeiter", "Employee", "Employé", "Dipendente");
    public string TxtEmployeeReorderHint => Tr("≡ zum Verschieben", "≡ drag to reorder", "≡ glisser pour déplacer", "≡ trascina per riordinare");
    public string TxtEmployeeSortHint => EmployeeSortMode == EmployeeSortMode.Number
        ? Tr("Klick: nach Name", "Click: by name", "Clic : par nom", "Clic: per nome")
        : Tr("Klick: nach Nummer", "Click: by number", "Clic : par numéro", "Clic: per numero");
    public string TxtEmployeeSortLabel => EmployeeSortMode switch
    {
        EmployeeSortMode.Number => Tr("Mitarbeiter ↓ Nr.", "Employee ↓ No.", "Employé ↓ N°", "Dipendente ↓ N°"),
        EmployeeSortMode.Name => Tr("Mitarbeiter ↓ Name", "Employee ↓ Name", "Employé ↓ Nom", "Dipendente ↓ Nome"),
        _ => Tr("Mitarbeiter", "Employee", "Employé", "Dipendente")
    };
    public string TxtEmployeeSortTooltip => Tr(
        "Klicken zum Sortieren nach Nummer / Name. Mit ≡ manuell verschieben – die Reihenfolge bleibt nach Neustart erhalten.",
        "Click to sort by number / name. Use ≡ to reorder manually – order is kept after restart.",
        "Cliquer pour trier par numéro / nom. Utilisez ≡ pour réordonner – l'ordre est conservé après redémarrage.",
        "Clic per ordinare per numero / nome. Usa ≡ per riordinare – l'ordine resta dopo il riavvio.");

    public EmployeeSortMode EmployeeSortMode { get; private set; } = EmployeeSortMode.Custom;
    public string TxtCalendarWeekShort => Tr("KW", "CW", "SW", "SW");
    public string TxtTasksGroup => Tr("Aufgaben", "Tasks", "Tâches", "Attività");
    public string TxtExportGroup => Tr("Export", "Export", "Export", "Esporta");
    public string TxtToday => Tr("Heute", "Today", "Aujourd'hui", "Oggi");
    public string TxtPrevNavGroup => Tr("Zurück-Buttons", "Previous buttons", "Boutons précédent", "Pulsanti precedente");
    public string TxtCurrentNavGroup => Tr("Aktuell-Buttons", "Current buttons", "Boutons actuel", "Pulsanti attuale");
    public string TxtNextNavGroup => Tr("Weiter-Buttons", "Next buttons", "Boutons suivant", "Pulsanti successivo");
    public string TxtCurrentWeek => Tr("Aktuelle Woche", "Current week", "Semaine actuelle", "Settimana attuale");
    public string TxtNextWeek => Tr("Nächste Woche ►", "Next week ►", "Semaine suivante ►", "Settimana successiva ►");
    public string TxtCurrentDay => Tr("Aktueller Tag", "Current day", "Jour actuel", "Giorno attuale");
    public string TxtNextDay => Tr("Nächster Tag ►", "Next day ►", "Jour suivant ►", "Giorno successivo ►");
    public string TxtCurrentMonth => Tr("Aktueller Monat", "Current month", "Mois actuel", "Mese attuale");
    public string TxtNextMonth => Tr("Nächster Monat ►", "Next month ►", "Mois suivant ►", "Mese successivo ►");
    public string TxtPrevWeek => Tr("◄ Vorherige Woche", "◄ Previous week", "◄ Semaine précédente", "◄ Settimana precedente");
    public string TxtPrevDay => Tr("◄ Vorheriger Tag", "◄ Previous day", "◄ Jour précédent", "◄ Giorno precedente");
    public string TxtPrevMonth => Tr("◄ Vorheriger Monat", "◄ Previous month", "◄ Mois précédent", "◄ Mese precedente");
    public string TxtViewDay => Tr("Tag", "Day", "Jour", "Giorno");
    public string TxtViewWeek => Tr("Woche", "Week", "Semaine", "Settimana");
    public string TxtViewMonth => Tr("Monat", "Month", "Mois", "Mese");
    public string TxtAddEmployee => Tr("+ Mitarbeiter", "+ Employee", "+ Employé", "+ Dipendente");
    public string TxtEditEmployee => Tr("Mitarbeiter bearbeiten", "Edit employee", "Modifier employé", "Modifica dipendente");
    public string TxtDeleteEmployee => Tr("Mitarbeiter ausblenden", "Hide employee", "Masquer l'employé", "Nascondi dipendente");
    public string TxtSearch => Tr("Suchen", "Search", "Rechercher", "Cerca");
    public string TxtShareWlan => IsPlanShareRunning
        ? Tr("WLAN-Plan stoppen", "Stop WLAN plan", "Arrêter le plan Wi-Fi", "Ferma piano Wi-Fi")
        : Tr("Plan im WLAN", "Plan on WLAN", "Plan sur le Wi-Fi", "Piano in Wi-Fi");
    public string TxtShowWeekend => Tr("Wochenende anzeigen (Sa/So)", "Show weekend (Sat/Sun)", "Afficher le week-end (sam/dim)", "Mostra weekend (sab/dom)");
    public string TxtShowInactiveEmployees => Tr("Ausgeblendete Mitarbeiter zeigen", "Show hidden employees", "Afficher les employés masqués", "Mostra dipendenti nascosti");
    public string TxtReadOnlyBanner => Tr(
        "Nur-Lesen: dieses SQL-Konto darf den Plan nicht ändern (Rolle meisterfenster_lesen).",
        "Read-only: this SQL login cannot change the plan (role meisterfenster_lesen).",
        "Lecture seule : ce compte SQL ne peut pas modifier le plan.",
        "Sola lettura: questo account SQL non può modificare il piano.");
    public string TxtAddTask => Tr("+ Aufgabe", "+ Task", "+ Tâche", "+ Attività");
    public string TxtEditTask => Tr("Aufgabe bearbeiten", "Edit task", "Modifier tâche", "Modifica attività");
    public string TxtDeleteTask => Tr("Aufgabe löschen", "Delete task", "Supprimer tâche", "Elimina attività");
    public string TxtUndo => Tr("Undo", "Undo", "Annuler", "Annulla");
    public string TxtRedo => Tr("Redo", "Redo", "Rétablir", "Ripristina");
    public string TxtHoliday => Tr("Feiertag", "Holiday", "Jour férié", "Festività");
    public string TxtCompanyVacation => Tr("Ferien eintragen", "Add vacation", "Saisir les vacances", "Inserisci ferie");
    public string TxtPrint => Tr("Drucken", "Print", "Imprimer", "Stampa");
    public string TxtBackup => Tr("Sicherung", "Backup", "Sauvegarde", "Backup");
    public string TxtCopyWeek => Tr("Woche kopieren", "Copy week", "Copier la semaine", "Copia settimana");
    public string TxtStatistics => Tr("Statistik", "Statistics", "Statistiques", "Statistiche");
    public string TxtAbsence => Tr("Ferien", "Vacation", "Vacances", "Ferie");
    public string TxtRestore => Tr("Wiederherstellen", "Restore", "Restaurer", "Ripristina");
    public string TxtDemoSection => Tr("Demo / Test", "Demo / Test", "Démo / Test", "Demo / Test");
    public string TxtSeedDemo => Tr("Feedback-Demo-Daten laden", "Load feedback demo data", "Charger les données démo", "Carica dati demo");
    public string TxtSeedDemoTooltip => Tr("Lädt Vorlagen + Werkstatt-Woche 06.–10.07.2026 (8 Mitarbeiter wie im Testbild)", "Loads templates + workshop week 06–10 Jul 2026 (8 employees as in the test screenshot)", "Charge les modèles + semaine atelier 06–10 juil. 2026", "Carica modelli + settimana officina 06–10 lug 2026");
    public bool ShowDemoTools
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }
    public string TxtImport => Tr("CSV/Excel Import", "CSV/Excel Import", "Import CSV/Excel", "Importa CSV/Excel");
    public string TxtStandardTasks => Tr("Standard-Aufgaben", "Default tasks", "Tâches standard", "Attività standard");
    public string TxtTemplateHint => Tr(
        "★ Favoriten oben. Ziehen auf einen Tag, oder Doppelklick für das Aufgabenfenster. Mehrere mit Shift/Ctrl wählen.",
        "★ Favorites on top. Drag to a day, or double-click to open the task window. Select multiple with Shift/Ctrl.",
        "★ Favoris en haut. Glisser sur un jour, ou double-clic pour la fenêtre de tâche. Sélection multiple avec Maj/Ctrl.",
        "★ Preferiti in alto. Trascina su un giorno, o doppio clic per la finestra attività. Seleziona più voci con Maiusc/Ctrl.");
    public string TxtMoveTaskTooltip => Tr(
        "Ziehen zum Verschieben auf anderen Tag",
        "Drag to move to another day",
        "Glisser pour déplacer vers un autre jour",
        "Trascina per spostare su un altro giorno");
    public string TxtTemplatePanelResizeTooltip => Tr(
        "Ziehen, um die Breite der Standard-Aufgaben zu ändern. Die Breite bleibt nach dem Schließen erhalten.",
        "Drag to change the default-task panel width. The width is kept after closing the app.",
        "Glisser pour modifier la largeur des tâches standard. La largeur est conservée après la fermeture.",
        "Trascina per cambiare la larghezza delle attività standard. La larghezza resta dopo la chiusura.");
    public string TxtTemplateAdd => Tr("+ Vorlage", "+ Template", "+ Modèle", "+ Modello");
    public string TxtTemplateEdit => Tr("Bearbeiten", "Edit", "Modifier", "Modifica");
    public string TxtTemplateDelete => Tr("Löschen", "Delete", "Supprimer", "Elimina");
    public string TxtOptionsButtonsSection => Tr("Button-Sichtbarkeit", "Button visibility", "Visibilité des boutons", "Visibilità pulsanti");
    public string TxtOptionsThemeSection => Tr("Design", "Theme", "Thème", "Tema");
    public string TxtOptionsLanguageSection => Tr("Sprache", "Language", "Langue", "Lingua");
    public string TxtThemeDark => Tr("Dunkel", "Dark", "Sombre", "Scuro");
    public string TxtThemeLight => Tr("Hell", "Light", "Clair", "Chiaro");
    public string TxtNoEmployees => Tr("Noch keine Mitarbeiter vorhanden.", "No employees yet.", "Aucun employé pour le moment.", "Nessun dipendente presente.");
    public string TxtNoEmployeesCta => Tr("CTA: Über \"+ Mitarbeiter\" direkt den ersten Mitarbeiter anlegen.", "CTA: Use \"+ Employee\" to create your first employee.", "CTA: Utilisez \"+ Employé\" pour créer le premier employé.", "CTA: Usa \"+ Dipendente\" per creare il primo dipendente.");
    public string TxtTodayTooltip => Tr("Springt zum aktuellen Tag / aktuellen Woche / aktuellen Monat (je nach Ansicht)", "Jumps to current day / week / month (depending on view)", "Aller au jour / à la semaine / au mois actuel (selon la vue)", "Va al giorno / alla settimana / al mese corrente (in base alla vista)");
    public string TxtDatePickerTooltip => Tr("Datum wählen - springt je nach Ansicht zu Woche/Tag/Monat", "Pick date - jumps to week/day/month based on current view", "Choisir une date - bascule vers semaine/jour/mois selon la vue", "Scegli data - passa a settimana/giorno/mese in base alla vista");
    public string TxtHolidayTooltip => Tr("Feiertag oder freien Tag für alle Mitarbeiter eintragen (Tag oder Zeitraum, inkl. Schweizer Feiertage)", "Add a holiday or day off for all employees (single day or range, including Swiss holidays)", "Ajouter un jour férié ou libre pour tous les employés", "Aggiungi una festività o un giorno libero per tutti i dipendenti");
    public string TxtCompanyVacationTooltip => Tr("Betriebsferien für alle Mitarbeiter auf einmal eintragen", "Add company vacation for all employees at once", "Saisir les vacances d'entreprise pour tous les employés", "Inserisci ferie aziendali per tutti i dipendenti");
    public string TxtBackupTooltip => IsServerMode
        ? Tr(
            "Gemeinsame Daten sichert ihr in SSMS. Optional: lokale Datei dieses PCs speichern – das ist nicht der Server-Stand.",
            "Shared data is backed up in SSMS. Optional: save this PC's local file — that is not the server copy.",
            "Les données partagées se sauvegardent dans SSMS. Option : fichier local de ce PC — ce n'est pas le serveur.",
            "I dati condivisi si salvano in SSMS. Opzionale: file locale di questo PC — non è il server.")
        : Tr("Kopie der lokalen Datenbank als Datei speichern (z. B. auf USB oder Netzlaufwerk)", "Save a copy of the local database as a file (e.g. USB or network drive)", "Enregistrer une copie de la base locale comme fichier", "Salva una copia del database locale come file");
    public string TxtRestoreTooltip => IsServerMode
        ? Tr("Wiederherstellen betrifft nur die lokale Datei, nicht SQL Server. Im Server-Modus nicht vorgesehen.", "Restore only replaces the local file, not SQL Server. Not available in server mode.", "La restauration concerne uniquement le fichier local, pas SQL Server.", "Il ripristino riguarda solo il file locale, non SQL Server.")
        : Tr("Lokale Datenbank durch eine zuvor gespeicherte Sicherungsdatei ersetzen. Die App startet danach neu.", "Replace the local database with a previously saved backup file. The app then restarts.", "Remplacer la base locale par un fichier de sauvegarde. L'application redémarre ensuite.", "Sostituisci il database locale con un file di backup. L'app si riavvia.");
    public string TxtStatisticsTooltip => Tr("Arbeitstage und geschätzte Stunden je Mitarbeiter im gewählten Zeitraum. Ferien und Absenzen zählen nicht.", "Workdays and estimated hours per employee in the selected range. Vacation and absences are excluded.", "Jours travaillés et heures estimées par employé. Vacances et absences exclus.", "Giorni lavorativi e ore stimate per dipendente. Ferie e assenze escluse.");
    public string TxtAbsenceTooltip => Tr("Ferien und Abwesenheiten pro Mitarbeiter, Jahr oder Zeitraum", "Vacation and absences per employee, year or date range", "Vacances et absences par employé, année ou période", "Ferie e assenze per dipendente, anno o intervallo");
    public string TxtFilterTextTooltip => Tr("Freitext in Beschreibung, Ort, Projekt, Notizen", "Free text in description, location, project, notes", "Texte libre dans description, lieu, projet, notes", "Testo libero in descrizione, luogo, progetto, note");
    public string TxtPasteCopiedTaskHere => Tr("Kopierte Aufgabe hier einfügen", "Paste copied task here", "Coller la tâche copiée ici", "Incolla attività copiata qui");
    public string TxtContextEdit => Tr("Bearbeiten", "Edit", "Modifier", "Modifica");
    public string TxtContextDelete => Tr("Löschen", "Delete", "Supprimer", "Elimina");
    public string TxtContextCopy => Tr("Kopieren", "Copy", "Copier", "Copia");
    public string TxtContextAddFavorite => Tr("Zu Favoriten hinzufügen", "Add to favorites", "Ajouter aux favoris", "Aggiungi ai preferiti");
    public string TxtContextAlreadyFavorite => Tr("Bereits in den Favoriten", "Already in favorites", "Déjà dans les favoris", "Già nei preferiti");
    public string TxtTooltipLocation => Tr("Ort", "Location", "Lieu", "Luogo");
    public string TxtTooltipProject => Tr("Projekt", "Project", "Projet", "Progetto");
    public string TxtTooltipTime => Tr("Zeit", "Time", "Heure", "Ora");
    public string TxtCopyGripTooltip => Tr("Ziehen zum Kopieren auf anderen Mitarbeiter/Tag", "Drag to copy to another employee/day", "Glisser pour copier vers un autre employé/jour", "Trascina per copiare su un altro dipendente/giorno");
    public string TxtResizeGripTooltip => Tr(
        "Ziehen, um den Zeitraum zu verlängern oder zu verkürzen",
        "Drag to extend or shorten the date range",
        "Glisser pour prolonger ou raccourcir la période",
        "Trascina per estendere o accorciare il periodo");
    public string TxtResizeStartGripTooltip => Tr(
        "Anfangstag ziehen (früher/später)",
        "Drag start day (earlier/later)",
        "Glisser le jour de début (plus tôt/tard)",
        "Trascina il giorno di inizio (prima/dopo)");
    public string TxtResizeEndGripTooltip => Tr(
        "Endtag ziehen (verlängern/verkürzen)",
        "Drag end day (extend/shorten)",
        "Glisser le jour de fin (prolonger/raccourcir)",
        "Trascina il giorno di fine (estendi/accorcia)");
    public string TxtReorderEmployeeTooltip => Tr("Ziehen zum Umsortieren", "Drag to reorder", "Glisser pour réorganiser", "Trascina per riordinare");

    public bool ShowPrevNavigationButtons
    {
        get => ShowPrevWeekButton && ShowPrevDayButton && ShowPrevMonthButton;
        set
        {
            if (ShowPrevWeekButton == value && ShowPrevDayButton == value && ShowPrevMonthButton == value)
                return;
            ShowPrevWeekButton = value;
            ShowPrevDayButton = value;
            ShowPrevMonthButton = value;
            OnPropertyChanged(nameof(ShowPrevNavigationButtons));
        }
    }

    public bool ShowCurrentNavigationButtons
    {
        get => ShowCurrentDayButton && ShowCurrentMonthButton;
        set
        {
            if (ShowCurrentDayButton == value && ShowCurrentMonthButton == value)
                return;
            ShowCurrentDayButton = value;
            ShowCurrentMonthButton = value;
            OnPropertyChanged(nameof(ShowCurrentNavigationButtons));
        }
    }

    public bool ShowNextNavigationButtons
    {
        get => ShowNextWeekButton && ShowNextDayButton && ShowNextMonthButton;
        set
        {
            if (ShowNextWeekButton == value && ShowNextDayButton == value && ShowNextMonthButton == value)
                return;
            ShowNextWeekButton = value;
            ShowNextDayButton = value;
            ShowNextMonthButton = value;
            OnPropertyChanged(nameof(ShowNextNavigationButtons));
        }
    }

    /// <summary>Standard-Aufgaben (Vorlagen) zum Ziehen auf einen Mitarbeiter-Tag. Bearbeitbar und persistent.</summary>
    public ObservableCollection<TaskTemplate> DefaultTasks { get; }

    private async Task SaveTemplatesToFileAsync()
    {
        await _templateStorage.SaveAsync(DefaultTasks);
    }

    /// <summary>Header-Text für die Wochenansicht (z. B. "Arbeitsprogramm 2025 KW 5").</summary>
    public string WeekHeaderText => $"{Tr("Arbeitsprogramm", "Work schedule", "Programme de travail", "Programma di lavoro")} {CurrentWeek.Year} KW {CurrentWeek.WeekNumber}";

    partial void OnCurrentWeekChanged(WeekInfo value) => OnPropertyChanged(nameof(WeekHeaderText));

    /// <summary>Bei Datumsauswahl: Woche/Monat anpassen. Reload nur wenn der sichtbare Zeitraum wechselt.</summary>
    partial void OnSelectedDateChanged(DateTime value)
    {
        if (_suppressSelectedDateSideEffects)
            return;

        var previousWeek = CurrentWeek;
        var previousMonth = CurrentMonthStart;
        var newWeek = new WeekInfo(value);
        var newMonth = new DateTime(value.Year, value.Month, 1);

        var weekChanged = previousWeek.Year != newWeek.Year || previousWeek.WeekNumber != newWeek.WeekNumber;
        var monthChanged = previousMonth.Year != newMonth.Year || previousMonth.Month != newMonth.Month;

        if (weekChanged)
            CurrentWeek = newWeek;
        if (monthChanged)
            CurrentMonthStart = newMonth;

        var needsReload = CurrentViewMode switch
        {
            ScheduleViewMode.Day => true,
            ScheduleViewMode.Week => weekChanged,
            ScheduleViewMode.Month => monthChanged,
            _ => true
        };

        if (needsReload)
            _ = LoadTasksForCurrentViewAsync();
    }

    public MainViewModel(
        ScheduleService scheduleService,
        ILogger<MainViewModel> logger,
        Presentation.Services.ExportService exportService,
        Presentation.Services.TaskTemplateStorage templateStorage,
        Presentation.Services.UpdateCheckService updateCheckService,
        Presentation.Services.DemoDataService demoDataService,
        IDatabaseInfo databaseOptions,
        ScheduleBackupService backupService)
    {
        _scheduleService = scheduleService;
        _logger = logger;
        _exportService = exportService;
        _templateStorage = templateStorage;
        _updateCheckService = updateCheckService;
        _demoDataService = demoDataService;
        _databaseOptions = databaseOptions;
        _backupService = backupService;
        _currentWeek = new WeekInfo(DateTime.Now);
        DefaultTasks = new ObservableCollection<TaskTemplate>();
        var updateSettings = _updateCheckService.LoadSettings();
        IsAutoUpdateEnabled = updateSettings.Enabled;
        ApplySavedLanguage();
        _autoReloadTimer.Tick += AutoReloadTimerOnTick;
        ApplyUiLayout();
        PropertyChanged += OnLayoutPropertyChanged;
        UpdateAutoReloadTimer();

        if (App.JumpToDemoWeek)
        {
            SelectedDate = Presentation.Services.DemoDataSeeder.DemoWeekMonday;
            CurrentWeek = new WeekInfo(SelectedDate);
            CurrentMonthStart = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
        }
    }

    /// <summary>True während LoadData/Wochenwechsel – UI macht dann einen Full-Rebuild.</summary>
    public bool IsScheduleDataReload { get; private set; }

    /// <summary>True bei Wochen-/Monatsnavigation – UI aktualisiert Inhalte in-place.</summary>
    public bool PreferInPlaceScheduleUpdate { get; private set; }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        // Explizite Reloads (Mitarbeiter add/edit/delete/reorder) müssen Employees neu laden.
        await LoadDataAsync(reloadEmployees: true);
    }

    private async Task LoadDataAsync(bool reloadEmployees)
    {
        await _loadGate.WaitAsync();
        try
        {
            IsScheduleDataReload = true;
            await ReloadTemplatesPreservingSelectionAsync();
            if (reloadEmployees || Employees.Count == 0 || _allEmployees.Count == 0)
            {
                // DisplayOrder aus der DB laden – keine automatische Umsortierung beim Start,
                // damit manuelle Reihenfolge erhalten bleibt.
                _allEmployees = await _scheduleService.GetAllEmployeesAsync();
            }

            Employees = ShowInactiveEmployees
                ? _allEmployees
                : _allEmployees.Where(e => e.IsActive).ToList();

            if (CurrentViewMode == ScheduleViewMode.Month)
            {
                var monthEnd = CurrentMonthStart.AddMonths(1).AddDays(-1);
                Tasks = await _scheduleService.GetTasksForDateRangeAsync(CurrentMonthStart, monthEnd);
            }
            else if (CurrentViewMode == ScheduleViewMode.Week)
            {
                Tasks = await _scheduleService.GetTasksForWeekAsync(CurrentWeek);
            }
            else
            {
                Tasks = await _scheduleService.GetTasksForDateRangeAsync(SelectedDate.Date, SelectedDate.Date);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Daten");
            if (_databaseOptions.IsServer)
            {
                MessageBox.Show(
                    $"{Tr("Daten konnten nicht vom Server geladen werden", "Could not load data from the server", "Impossible de charger les données du serveur", "Impossibile caricare i dati dal server")}:\n{ex.Message}",
                    TxtDatabaseSettings,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        finally
        {
            IsScheduleDataReload = false;
            _loadGate.Release();
        }
    }

    /// <summary>Nur Aufgaben für die aktuelle Ansicht laden (kein Mitarbeiter-Reload).</summary>
    private Task LoadTasksForCurrentViewAsync() => LoadDataAsync(reloadEmployees: false);

    /// <summary>Nach lokalem Task-Update: nur betroffene Mitarbeiterzeilen neu zeichnen.</summary>
    public event EventHandler<IReadOnlyList<int>>? EmployeeScheduleChanged;

    private (DateTime Start, DateTime End) GetVisibleDateRange() =>
        CurrentViewMode switch
        {
            ScheduleViewMode.Day => (SelectedDate.Date, SelectedDate.Date),
            ScheduleViewMode.Month => (CurrentMonthStart.Date, CurrentMonthStart.AddMonths(1).AddDays(-1).Date),
            _ => (CurrentWeek.StartDate.Date, CurrentWeek.EndDate.Date)
        };

    private bool IsVisibleInCurrentView(WorkTask task)
    {
        var (viewStart, viewEnd) = GetVisibleDateRange();
        var start = task.StartDate.Date;
        var end = (task.EndDate ?? task.StartDate).Date;
        return start <= viewEnd && end >= viewStart;
    }

    /// <summary>Aktualisiert die lokale Aufgabenliste ohne DB-Reload (schneller UI-Pfad).</summary>
    private void SyncTaskInMemory(WorkTask task, int? previousEmployeeId = null)
    {
        AppendTasksInMemory(new[] { task }, previousEmployeeId);
    }

    private void AppendTasksInMemory(IReadOnlyList<WorkTask> tasks, int? previousEmployeeId = null)
    {
        if (tasks.Count == 0)
            return;

        var list = Tasks.ToList();
        var affected = new HashSet<int>();
        if (previousEmployeeId is int prevEmp)
            affected.Add(prevEmp);

        var changed = false;
        foreach (var task in tasks)
        {
            affected.Add(task.EmployeeId);
            var idx = list.FindIndex(t => t.Id == task.Id);
            if (!IsVisibleInCurrentView(task))
            {
                if (idx >= 0)
                {
                    list.RemoveAt(idx);
                    changed = true;
                }
                continue;
            }

            if (idx >= 0)
                list[idx] = task;
            else
                list.Add(task);
            changed = true;
        }

        if (!changed)
            return;

        // Property setzen (kein Full-Rebuild – ScheduleView ignoriert Tasks und nutzt EmployeeScheduleChanged).
        Tasks = list;
        EmployeeScheduleChanged?.Invoke(this, affected.ToList());
    }

    private void RemoveTaskFromMemory(int taskId, int? employeeId = null)
    {
        var existing = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (existing == null)
            return;

        Tasks = Tasks.Where(t => t.Id != taskId).ToList();
        EmployeeScheduleChanged?.Invoke(this, new[] { employeeId ?? existing.EmployeeId });
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoAsync()
    {
        if (!_history.CanUndo)
            return;

        await _history.UndoAsync();
        NotifyHistoryStateChanged();
        await LoadDataAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private async Task RedoAsync()
    {
        if (!_history.CanRedo)
            return;

        await _history.RedoAsync();
        NotifyHistoryStateChanged();
        await LoadDataAsync();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        FilterText = string.Empty;
        FilterEmployee = null;
        FilterTaskType = null;
    }

    [RelayCommand]
    private void ToggleAutoUpdate()
    {
        var settings = _updateCheckService.LoadSettings();
        settings.Enabled = !settings.Enabled;
        _updateCheckService.SaveSettings(settings);
        IsAutoUpdateEnabled = settings.Enabled;

        MessageBox.Show(
            settings.Enabled
                ? Tr(
                    "Auto-Update-Prüfung wurde aktiviert.\nBitte bei Bedarf ReleaseApiUrl in update_settings.json eintragen.",
                    "Auto update check has been enabled.\nSet ReleaseApiUrl in update_settings.json if needed.",
                    "La vérification automatique des mises à jour a été activée.\nRenseignez ReleaseApiUrl dans update_settings.json si nécessaire.",
                    "Il controllo automatico aggiornamenti è stato attivato.\nImposta ReleaseApiUrl in update_settings.json se necessario.")
                : Tr(
                    "Auto-Update-Prüfung wurde deaktiviert.",
                    "Auto update check has been disabled.",
                    "La vérification automatique des mises à jour a été désactivée.",
                    "Il controllo automatico aggiornamenti è stato disattivato."),
            Tr("Update-Einstellungen", "Update settings", "Paramètres de mise à jour", "Impostazioni aggiornamento"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        var currentVersion = typeof(MainViewModel).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);
        var result = await _updateCheckService.CheckAsync(currentVersion);
        if (!result.IsEnabled || !result.IsConfigured)
        {
            MessageBox.Show(result.Message, Tr("Update", "Update", "Mise à jour", "Aggiornamento"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (result.HasUpdate)
        {
            var message = $"{result.Message}\n\n{Tr("Soll die Download-Seite geöffnet werden?", "Open download page now?", "Ouvrir la page de téléchargement maintenant ?", "Aprire ora la pagina di download?")}";
            var open = MessageBox.Show(message, Tr("Update verfügbar", "Update available", "Mise à jour disponible", "Aggiornamento disponibile"), MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(result.Url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(result.Url) { UseShellExecute = true });
            }
            return;
        }

        MessageBox.Show(result.Message, Tr("Update", "Update", "Mise à jour", "Aggiornamento"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool _suppressSelectedDateSideEffects;

    [RelayCommand]
    private async Task PreviousWeekAsync()
    {
        PreferInPlaceScheduleUpdate = true;
        try
        {
            CurrentWeek = CurrentWeek.GetPreviousWeek();
            _suppressSelectedDateSideEffects = true;
            try { SelectedDate = CurrentWeek.StartDate; }
            finally { _suppressSelectedDateSideEffects = false; }
            await LoadTasksForCurrentViewAsync();
        }
        finally
        {
            PreferInPlaceScheduleUpdate = false;
        }
    }

    [RelayCommand]
    private async Task NextWeekAsync()
    {
        PreferInPlaceScheduleUpdate = true;
        try
        {
            CurrentWeek = CurrentWeek.GetNextWeek();
            _suppressSelectedDateSideEffects = true;
            try { SelectedDate = CurrentWeek.StartDate; }
            finally { _suppressSelectedDateSideEffects = false; }
            await LoadTasksForCurrentViewAsync();
        }
        finally
        {
            PreferInPlaceScheduleUpdate = false;
        }
    }

    [RelayCommand]
    private async Task CurrentWeekCommandAsync()
    {
        CurrentViewMode = ScheduleViewMode.Week;
        CurrentWeek = new WeekInfo(DateTime.Now);
        SelectedDate = CurrentWeek.StartDate;
        await LoadTasksForCurrentViewAsync();
        OnPropertyChanged(nameof(WeekHeaderText));
    }

    [RelayCommand]
    private async Task SwitchToWeekViewAsync()
    {
        CurrentViewMode = ScheduleViewMode.Week;
        CurrentWeek = new WeekInfo(DateTime.Now);
        SelectedDate = DateTime.Today;
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task SwitchToDayViewAsync()
    {
        CurrentViewMode = ScheduleViewMode.Day;
        SelectedDate = DateTime.Now;
        CurrentWeek = new WeekInfo(SelectedDate);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task CurrentDayCommandAsync()
    {
        CurrentViewMode = ScheduleViewMode.Day;
        SelectedDate = DateTime.Now.Date;
        CurrentWeek = new WeekInfo(SelectedDate);
        await LoadDataAsync();
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(CurrentWeek));
        OnPropertyChanged(nameof(WeekHeaderText));
    }

    [RelayCommand]
    private async Task CurrentMonthCommandAsync()
    {
        CurrentViewMode = ScheduleViewMode.Month;
        CurrentMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        await LoadDataAsync();
        OnPropertyChanged(nameof(CurrentMonthStart));
        OnPropertyChanged(nameof(WeekHeaderText));
    }

    /// <summary>Springt zum aktuellen Tag/Woche/Monat (je nach Ansicht). Immer sichtbar.</summary>
    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        var now = DateTime.Today;
        SelectedDate = now;
        CurrentWeek = new WeekInfo(now);
        CurrentMonthStart = new DateTime(now.Year, now.Month, 1);
        await LoadDataAsync();
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(CurrentWeek));
        OnPropertyChanged(nameof(CurrentMonthStart));
        OnPropertyChanged(nameof(WeekHeaderText));
    }

    [RelayCommand]
    private async Task SwitchToMonthViewAsync()
    {
        CurrentViewMode = ScheduleViewMode.Month;
        CurrentMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        await LoadDataAsync();
    }

    [RelayCommand]
    private void PreviousDay()
    {
        SelectedDate = SelectedDate.AddDays(-1);
    }

    [RelayCommand]
    private void NextDay()
    {
        SelectedDate = SelectedDate.AddDays(1);
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        PreferInPlaceScheduleUpdate = true;
        try
        {
            CurrentMonthStart = CurrentMonthStart.AddMonths(-1);
            await LoadTasksForCurrentViewAsync();
        }
        finally
        {
            PreferInPlaceScheduleUpdate = false;
        }
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        PreferInPlaceScheduleUpdate = true;
        try
        {
            CurrentMonthStart = CurrentMonthStart.AddMonths(1);
            await LoadTasksForCurrentViewAsync();
        }
        finally
        {
            PreferInPlaceScheduleUpdate = false;
        }
    }

    public List<DateTime> GetDisplayDays()
    {
        if (CurrentViewMode == ScheduleViewMode.Day)
            return new List<DateTime> { SelectedDate.Date };
        if (CurrentViewMode == ScheduleViewMode.Month)
        {
            var days = new List<DateTime>();
            var monthEnd = CurrentMonthStart.AddMonths(1).AddDays(-1);
            for (var d = CurrentMonthStart; d <= monthEnd; d = d.AddDays(1))
                days.Add(d);
            return days;
        }
        return CurrentWeek.GetDays(includeWeekend: ShowWeekend);
    }

    public string? GetHolidayLabel(DateTime day) =>
        SwissPublicHolidays.ForYear(day.Year).FirstOrDefault(h => h.Date.Date == day.Date)?.NameDe;

    /// <summary>Tasks für einen Mitarbeiter an einem Tag (wie Kalender). Nutzt FilteredTasks.</summary>
    public List<WorkTask> GetTasksForEmployeeAndDay(Employee employee, DateTime day)
    {
        return FilteredTasks
            .Where(t => t.EmployeeId == employee.Id &&
                       t.StartDate.Date <= day.Date &&
                       (t.EndDate == null ? t.StartDate.Date == day.Date : t.EndDate.Value.Date >= day.Date))
            .OrderBy(t => t.StartTime ?? TimeSpan.Zero)
            .ToList();
    }

    [RelayCommand]
    private async Task AddEmployeeAsync()
    {
        if (!EnsureCanWrite())
            return;
        try
        {
            var dialog = new EmployeeDialog();
            dialog.SetViewModel(new EmployeeDialogViewModel());
            dialog.Owner = System.Windows.Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                var employee = dialog.ViewModel.GetEmployee();
                await _scheduleService.CreateEmployeeAsync(employee);
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Hinzufügen des Mitarbeiters");
            var msg = ex.InnerException != null ? $"{ex.Message}{Environment.NewLine}{Environment.NewLine}Details: {ex.InnerException.Message}" : ex.Message;
            MessageBox.Show(
                $"{Tr("Fehler beim Hinzufügen des Mitarbeiters", "Error while adding employee", "Erreur lors de l'ajout de l'employé", "Errore durante l'aggiunta del dipendente")}: {msg}",
                Tr("Fehler", "Error", "Erreur", "Errore"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task EditEmployeeAsync()
    {
        if (SelectedEmployee == null)
        {
            MessageBox.Show(
                Tr("Bitte wählen Sie zuerst einen Mitarbeiter in der Tabelle aus (Klick auf den Namen).", "Please select an employee in the table first (click the name).", "Veuillez d'abord sélectionner un employé dans le tableau (cliquez sur le nom).", "Seleziona prima un dipendente nella tabella (clic sul nome)."),
                Tr("Kein Mitarbeiter ausgewählt", "No employee selected", "Aucun employé sélectionné", "Nessun dipendente selezionato"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var dialog = new EmployeeDialog();
            dialog.SetViewModel(new EmployeeDialogViewModel(SelectedEmployee));
            dialog.Owner = System.Windows.Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                var employee = dialog.ViewModel.GetEmployee();
                await _scheduleService.UpdateEmployeeAsync(employee);
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            ShowTaskOperationError(ex, "Fehler beim Bearbeiten des Mitarbeiters");
        }
    }

    [RelayCommand]
    private async Task DeleteEmployeeAsync()
    {
        if (SelectedEmployee == null)
        {
            MessageBox.Show(
                Tr("Bitte wählen Sie zuerst einen Mitarbeiter in der Tabelle aus (Klick auf den Namen).", "Please select an employee in the table first (click the name).", "Veuillez d'abord sélectionner un employé dans le tableau (cliquez sur le nom).", "Seleziona prima un dipendente nella tabella (clic sul nome)."),
                Tr("Kein Mitarbeiter ausgewählt", "No employee selected", "Aucun employé sélectionné", "Nessun dipendente selezionato"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!EnsureCanWrite())
            return;

        var hiding = SelectedEmployee.IsActive;
        var result = MessageBox.Show(
            hiding
                ? string.Format(
                    Tr("Mitarbeiter '{0}' ausblenden? Die Aufgaben bleiben gespeichert.", "Hide employee '{0}'? Tasks stay in the database.", "Masquer l'employé '{0}' ? Les tâches restent enregistrées.", "Nascondere il dipendente '{0}'? Le attività restano salvate."),
                    SelectedEmployee.FullName)
                : string.Format(
                    Tr("Mitarbeiter '{0}' wieder einblenden?", "Show employee '{0}' again?", "Réafficher l'employé '{0}' ?", "Mostrare di nuovo il dipendente '{0}'?"),
                    SelectedEmployee.FullName),
            TxtDeleteEmployee,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                SelectedEmployee.IsActive = !hiding;
                await _scheduleService.UpdateEmployeeAsync(SelectedEmployee);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Löschen des Mitarbeiters");
                MessageBox.Show(
                    $"{Tr("Fehler beim Löschen des Mitarbeiters", "Error while deleting employee", "Erreur lors de la suppression de l'employé", "Errore durante l'eliminazione del dipendente")}: {ex.Message}",
                    Tr("Fehler", "Error", "Erreur", "Errore"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private Task AddTaskAsync() => OpenNewTaskDialogAsync();

    public async Task OpenNewTaskDialogAsync(
        Employee? employee = null,
        DateTime? date = null,
        TaskTemplate? template = null)
    {
        if (!EnsureCanWrite())
            return;
        if (Employees.Count == 0)
        {
            MessageBox.Show(
                Tr("Bitte legen Sie zuerst mindestens einen Mitarbeiter an.", "Please create at least one employee first.", "Veuillez d'abord créer au moins un employé.", "Crea prima almeno un dipendente."),
                Tr("Keine Mitarbeiter", "No employees", "Aucun employé", "Nessun dipendente"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        try
        {
            var dialog = new TaskDialog();
            var vm = new TaskDialogViewModel(Employees, null, CurrentWeek);
            if (employee != null)
                vm.SelectedEmployee = employee;
            else if (SelectedEmployee != null)
                vm.SelectedEmployee = SelectedEmployee;
            if (date.HasValue)
                vm.StartDate = date.Value.Date;
            else if (SelectedDate != default)
                vm.StartDate = SelectedDate.Date;
            if (template != null)
            {
                vm.Description = template.Description;
                vm.TaskType = template.Type;
                vm.TaskColor = template.Color;
            }
            dialog.SetViewModel(vm);
            dialog.Owner = System.Windows.Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                var tasksToCreate = dialog.ViewModel.GetTasksToCreate()
                    .Select(CloneTaskWithoutId)
                    .ToList();
                var createdIds = new List<int>();
                var createdTasks = new List<WorkTask>();
                foreach (var taskToCreate in tasksToCreate)
                {
                    var created = await _scheduleService.CreateTaskAsync(taskToCreate);
                    createdIds.Add(created.Id);
                    createdTasks.Add(created);
                }

                if (createdIds.Count > 0)
                {
                    PushHistory(
                        undoAsync: async () =>
                        {
                            foreach (var id in createdIds.ToList())
                                await _scheduleService.DeleteTaskAsync(id);
                        },
                        redoAsync: async () =>
                        {
                            createdIds.Clear();
                            foreach (var blueprint in tasksToCreate.Select(CloneTaskWithoutId))
                            {
                                var recreated = await _scheduleService.CreateTaskAsync(blueprint);
                                createdIds.Add(recreated.Id);
                            }
                        });
                }

                AppendTasksInMemory(createdTasks);
            }
        }
        catch (Exception ex)
        {
            ShowTaskOperationError(ex, "Fehler beim Hinzufügen der Aufgabe");
        }
    }

    [RelayCommand]
    private async Task EditTaskAsync()
    {
        if (SelectedTask == null)
        {
            MessageBox.Show(
                Tr("Bitte wählen Sie zuerst eine Aufgabe in der Tabelle aus (Klick auf die Aufgabe).", "Please select a task in the table first (click the task).", "Veuillez d'abord sélectionner une tâche dans le tableau (cliquez sur la tâche).", "Seleziona prima un'attività nella tabella (clic sull'attività)."),
                Tr("Keine Aufgabe ausgewählt", "No task selected", "Aucune tâche sélectionnée", "Nessuna attività selezionata"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var before = CloneTask(SelectedTask);
            var dialog = new TaskDialog();
            dialog.SetViewModel(new TaskDialogViewModel(Employees, SelectedTask));
            dialog.Owner = System.Windows.Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                if (!EnsureCanWrite())
                    return;
                var task = dialog.ViewModel.GetTask();
                var after = CloneTask(task);
                var scope = dialog.ViewModel.SeriesEditScope;
                if (task.RecurrenceGroupId.HasValue && scope != SeriesEditScope.ThisOnly)
                {
                    DateTime? from = scope == SeriesEditScope.ThisAndFuture ? task.StartDate : null;
                    await _scheduleService.UpdateSeriesAsync(task, from);
                    await LoadDataAsync();
                }
                else
                {
                    await _scheduleService.UpdateTaskAsync(task);
                    PushHistory(
                        undoAsync: async () => await _scheduleService.UpdateTaskAsync(CloneTask(before)),
                        redoAsync: async () => await _scheduleService.UpdateTaskAsync(CloneTask(after)));
                    SelectedTask = task;
                    SyncTaskInMemory(task);
                }
            }
        }
        catch (Exception ex)
        {
            ShowTaskOperationError(ex, "Fehler beim Bearbeiten der Aufgabe");
        }
    }

    [RelayCommand]
    private async Task DeleteTaskAsync()
    {
        if (SelectedTask == null)
        {
            MessageBox.Show(
                Tr("Bitte wählen Sie zuerst eine Aufgabe in der Tabelle aus (Klick auf die Aufgabe).", "Please select a task in the table first (click the task).", "Veuillez d'abord sélectionner une tâche dans le tableau (cliquez sur la tâche).", "Seleziona prima un'attività nella tabella (clic sull'attività)."),
                Tr("Keine Aufgabe ausgewählt", "No task selected", "Aucune tâche sélectionnée", "Nessuna attività selezionata"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            if (_selectedTaskDays.Count > 0 && (SelectedTask.EndDate ?? SelectedTask.StartDate).Date > SelectedTask.StartDate.Date)
            {
                var rangeStart = SelectedTask.StartDate.Date.AddDays(-1);
                var rangeEnd = (SelectedTask.EndDate ?? SelectedTask.StartDate).Date.AddDays(1);
                var beforeState = await SnapshotEmployeeTasksAsync(SelectedTask.EmployeeId, rangeStart, rangeEnd);
                await DeleteSelectedDaysFromTaskAsync(SelectedTask, _selectedTaskDays);
                var afterState = await SnapshotEmployeeTasksAsync(SelectedTask.EmployeeId, rangeStart, rangeEnd);
                PushHistory(
                    undoAsync: async () => await ReplaceEmployeeTasksInRangeAsync(SelectedTask.EmployeeId, rangeStart, rangeEnd, beforeState),
                    redoAsync: async () => await ReplaceEmployeeTasksInRangeAsync(SelectedTask.EmployeeId, rangeStart, rangeEnd, afterState));
                _selectedTaskDays.Clear();
                await LoadDataAsync();
                return;
            }

            if (!EnsureCanWrite())
                return;

            var isSeries = SelectedTask.RecurrenceGroupId.HasValue;
            var result = MessageBox.Show(
                isSeries
                    ? string.Format(
                        Tr("Aufgabe '{0}' gehört zu einer Serie.\nJa = nur diese, Nein = ganze Serie, Abbrechen = nichts.", "Task '{0}' is part of a series.\nYes = this only, No = entire series, Cancel = nothing.", "La tâche '{0}' fait partie d'une série.\nOui = celle-ci, Non = toute la série, Annuler = rien.", "L'attività '{0}' fa parte di una serie.\nSì = solo questa, No = tutta la serie, Annulla = niente."),
                        SelectedTask.Description)
                    : string.Format(
                        Tr("Möchten Sie die Aufgabe '{0}' wirklich löschen?", "Do you really want to delete task '{0}'?", "Voulez-vous vraiment supprimer la tâche '{0}' ?", "Vuoi davvero eliminare l'attività '{0}'?"),
                        SelectedTask.Description),
                Tr("Aufgabe löschen", "Delete task", "Supprimer la tâche", "Elimina attività"),
                isSeries ? MessageBoxButton.YesNoCancel : MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return;

            if (isSeries && result == MessageBoxResult.No && SelectedTask.RecurrenceGroupId is Guid groupId)
            {
                await _scheduleService.DeleteSeriesAsync(groupId, fromDateInclusive: null);
                SelectedTask = null;
                await LoadDataAsync();
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                var deletedSnapshot = CloneTask(SelectedTask);
                var currentId = SelectedTask.Id;
                await _scheduleService.DeleteTaskAsync(currentId);
                PushHistory(
                    undoAsync: async () =>
                    {
                        var recreated = await _scheduleService.CreateTaskAsync(CloneTaskWithoutId(deletedSnapshot));
                        currentId = recreated.Id;
                    },
                    redoAsync: async () => await _scheduleService.DeleteTaskAsync(currentId));
                SelectedTask = null;
                RemoveTaskFromMemory(deletedSnapshot.Id, deletedSnapshot.EmployeeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Löschen der Aufgabe");
            MessageBox.Show(
                $"{Tr("Fehler beim Löschen der Aufgabe", "Error while deleting task", "Erreur lors de la suppression de la tâche", "Errore durante l'eliminazione dell'attività")}: {ex.Message}",
                Tr("Fehler", "Error", "Erreur", "Errore"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CopySelectedTask()
    {
        if (SelectedTask == null)
        {
            MessageBox.Show(
                Tr("Bitte zuerst eine Aufgabe auswählen.", "Please select a task first.", "Veuillez d'abord sélectionner une tâche.", "Seleziona prima un'attività."),
                Tr("Keine Aufgabe ausgewählt", "No task selected", "Aucune tâche sélectionnée", "Nessuna attività selezionata"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        var source = SelectedTask;
        var sourceStart = source.StartDate.Date;
        var sourceEnd = (source.EndDate ?? source.StartDate).Date;
        var segments = CopiedTaskPlanner.BuildSegments(sourceStart, sourceEnd, _selectedTaskDays);
        _copiedTask = CopiedTaskPayload.From(source, segments);
        OnPropertyChanged(nameof(HasCopiedTask));
    }

    [RelayCommand]
    private async Task PasteCopiedTaskAtCurrentDateAsync()
    {
        if (_copiedTask == null)
        {
            MessageBox.Show(
                Tr("Es wurde noch keine Aufgabe kopiert.", "No task has been copied yet.", "Aucune tâche n'a encore été copiée.", "Nessuna attività è stata ancora copiata."),
                Tr("Hinweis", "Note", "Information", "Avviso"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        var targetEmployee = SelectedEmployee;
        if (targetEmployee == null && SelectedTask != null)
            targetEmployee = Employees.FirstOrDefault(e => e.Id == SelectedTask.EmployeeId);

        if (targetEmployee == null)
        {
            MessageBox.Show(
                Tr("Bitte zuerst eine Zielzelle oder einen Mitarbeiter wählen.", "Please select a target cell or an employee first.", "Veuillez d'abord sélectionner une cellule cible ou un employé.", "Seleziona prima una cella di destinazione o un dipendente."),
                Tr("Hinweis", "Note", "Information", "Avviso"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        await PasteCopiedTaskAsync(targetEmployee, SelectedDate.Date);
    }

    public async Task CopyTaskToAsync(WorkTask sourceTask, Employee targetEmployee, DateTime targetDay)
    {
        try
        {
            var sourceStart = sourceTask.StartDate.Date;
            var sourceEnd = (sourceTask.EndDate ?? sourceTask.StartDate).Date;
            var durationDays = Math.Max(0, (sourceEnd - sourceStart).Days);
            var newTask = new WorkTask
            {
                EmployeeId = targetEmployee.Id,
                Description = sourceTask.Description,
                StartDate = targetDay.Date,
                EndDate = durationDays > 0 ? targetDay.Date.AddDays(durationDays) : null,
                StartTime = sourceTask.StartTime,
                EndTime = sourceTask.EndTime,
                Location = sourceTask.Location,
                Project = sourceTask.Project,
                Notes = sourceTask.Notes,
                Type = sourceTask.Type,
                Color = sourceTask.Color
            };

            var created = await _scheduleService.CreateTaskAsync(newTask);
            var currentId = created.Id;
            var blueprint = CloneTaskWithoutId(newTask);
            PushHistory(
                undoAsync: async () => await _scheduleService.DeleteTaskAsync(currentId),
                redoAsync: async () =>
                {
                    var recreated = await _scheduleService.CreateTaskAsync(CloneTaskWithoutId(blueprint));
                    currentId = recreated.Id;
                });
            SyncTaskInMemory(created);
        }
        catch (Exception ex)
        {
            ShowTaskOperationError(ex, "Fehler beim Kopieren per Drag&Drop");
        }
    }

    /// <summary>Mitarbeiter-Reihenfolge per Drag &amp; Drop aktualisieren.</summary>
    public async Task ReorderEmployeesAsync(IReadOnlyList<Employee> orderedEmployees)
    {
        try
        {
            var ids = orderedEmployees.Select(e => e.Id).ToList();
            await _scheduleService.ReorderEmployeesAsync(ids);
            EmployeeSortMode = EmployeeSortMode.Custom;
            NotifyEmployeeSortUiChanged();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Sortieren der Mitarbeiter");
            MessageBox.Show($"{Tr("Fehler", "Error", "Erreur", "Errore")}: {ex.Message}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Mitarbeiter-Spalte per Header-Klick zwischen Nummer und Name umschalten.</summary>
    [RelayCommand]
    public async Task CycleEmployeeSortAsync()
    {
        try
        {
            if (EmployeeSortMode == EmployeeSortMode.Number)
            {
                await _scheduleService.NormalizeEmployeeOrderByNameAsync();
                EmployeeSortMode = EmployeeSortMode.Name;
            }
            else
            {
                // Custom oder Name → nach Nummer
                await _scheduleService.NormalizeEmployeeOrderByNumberAsync();
                EmployeeSortMode = EmployeeSortMode.Number;
            }

            NotifyEmployeeSortUiChanged();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Umschalten der Mitarbeiter-Sortierung");
            MessageBox.Show($"{Tr("Fehler", "Error", "Erreur", "Errore")}: {ex.Message}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void NotifyEmployeeSortUiChanged()
    {
        OnPropertyChanged(nameof(EmployeeSortMode));
        OnPropertyChanged(nameof(TxtEmployeeSortHint));
        OnPropertyChanged(nameof(TxtEmployeeSortLabel));
        OnPropertyChanged(nameof(TxtEmployeeSortTooltip));
    }

    /// <summary>Erstellt Aufgaben aus mehreren Vorlagen für denselben Tag.</summary>
    public async Task CreateTasksFromTemplatesAsync(Employee employee, DateTime day, IReadOnlyList<TaskTemplate> templates)
    {
        if (templates.Count == 0)
            return;

        var created = new List<WorkTask>();
        foreach (var template in templates)
        {
            var task = await TryCreateTaskFromTemplateAsync(employee, day, template);
            if (task != null)
                created.Add(task);
        }

        AppendTasksInMemory(created);
    }

    /// <summary>Erstellt eine Aufgabe aus einer Vorlage für den angegebenen Mitarbeiter und Tag.</summary>
    public async Task CreateTaskFromTemplateAsync(Employee employee, DateTime day, TaskTemplate template, bool reload = true)
    {
        var created = await TryCreateTaskFromTemplateAsync(employee, day, template);
        if (created != null)
            SyncTaskInMemory(created);
    }

    private async Task<WorkTask?> TryCreateTaskFromTemplateAsync(Employee employee, DateTime day, TaskTemplate template)
    {
        try
        {
            var blueprint = new WorkTask
            {
                EmployeeId = employee.Id,
                Description = template.Description,
                StartDate = day.Date,
                Type = template.Type,
                Color = template.Color
            };
            var created = await _scheduleService.CreateTaskAsync(blueprint);
            var currentId = created.Id;
            var templateCopy = CloneTaskWithoutId(blueprint);
            PushHistory(
                undoAsync: async () => await _scheduleService.DeleteTaskAsync(currentId),
                redoAsync: async () =>
                {
                    var recreated = await _scheduleService.CreateTaskAsync(CloneTaskWithoutId(templateCopy));
                    currentId = recreated.Id;
                });
            return created;
        }
        catch (Exception ex)
        {
            ShowTaskOperationError(ex, "Fehler beim Anlegen der Aufgabe aus Vorlage");
            return null;
        }
    }

    /// <summary>Erstellt mehrtägige Aufgaben aus allen ausgewählten Vorlagen.</summary>
    public async Task CreateTasksFromTemplateRangeAsync(
        Employee employee,
        DateTime startDay,
        DateTime endDay,
        IReadOnlyList<TaskTemplate> templates)
    {
        if (templates.Count == 0)
            return;

        var created = new List<WorkTask>();
        foreach (var template in templates)
        {
            var task = await TryCreateTaskFromTemplateRangeAsync(employee, startDay, endDay, template);
            if (task != null)
                created.Add(task);
        }

        AppendTasksInMemory(created);
    }

    /// <summary>Erstellt eine mehrtägige Aufgabe aus Vorlage (Start/Ende inkl.).</summary>
    public async Task CreateTaskFromTemplateRangeAsync(
        Employee employee,
        DateTime startDay,
        DateTime endDay,
        TaskTemplate template,
        bool reload = true)
    {
        var created = await TryCreateTaskFromTemplateRangeAsync(employee, startDay, endDay, template);
        if (created != null)
            SyncTaskInMemory(created);
    }

    private async Task<WorkTask?> TryCreateTaskFromTemplateRangeAsync(
        Employee employee,
        DateTime startDay,
        DateTime endDay,
        TaskTemplate template)
    {
        try
        {
            var start = startDay.Date <= endDay.Date ? startDay.Date : endDay.Date;
            var end = endDay.Date >= startDay.Date ? endDay.Date : startDay.Date;

            var blueprint = new WorkTask
            {
                EmployeeId = employee.Id,
                Description = template.Description,
                StartDate = start,
                EndDate = end > start ? end : null,
                Type = template.Type,
                Color = template.Color
            };
            var created = await _scheduleService.CreateTaskAsync(blueprint);
            var currentId = created.Id;
            var templateCopy = CloneTaskWithoutId(blueprint);
            PushHistory(
                undoAsync: async () => await _scheduleService.DeleteTaskAsync(currentId),
                redoAsync: async () =>
                {
                    var recreated = await _scheduleService.CreateTaskAsync(CloneTaskWithoutId(templateCopy));
                    currentId = recreated.Id;
                });
            return created;
        }
        catch (Exception ex)
        {
            ShowTaskOperationError(ex, "Fehler beim Anlegen der mehrtägigen Aufgabe aus Vorlage");
            return null;
        }
    }

    public bool IsTaskDaySelected(WorkTask task, DateTime day)
        => SelectedTask?.Id == task.Id && _selectedTaskDays.Contains(day.Date);

    public void ToggleSelectedTaskDay(DateTime day)
    {
        if (SelectedTask == null)
            return;
        var start = SelectedTask.StartDate.Date;
        var end = (SelectedTask.EndDate ?? SelectedTask.StartDate).Date;
        if (day.Date < start || day.Date > end)
            return;

        if (!_selectedTaskDays.Add(day.Date))
            _selectedTaskDays.Remove(day.Date);
        OnPropertyChanged(nameof(SelectedTask));
    }

    public static (DateTime Start, DateTime End) ComputeResizedRange(DateTime start, DateTime end, DateTime target, DateTime anchor)
    {
        start = start.Date;
        end = end.Date;
        target = target.Date;
        anchor = anchor.Date;

        if (anchor == start && anchor == end)
        {
            if (target < anchor)
            {
                start = target;
                end = anchor;
            }
            else if (target > anchor)
            {
                start = anchor;
                end = target;
            }
        }
        else if (anchor <= start)
        {
            start = target;
            if (start > end)
                end = start;
        }
        else
        {
            end = target;
            if (end < start)
                start = end;
        }

        return (start, end);
    }

    public async Task AdjustSelectedTaskRangeToDayAsync(DateTime targetDay, DateTime anchorDay)
    {
        if (SelectedTask == null)
            return;

        try
        {
            var task = SelectedTask;
            var before = CloneTask(task);
            var start = task.StartDate.Date;
            var end = (task.EndDate ?? task.StartDate).Date;
            var (newStart, newEnd) = ComputeResizedRange(start, end, targetDay, anchorDay);

            task.StartDate = newStart;
            task.EndDate = newEnd > newStart ? newEnd : null;
            var after = CloneTask(task);
            await _scheduleService.UpdateTaskAsync(task);
            PushHistory(
                undoAsync: async () => await _scheduleService.UpdateTaskAsync(CloneTask(before)),
                redoAsync: async () => await _scheduleService.UpdateTaskAsync(CloneTask(after)));
            SyncTaskInMemory(task);
        }
        catch (Exception ex)
        {
            ShowTaskOperationError(ex, "Fehler beim Anpassen des Zeitraums");
            await LoadDataAsync();
        }
    }

    public async Task AdjustSelectedTaskRangeToDayAsync(DateTime day) =>
        await AdjustSelectedTaskRangeToDayAsync(day, day);

    public async Task AdjustTaskRangeToDayAsync(int taskId, DateTime targetDay, DateTime anchorDay)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null)
            return;
        SelectedTask = task;
        await AdjustSelectedTaskRangeToDayAsync(targetDay, anchorDay);
    }

    public void SyncSelectedTaskTemplates(IReadOnlyList<TaskTemplate> templates)
    {
        SelectedTaskTemplates.Clear();
        foreach (var template in templates)
            SelectedTaskTemplates.Add(template);
        SelectedTaskTemplate = templates.Count > 0 ? templates[^1] : null;
        EditTemplateCommand.NotifyCanExecuteChanged();
        DeleteTemplateCommand.NotifyCanExecuteChanged();
    }

    public void SelectScheduleCell(Employee employee, DateTime day)
    {
        ScheduleHighlightMode = ScheduleHighlightMode.Cell;
        HighlightCellEmployeeId = employee.Id;
        HighlightCellDay = day.Date;
        HighlightColumnDay = null;
        HighlightRowEmployeeId = null;
        SelectedEmployee = employee;
        SelectedDate = day.Date;
    }

    public void SelectScheduleColumn(DateTime day)
    {
        ScheduleHighlightMode = ScheduleHighlightMode.Column;
        HighlightColumnDay = day.Date;
        HighlightCellEmployeeId = null;
        HighlightCellDay = null;
        HighlightRowEmployeeId = null;
        SelectedDate = day.Date;
        SelectedTask = null;
    }

    public void SelectScheduleRow(Employee employee)
    {
        ScheduleHighlightMode = ScheduleHighlightMode.Row;
        HighlightRowEmployeeId = employee.Id;
        HighlightCellEmployeeId = null;
        HighlightCellDay = null;
        HighlightColumnDay = null;
        SelectedEmployee = employee;
        SelectedTask = null;
    }

    public bool IsScheduleCellHighlighted(int employeeId, DateTime day) =>
        ScheduleHighlight.IsCellHighlighted(
            ScheduleHighlightMode,
            HighlightCellEmployeeId,
            HighlightCellDay,
            HighlightColumnDay,
            HighlightRowEmployeeId,
            employeeId,
            day);

    public bool IsScheduleColumnHeaderHighlighted(DateTime day) =>
        ScheduleHighlight.IsColumnHeaderHighlighted(ScheduleHighlightMode, HighlightColumnDay, day);

    public bool IsScheduleEmployeeNameHighlighted(int employeeId) =>
        ScheduleHighlight.IsEmployeeNameHighlighted(ScheduleHighlightMode, HighlightRowEmployeeId, employeeId);

    public void ClearScheduleSelection()
    {
        SelectedTask = null;
        SelectedEmployee = null;
        _selectedTaskDays.Clear();
        ScheduleHighlightMode = ScheduleHighlightMode.None;
        HighlightCellEmployeeId = null;
        HighlightCellDay = null;
        HighlightColumnDay = null;
        HighlightRowEmployeeId = null;
    }

    public async Task MoveTaskToDayAsync(WorkTask task, Employee targetEmployee, DateTime targetDay, DateTime anchorDay)
    {
        try
        {
            var before = CloneTask(task);
            var offsetDays = (targetDay.Date - anchorDay.Date).Days;
            if (offsetDays == 0 && task.EmployeeId == targetEmployee.Id)
                return;

            var start = task.StartDate.Date;
            var end = (task.EndDate ?? task.StartDate).Date;
            var durationDays = (end - start).Days;

            task.EmployeeId = targetEmployee.Id;
            task.StartDate = start.AddDays(offsetDays);
            task.EndDate = durationDays > 0 ? task.StartDate.AddDays(durationDays) : null;

            var after = CloneTask(task);
            await _scheduleService.UpdateTaskAsync(task);
            PushHistory(
                undoAsync: async () => await _scheduleService.UpdateTaskAsync(CloneTask(before)),
                redoAsync: async () => await _scheduleService.UpdateTaskAsync(CloneTask(after)));
            SelectedTask = task;
            SyncTaskInMemory(task, before.EmployeeId);
        }
        catch (Exception ex)
        {
            ShowTaskOperationError(ex, "Fehler beim Verschieben der Aufgabe");
        }
    }

    public async Task PasteCopiedTaskAsync(Employee targetEmployee, DateTime targetDay)
    {
        if (_copiedTask == null)
            return;
        try
        {
            var blueprints = new List<WorkTask>();
            var createdIds = new List<int>();
            var createdTasks = new List<WorkTask>();
            foreach (var segment in _copiedTask.Segments)
            {
                var segStart = targetDay.Date.AddDays(segment.StartOffset);
                var segEnd = targetDay.Date.AddDays(segment.EndOffset);
                var newTask = new WorkTask
                {
                    EmployeeId = targetEmployee.Id,
                    Description = _copiedTask.Description,
                    StartDate = segStart,
                    EndDate = segEnd > segStart ? segEnd : null,
                    StartTime = _copiedTask.StartTime,
                    EndTime = _copiedTask.EndTime,
                    Location = _copiedTask.Location,
                    Project = _copiedTask.Project,
                    Notes = _copiedTask.Notes,
                    Type = _copiedTask.Type,
                    Color = _copiedTask.Color
                };
                var created = await _scheduleService.CreateTaskAsync(newTask);
                createdIds.Add(created.Id);
                blueprints.Add(CloneTaskWithoutId(newTask));
                createdTasks.Add(created);
            }

            PushHistory(
                undoAsync: async () =>
                {
                    foreach (var id in createdIds.ToList())
                        await _scheduleService.DeleteTaskAsync(id);
                },
                redoAsync: async () =>
                {
                    createdIds.Clear();
                    foreach (var blueprint in blueprints.Select(CloneTaskWithoutId))
                    {
                        var recreated = await _scheduleService.CreateTaskAsync(blueprint);
                        createdIds.Add(recreated.Id);
                    }
                });
            _copiedTask = null;
            OnPropertyChanged(nameof(HasCopiedTask));
            AppendTasksInMemory(createdTasks);
        }
        catch (Exception ex)
        {
            ShowTaskOperationError(ex, "Fehler beim Einfügen der kopierten Aufgabe");
        }
    }

    private bool EnsureCanWrite()
    {
        if (_databaseOptions.CanWrite)
            return true;
        MessageBox.Show(
            TxtReadOnlyBanner,
            Tr("Nur lesen", "Read only", "Lecture seule", "Sola lettura"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    [RelayCommand]
    private async Task SearchPlanAsync()
    {
        var dialog = new SearchDialog(async query =>
        {
            var hits = await _scheduleService.SearchTasksAsync(query);
            return hits.Select(t => new SearchDialog.SearchHit(
                t,
                $"{t.StartDate:dd.MM.yyyy} · {t.Employee?.DisplayName ?? t.EmployeeId.ToString()} · {t.Description}")).ToList();
        });
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && dialog.SelectedTask != null)
        {
            var hit = dialog.SelectedTask;
            SelectedDate = hit.StartDate.Date;
            CurrentWeek = new WeekInfo(hit.StartDate);
            CurrentMonthStart = new DateTime(hit.StartDate.Year, hit.StartDate.Month, 1);
            CurrentViewMode = ScheduleViewMode.Week;
            await LoadDataAsync();
            SelectedTask = Tasks.FirstOrDefault(t => t.Id == hit.Id) ?? hit;
        }
    }

    [RelayCommand]
    private void TogglePlanShare()
    {
        if (_planShare.IsRunning)
        {
            _planShare.Stop();
            IsPlanShareRunning = false;
            PlanShareUrl = string.Empty;
            OnPropertyChanged(nameof(TxtShareWlan));
            return;
        }

        try
        {
            _planShare.Start(() => PlanShareHost.BuildHtml(CurrentWeek, Employees, Tasks));
            IsPlanShareRunning = true;
            PlanShareUrl = _planShare.PublicUrl ?? string.Empty;
            OnPropertyChanged(nameof(TxtShareWlan));
            MessageBox.Show(
                string.Format(
                    Tr("Plan im Büro-WLAN (nur lesen):\n{0}\n\nHandy im gleichen WLAN öffnet diese Adresse. Firewall ggf. Port 5088 erlauben.", "Read-only plan on office WLAN:\n{0}\n\nOpen this address on a phone in the same WLAN. Allow firewall port 5088 if needed.", "Plan en lecture seule sur le Wi-Fi :\n{0}", "Piano in sola lettura sul Wi-Fi:\n{0}"),
                    PlanShareUrl),
                TxtShareWlan,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _planShare.Stop();
            IsPlanShareRunning = false;
            MessageBox.Show(
                $"{Tr("WLAN-Ansicht konnte nicht gestartet werden", "Could not start the WLAN view", "Impossible de démarrer la vue Wi-Fi", "Impossibile avviare la vista Wi-Fi")}:\n{ex.Message}",
                TxtShareWlan,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ShowTaskOperationError(Exception ex, string logMessage)
    {
        _logger.LogError(ex, logMessage);
        if (ex is ConcurrencyConflictException)
        {
            MessageBox.Show(
                UiText.MsgConcurrencyConflict,
                Tr("Gleichzeitige Änderung", "Concurrent change", "Modification simultanée", "Modifica simultanea"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            _ = LoadDataAsync();
            return;
        }

        if (ex is DuplicateTaskException duplicate)
        {
            MessageBox.Show(
                string.Format(
                    Tr(
                        "Die Aufgabe '{0}' ist für diesen Mitarbeiter am {1:dd.MM.yyyy} bereits eingetragen.",
                        "Task '{0}' is already assigned to this employee on {1:MM/dd/yyyy}.",
                        "La tâche '{0}' est déjà enregistrée pour cet employé le {1:dd.MM.yyyy}.",
                        "L'attività '{0}' è già registrata per questo dipendente il {1:dd.MM.yyyy}."),
                    duplicate.TaskDescription,
                    duplicate.Day),
                Tr("Doppelte Aufgabe", "Duplicate task", "Tâche en double", "Attività duplicata"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(
            $"{Tr("Fehler", "Error", "Erreur", "Errore")}: {ex.Message}",
            Tr("Fehler", "Error", "Erreur", "Errore"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void PushHistory(Func<Task> undoAsync, Func<Task> redoAsync)
    {
        _history.Push(undoAsync, redoAsync);
        NotifyHistoryStateChanged();
    }

    private void NotifyHistoryStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private static WorkTask CloneTask(WorkTask source) => source.Clone();

    private static WorkTask CloneTaskWithoutId(WorkTask source) => source.Clone(includeId: false);

    private async Task<List<WorkTask>> SnapshotEmployeeTasksAsync(int employeeId, DateTime start, DateTime end)
    {
        var tasks = await _scheduleService.GetTasksForDateRangeAsync(start.Date, end.Date);
        return tasks
            .Where(t => t.EmployeeId == employeeId)
            .Select(CloneTask)
            .ToList();
    }

    private async Task ReplaceEmployeeTasksInRangeAsync(int employeeId, DateTime start, DateTime end, IEnumerable<WorkTask> targetTasks)
    {
        var existing = await _scheduleService.GetTasksForDateRangeAsync(start.Date, end.Date);
        var existingIds = existing
            .Where(t => t.EmployeeId == employeeId)
            .Select(t => t.Id)
            .Distinct()
            .ToList();

        foreach (var id in existingIds)
            await _scheduleService.DeleteTaskAsync(id);

        foreach (var task in targetTasks.Select(CloneTaskWithoutId))
            await _scheduleService.CreateTaskAsync(task);
    }

    private async Task DeleteSelectedDaysFromTaskAsync(WorkTask task, IEnumerable<DateTime> daysToDelete)
    {
        var start = task.StartDate.Date;
        var end = (task.EndDate ?? task.StartDate).Date;
        var removeSet = new HashSet<DateTime>(daysToDelete.Select(d => d.Date));
        var keepDays = Enumerable.Range(0, (end - start).Days + 1)
            .Select(i => start.AddDays(i))
            .Where(d => !removeSet.Contains(d))
            .ToList();

        if (keepDays.Count == 0)
        {
            await _scheduleService.DeleteTaskAsync(task.Id);
            return;
        }

        var segments = new List<(DateTime Start, DateTime End)>();
        var segStart = keepDays[0];
        var prev = keepDays[0];
        for (int i = 1; i < keepDays.Count; i++)
        {
            if ((keepDays[i] - prev).Days == 1)
            {
                prev = keepDays[i];
                continue;
            }
            segments.Add((segStart, prev));
            segStart = keepDays[i];
            prev = keepDays[i];
        }
        segments.Add((segStart, prev));

        task.StartDate = segments[0].Start;
        task.EndDate = segments[0].End > segments[0].Start ? segments[0].End : null;
        await _scheduleService.UpdateTaskAsync(task);

        for (int i = 1; i < segments.Count; i++)
        {
            var seg = segments[i];
            var copy = new WorkTask
            {
                EmployeeId = task.EmployeeId,
                Description = task.Description,
                StartDate = seg.Start,
                EndDate = seg.End > seg.Start ? seg.End : null,
                StartTime = task.StartTime,
                EndTime = task.EndTime,
                Location = task.Location,
                Project = task.Project,
                Notes = task.Notes,
                Type = task.Type,
                Color = task.Color
            };
            await _scheduleService.CreateTaskAsync(copy);
        }
    }

    [RelayCommand]
    private async Task AddHolidayAsync()
    {
        if (Employees.Count == 0)
        {
            MessageBox.Show(
                Tr("Bitte legen Sie zuerst mindestens einen Mitarbeiter an.", "Please create at least one employee first.", "Veuillez d'abord créer au moins un employé.", "Crea prima almeno un dipendente."),
                Tr("Keine Mitarbeiter", "No employees", "Aucun employé", "Nessun dipendente"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        try
        {
            var dialog = new HolidayDialog();
            dialog.SetViewModel(new HolidayDialogViewModel());
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            if (dialog.ShowDialog() != true)
                return;

            var days = dialog.ViewModel.GetHolidayDays();
            if (days.Count == 0)
            {
                MessageBox.Show(
                    Tr("Bitte geben Sie eine Bezeichnung ein oder wählen Sie Feiertage.", "Please enter a label or select holidays.", "Veuillez saisir un intitulé ou choisir des jours fériés.", "Inserisci un'etichetta o seleziona festività."),
                    Tr("Hinweis", "Note", "Information", "Avviso"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            int count = 0;
            var tasks = HolidayPlanner.CreateTasks(Employees, days);
            foreach (var task in tasks)
            {
                await _scheduleService.CreateTaskAsync(task);
                count++;
            }
            await LoadDataAsync();
            MessageBox.Show(
                string.Format(
                    Tr("{0} wurde für alle {1} Mitarbeiter an {2} Tag(en) eingetragen ({3} Einträge).", "{0} has been added for all {1} employees on {2} day(s) ({3} entries).", "{0} a été ajouté pour les {1} employés sur {2} jour(s) ({3} entrées).", "{0} è stato inserito per tutti i {1} dipendenti su {2} giorno/i ({3} voci)."),
                    days.Count == 1 ? days[0].Name : Tr("Feiertage", "Holidays", "Jours fériés", "Festività"),
                    Employees.Count,
                    days.Select(d => d.Date).Distinct().Count(),
                    count),
                Tr("Feiertag eingetragen", "Holiday added", "Jour férié ajouté", "Festività inserita"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Eintragen des Feiertags");
            MessageBox.Show($"{Tr("Fehler", "Error", "Erreur", "Errore")}: {ex.Message}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task AddCompanyVacationAsync()
    {
        if (Employees.Count == 0)
        {
            MessageBox.Show(
                Tr("Bitte legen Sie zuerst mindestens einen Mitarbeiter an.", "Please create at least one employee first.", "Veuillez d'abord créer au moins un employé.", "Crea prima almeno un dipendente."),
                Tr("Keine Mitarbeiter", "No employees", "Aucun employé", "Nessun dipendente"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var dialog = new HolidayDialog();
            dialog.SetViewModel(HolidayDialogViewModel.ForCompanyVacation());
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            if (dialog.ShowDialog() != true)
                return;

            var name = dialog.ViewModel.HolidayName;
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(UiText.MsgEnterHolidayName, UiText.CaptionHint, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (start, end) = dialog.ViewModel.GetRange();
            var tasks = HolidayPlanner.CreateRangeTasks(Employees, start, end, name);
            foreach (var task in tasks)
                await _scheduleService.CreateTaskAsync(task);
            await LoadDataAsync();
            MessageBox.Show(
                string.Format(
                    Tr("{0} wurde für alle {1} Mitarbeiter eingetragen ({2}–{3}).", "{0} has been added for all {1} employees ({2}–{3}).", "{0} a été ajouté pour les {1} employés ({2}–{3}).", "{0} è stato inserito per tutti i {1} dipendenti ({2}–{3})."),
                    name.Trim(),
                    Employees.Count,
                    start.ToString("dd.MM.yyyy"),
                    end.ToString("dd.MM.yyyy")),
                UiText.CompanyVacationTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Eintragen der Ferien");
            MessageBox.Show($"{Tr("Fehler", "Error", "Erreur", "Errore")}: {ex.Message}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task AddTemplate()
    {
        var dialog = new TaskTemplateDialog();
        dialog.SetViewModel(new TaskTemplateDialogViewModel(null));
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && dialog.ViewModel.GetTemplate() is { } template)
        {
            DefaultTasks.Add(template);
            ReorderDefaultTasks();
            await SaveTemplatesToFileAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditTemplate))]
    private async Task EditTemplate()
    {
        if (SelectedTaskTemplate == null) return;
        var dialog = new TaskTemplateDialog();
        dialog.SetViewModel(new TaskTemplateDialogViewModel(SelectedTaskTemplate));
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && dialog.ViewModel.GetTemplate(SelectedTaskTemplate.IsFavorite) is { } template)
        {
            var idx = DefaultTasks.IndexOf(SelectedTaskTemplate);
            if (idx >= 0)
            {
                DefaultTasks.RemoveAt(idx);
                DefaultTasks.Insert(idx, template);
                SelectedTaskTemplate = template;
                ReorderDefaultTasks();
                await SaveTemplatesToFileAsync();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteTemplate))]
    private async Task DeleteTemplate()
    {
        if (SelectedTaskTemplates.Count == 0) return;

        var message = SelectedTaskTemplates.Count == 1
            ? string.Format(
                Tr("Vorlage '{0}' wirklich löschen?", "Delete template '{0}'?", "Supprimer le modèle '{0}' ?", "Eliminare il modello '{0}'?"),
                SelectedTaskTemplates[0].Description)
            : string.Format(
                Tr("{0} Vorlagen wirklich löschen?", "Delete {0} templates?", "Supprimer {0} modèles ?", "Eliminare {0} modelli?"),
                SelectedTaskTemplates.Count);

        var result = MessageBox.Show(
            message,
            Tr("Vorlage löschen", "Delete template", "Supprimer modèle", "Elimina modello"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            foreach (var template in SelectedTaskTemplates.ToList())
                DefaultTasks.Remove(template);
            SelectedTaskTemplates.Clear();
            SelectedTaskTemplate = null;
            await SaveTemplatesToFileAsync();
            DeleteTemplateCommand.NotifyCanExecuteChanged();
            EditTemplateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanEditTemplate() => SelectedTaskTemplates.Count == 1 && SelectedTaskTemplate != null;
    private bool CanDeleteTemplate() => SelectedTaskTemplates.Count > 0;

    [RelayCommand]
    private async Task ToggleTemplateFavorite(TaskTemplate? template)
    {
        if (template == null)
            return;

        template.IsFavorite = !template.IsFavorite;
        ReorderDefaultTasks();
        await SaveTemplatesToFileAsync();
    }

    public async Task AddTaskAsFavoriteAsync(WorkTask? task)
    {
        if (task == null || string.IsNullOrWhiteSpace(task.Description))
            return;

        var existing = FindMatchingTemplate(task);
        if (existing != null)
        {
            if (existing.IsFavorite)
                return;
            existing.IsFavorite = true;
        }
        else
        {
            DefaultTasks.Add(new TaskTemplate
            {
                Description = task.Description.Trim(),
                Type = task.Type,
                Color = task.Color,
                IsFavorite = true
            });
        }

        ReorderDefaultTasks();
        await SaveTemplatesToFileAsync();
    }

    public bool IsTaskAlreadyFavorite(WorkTask? task) =>
        FindMatchingTemplate(task)?.IsFavorite == true;

    private TaskTemplate? FindMatchingTemplate(WorkTask? task)
    {
        if (task == null)
            return null;
        var description = task.Description.Trim();
        return DefaultTasks.FirstOrDefault(t =>
            string.Equals(t.Description.Trim(), description, StringComparison.OrdinalIgnoreCase)
            && t.Type == task.Type
            && t.Color == task.Color);
    }

    private void ReorderDefaultTasks()
    {
        var ordered = Presentation.Services.TaskTemplateStorage.SortFavoritesFirst(DefaultTasks);
        DefaultTasks.Clear();
        foreach (var template in ordered)
            DefaultTasks.Add(template);
    }

    private async Task ReloadTemplatesPreservingSelectionAsync()
    {
        var loaded = await _templateStorage.LoadAsync();
        if (TemplatesMatch(DefaultTasks, loaded))
            return;

        var selectedKeys = SelectedTaskTemplates
            .Select(t => (t.Description, t.Type, t.Color, t.IsFavorite))
            .ToHashSet();
        DefaultTasks.Clear();
        foreach (var template in loaded)
            DefaultTasks.Add(template);

        var restored = DefaultTasks
            .Where(t => selectedKeys.Contains((t.Description, t.Type, t.Color, t.IsFavorite)))
            .ToList();
        SyncSelectedTaskTemplates(restored);
    }

    private static bool TemplatesMatch(
        IReadOnlyList<TaskTemplate> current,
        IReadOnlyList<TaskTemplate> loaded)
    {
        if (current.Count != loaded.Count)
            return false;
        for (var i = 0; i < current.Count; i++)
        {
            var a = current[i];
            var b = loaded[i];
            if (a.Description != b.Description || a.Type != b.Type || a.Color != b.Color || a.IsFavorite != b.IsFavorite)
                return false;
        }
        return true;
    }

    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        try
        {
            var (defaultStart, defaultEnd) = GetDefaultExportRange();
            var rangeVm = new ExportRangeViewModel(defaultStart, defaultEnd);
            var rangeDialog = new ExportRangeDialog();
            rangeDialog.SetViewModel(rangeVm);
            rangeDialog.Owner = System.Windows.Application.Current.MainWindow;
            if (rangeDialog.ShowDialog() != true)
                return;

            var (start, end) = rangeVm.GetRange();
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel-Dateien (*.xlsx)|*.xlsx",
                FileName = $"Wochenplan_{start:yyyy-MM-dd}_bis_{end:yyyy-MM-dd}.xlsx"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var tasksInRange = await _scheduleService.GetTasksForDateRangeAsync(start, end);
                await _exportService.ExportToExcelAsync(Employees, tasksInRange, start, end, saveDialog.FileName, rangeVm.FontSize);
                MessageBox.Show(Tr("Export erfolgreich!", "Export successful!", "Export réussi !", "Esportazione riuscita!"), Tr("Erfolg", "Success", "Succès", "Successo"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Excel-Export");
            var msg = ex is IOException && ex.Message.Contains("being used by another process")
                ? Tr("Die Datei wird von einem anderen Programm verwendet (z. B. Excel). Bitte schließen Sie die Datei und versuchen Sie es erneut.", "The file is being used by another program (e.g. Excel). Please close the file and try again.", "Le fichier est utilisé par un autre programme (p. ex. Excel). Fermez-le et réessayez.", "Il file è in uso da un altro programma (es. Excel). Chiudi il file e riprova.")
                : ex.Message;
            MessageBox.Show($"{Tr("Fehler beim Export", "Export error", "Erreur d'export", "Errore di esportazione")}: {msg}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportToPdfAsync()
    {
        try
        {
            var (defaultStart, defaultEnd) = GetDefaultExportRange();
            var rangeVm = new ExportRangeViewModel(defaultStart, defaultEnd);
            var rangeDialog = new ExportRangeDialog();
            rangeDialog.SetViewModel(rangeVm);
            rangeDialog.Owner = System.Windows.Application.Current.MainWindow;
            if (rangeDialog.ShowDialog() != true)
                return;

            var (start, end) = rangeVm.GetRange();
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF-Dateien (*.pdf)|*.pdf",
                FileName = $"Wochenplan_{start:yyyy-MM-dd}_bis_{end:yyyy-MM-dd}.pdf"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var tasksInRange = await _scheduleService.GetTasksForDateRangeAsync(start, end);
                await _exportService.ExportToPdfAsync(Employees, tasksInRange, start, end, saveDialog.FileName, rangeVm.FontSize);
                MessageBox.Show(Tr("Export erfolgreich!", "Export successful!", "Export réussi !", "Esportazione riuscita!"), Tr("Erfolg", "Success", "Succès", "Successo"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim PDF-Export");
            var msg = ex is IOException && ex.Message.Contains("being used by another process")
                ? Tr("Die Datei wird von einem anderen Programm verwendet. Bitte schließen Sie die Datei und versuchen Sie es erneut.", "The file is being used by another program. Please close the file and try again.", "Le fichier est utilisé par un autre programme. Fermez-le et réessayez.", "Il file è in uso da un altro programma. Chiudi il file e riprova.")
                : ex.Message;
            MessageBox.Show($"{Tr("Fehler beim Export", "Export error", "Erreur d'export", "Errore di esportazione")}: {msg}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task CopyWeekAsync()
    {
        if (CurrentViewMode != ScheduleViewMode.Week)
        {
            MessageBox.Show(Tr("Woche kopieren ist nur in der Wochenansicht möglich.", "Copy week is only available in week view.", "La copie de semaine est disponible uniquement en vue semaine.", "La copia settimana è disponibile solo nella vista settimana."), Tr("Hinweis", "Note", "Information", "Avviso"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (Employees.Count == 0)
        {
            MessageBox.Show(Tr("Es sind keine Mitarbeiter angelegt.", "No employees have been created.", "Aucun employé n'a été créé.", "Non è stato creato alcun dipendente."), Tr("Hinweis", "Note", "Information", "Avviso"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var targetVm = new ExportRangeViewModel(CurrentWeek.StartDate.AddDays(7), CurrentWeek.EndDate.AddDays(7));
            var dialog = new ExportRangeDialog();
            dialog.Title = Tr("Zielwoche wählen", "Select target week", "Choisir la semaine cible", "Scegli settimana di destinazione");
            dialog.SetViewModel(targetVm);
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            if (dialog.ShowDialog() != true)
                return;
            var (targetStart, targetEnd) = targetVm.GetRange();
            var sourceTasks = await _scheduleService.GetTasksForDateRangeAsync(CurrentWeek.StartDate, CurrentWeek.EndDate);
            var offset = (targetStart.Date - CurrentWeek.StartDate.Date).Days;
            int count = 0;
            foreach (var t in sourceTasks)
            {
                var newStart = t.StartDate.Date.AddDays(offset);
                var newEnd = t.EndDate.HasValue ? t.EndDate.Value.Date.AddDays(offset) : (DateTime?)null;
                if (newEnd.HasValue && newEnd < newStart) newEnd = newStart;
                var task = new WorkTask
                {
                    EmployeeId = t.EmployeeId,
                    Description = t.Description,
                    StartDate = newStart,
                    EndDate = newEnd,
                    StartTime = t.StartTime,
                    EndTime = t.EndTime,
                    Location = t.Location,
                    Project = t.Project,
                    Notes = t.Notes,
                    Type = t.Type,
                    Color = t.Color
                };
                await _scheduleService.CreateTaskAsync(task);
                count++;
            }
            await LoadDataAsync();
            MessageBox.Show(string.Format(Tr("{0} Aufgabe(n) in die Zielwoche kopiert.", "{0} task(s) copied to target week.", "{0} tâche(s) copiée(s) dans la semaine cible.", "{0} attività copiata/e nella settimana di destinazione."), count), Tr("Woche kopiert", "Week copied", "Semaine copiée", "Settimana copiata"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Kopieren der Woche");
            MessageBox.Show($"{Tr("Fehler", "Error", "Erreur", "Errore")}: {ex.Message}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private (DateTime Start, DateTime End) GetDefaultExportRange() =>
        ExportRangeCalculator.GetDefaultRange(
            CurrentViewMode,
            SelectedDate,
            CurrentMonthStart,
            CurrentWeek.StartDate,
            CurrentWeek.EndDate);

    [RelayCommand]
    private Task PrintAsync()
    {
        try
        {
            if (TryPrintSchedule == null)
            {
                MessageBox.Show(
                    UiText.PrintViewNotReady,
                    Tr("Drucken", "Print", "Imprimer", "Stampa"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return Task.CompletedTask;
            }

            TryPrintSchedule.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Drucken");
            MessageBox.Show($"{Tr("Fehler", "Error", "Erreur", "Errore")}: {ex.Message}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SeedDemoDataAsync()
    {
        var confirm = MessageBox.Show(
            Tr(
                "Testdaten vom Werkstatt-Bild laden?\n\n• Vorlagen werden überschrieben\n• 8 Mitarbeiter und die Woche 06.–10.07.2026 werden neu angelegt\n• Andere Mitarbeiter bleiben erhalten",
                "Load workshop screenshot test data?\n\n• Templates will be replaced\n• 8 employees and week 06–10 Jul 2026 will be recreated\n• Other employees stay",
                "Charger les données de test de l'atelier ?",
                "Caricare i dati di test dell'officina?"),
            Tr("Demo-Daten", "Demo data", "Données démo", "Dati demo"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            await _demoDataService.SeedFeedbackDemoAsync();
            DefaultTasks.Clear();
            foreach (var t in await _templateStorage.LoadAsync())
                DefaultTasks.Add(t);

            SelectedDate = Presentation.Services.DemoDataSeeder.DemoWeekMonday;
            CurrentWeek = new WeekInfo(SelectedDate);
            CurrentMonthStart = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
            CurrentViewMode = ScheduleViewMode.Week;
            await LoadDataAsync();

            MessageBox.Show(
                Tr("Testdaten wurden geladen. Ansicht: KW 06.–10.07.2026", "Test data loaded. View: week 06–10 Jul 2026", "Données de test chargées.", "Dati di test caricati."),
                Tr("Fertig", "Done", "Terminé", "Fatto"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Demo-Daten");
            MessageBox.Show($"{Tr("Fehler", "Error", "Erreur", "Errore")}: {ex.Message}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string GetAppDataFolder() =>
        AppPaths.Root;

    private static string GetLanguageSettingsPath() =>
        Path.Combine(GetAppDataFolder(), "language.txt");

    partial void OnSelectedLanguageCodeChanged(string value)
    {
        OnPropertyChanged(nameof(IsGermanLanguage));
        OnPropertyChanged(nameof(IsEnglishLanguage));
        OnPropertyChanged(nameof(IsFrenchLanguage));
        OnPropertyChanged(nameof(IsItalianLanguage));
        NotifyLocalizedUiTextChanged();
    }

    private void NotifyLocalizedUiTextChanged()
    {
        OnPropertyChanged(nameof(TxtOptions));
        OnPropertyChanged(nameof(TxtButtons));
        OnPropertyChanged(nameof(TxtOptionsThemeSection));
        OnPropertyChanged(nameof(TxtDatabaseSettings));
        OnPropertyChanged(nameof(TxtDatabaseStatus));
        OnPropertyChanged(nameof(TxtRefresh));
        OnPropertyChanged(nameof(TxtRefreshTooltip));
        OnPropertyChanged(nameof(TxtAutoReload));
        OnPropertyChanged(nameof(TxtAutoReloadHint));
        OnPropertyChanged(nameof(TxtLanguage));
        OnPropertyChanged(nameof(TxtFilter));
        OnPropertyChanged(nameof(TxtFilterReset));
        OnPropertyChanged(nameof(TxtEmployeesGroup));
        OnPropertyChanged(nameof(TxtEmployeeColumn));
        OnPropertyChanged(nameof(TxtEmployeeReorderHint));
        OnPropertyChanged(nameof(TxtEmployeeSortHint));
        OnPropertyChanged(nameof(TxtEmployeeSortLabel));
        OnPropertyChanged(nameof(TxtEmployeeSortTooltip));
        OnPropertyChanged(nameof(TxtCalendarWeekShort));
        OnPropertyChanged(nameof(TxtTasksGroup));
        OnPropertyChanged(nameof(TxtExportGroup));
        OnPropertyChanged(nameof(TxtToday));
        OnPropertyChanged(nameof(TxtPrevNavGroup));
        OnPropertyChanged(nameof(TxtCurrentNavGroup));
        OnPropertyChanged(nameof(TxtNextNavGroup));
        OnPropertyChanged(nameof(TxtCurrentWeek));
        OnPropertyChanged(nameof(TxtNextWeek));
        OnPropertyChanged(nameof(TxtCurrentDay));
        OnPropertyChanged(nameof(TxtNextDay));
        OnPropertyChanged(nameof(TxtCurrentMonth));
        OnPropertyChanged(nameof(TxtNextMonth));
        OnPropertyChanged(nameof(TxtPrevWeek));
        OnPropertyChanged(nameof(TxtPrevDay));
        OnPropertyChanged(nameof(TxtPrevMonth));
        OnPropertyChanged(nameof(TxtViewDay));
        OnPropertyChanged(nameof(TxtViewWeek));
        OnPropertyChanged(nameof(TxtViewMonth));
        OnPropertyChanged(nameof(TxtAddEmployee));
        OnPropertyChanged(nameof(TxtEditEmployee));
        OnPropertyChanged(nameof(TxtDeleteEmployee));
        OnPropertyChanged(nameof(TxtSearch));
        OnPropertyChanged(nameof(TxtShareWlan));
        OnPropertyChanged(nameof(TxtShowWeekend));
        OnPropertyChanged(nameof(TxtShowInactiveEmployees));
        OnPropertyChanged(nameof(TxtReadOnlyBanner));
        OnPropertyChanged(nameof(TxtAddTask));
        OnPropertyChanged(nameof(TxtEditTask));
        OnPropertyChanged(nameof(TxtDeleteTask));
        OnPropertyChanged(nameof(TxtUndo));
        OnPropertyChanged(nameof(TxtRedo));
        OnPropertyChanged(nameof(TxtHoliday));
        OnPropertyChanged(nameof(TxtCompanyVacation));
        OnPropertyChanged(nameof(TxtPrint));
        OnPropertyChanged(nameof(TxtBackup));
        OnPropertyChanged(nameof(TxtCopyWeek));
        OnPropertyChanged(nameof(TxtStatistics));
        OnPropertyChanged(nameof(TxtAbsence));
        OnPropertyChanged(nameof(TxtRestore));
        OnPropertyChanged(nameof(TxtDemoSection));
        OnPropertyChanged(nameof(TxtSeedDemo));
        OnPropertyChanged(nameof(TxtSeedDemoTooltip));
        OnPropertyChanged(nameof(TxtImport));
        OnPropertyChanged(nameof(TxtStandardTasks));
        OnPropertyChanged(nameof(TxtTemplateHint));
        OnPropertyChanged(nameof(TxtTemplatePanelResizeTooltip));
        OnPropertyChanged(nameof(TxtMoveTaskTooltip));
        OnPropertyChanged(nameof(TxtTemplateAdd));
        OnPropertyChanged(nameof(TxtTemplateEdit));
        OnPropertyChanged(nameof(TxtTemplateDelete));
        OnPropertyChanged(nameof(TxtOptionsButtonsSection));
        OnPropertyChanged(nameof(TxtOptionsThemeSection));
        OnPropertyChanged(nameof(TxtOptionsLanguageSection));
        OnPropertyChanged(nameof(TxtThemeDark));
        OnPropertyChanged(nameof(TxtThemeLight));
        OnPropertyChanged(nameof(TxtNoEmployees));
        OnPropertyChanged(nameof(TxtNoEmployeesCta));
        OnPropertyChanged(nameof(TxtTodayTooltip));
        OnPropertyChanged(nameof(TxtDatePickerTooltip));
        OnPropertyChanged(nameof(TxtHolidayTooltip));
        OnPropertyChanged(nameof(TxtCompanyVacationTooltip));
        OnPropertyChanged(nameof(TxtBackupTooltip));
        OnPropertyChanged(nameof(TxtRestoreTooltip));
        OnPropertyChanged(nameof(TxtStatisticsTooltip));
        OnPropertyChanged(nameof(TxtAbsenceTooltip));
        OnPropertyChanged(nameof(TxtFilterTextTooltip));
        OnPropertyChanged(nameof(TxtPasteCopiedTaskHere));
        OnPropertyChanged(nameof(TxtContextEdit));
        OnPropertyChanged(nameof(TxtContextDelete));
        OnPropertyChanged(nameof(TxtContextCopy));
        OnPropertyChanged(nameof(TxtContextAddFavorite));
        OnPropertyChanged(nameof(TxtContextAlreadyFavorite));
        OnPropertyChanged(nameof(TxtTooltipLocation));
        OnPropertyChanged(nameof(TxtTooltipProject));
        OnPropertyChanged(nameof(TxtTooltipTime));
        OnPropertyChanged(nameof(TxtCopyGripTooltip));
        OnPropertyChanged(nameof(TxtResizeGripTooltip));
        OnPropertyChanged(nameof(TxtReorderEmployeeTooltip));
        OnPropertyChanged(nameof(ShowPrevNavigationButtons));
        OnPropertyChanged(nameof(ShowCurrentNavigationButtons));
        OnPropertyChanged(nameof(ShowNextNavigationButtons));
        OnPropertyChanged(nameof(WeekHeaderText));
    }

    partial void OnShowRestoreButtonChanged(bool value) => OnPropertyChanged(nameof(ShowRestoreButtonOnToolbar));

    partial void OnShowPrevWeekButtonChanged(bool value) => OnPropertyChanged(nameof(ShowPrevNavigationButtons));
    partial void OnShowPrevDayButtonChanged(bool value) => OnPropertyChanged(nameof(ShowPrevNavigationButtons));
    partial void OnShowPrevMonthButtonChanged(bool value) => OnPropertyChanged(nameof(ShowPrevNavigationButtons));
    partial void OnShowCurrentDayButtonChanged(bool value) => OnPropertyChanged(nameof(ShowCurrentNavigationButtons));
    partial void OnShowCurrentMonthButtonChanged(bool value) => OnPropertyChanged(nameof(ShowCurrentNavigationButtons));
    partial void OnShowNextWeekButtonChanged(bool value) => OnPropertyChanged(nameof(ShowNextNavigationButtons));
    partial void OnShowNextDayButtonChanged(bool value) => OnPropertyChanged(nameof(ShowNextNavigationButtons));
    partial void OnShowNextMonthButtonChanged(bool value) => OnPropertyChanged(nameof(ShowNextNavigationButtons));

    private static string Tr(string de, string en, string fr, string it) =>
        UiText.Tr(de, en, fr, it);

    private void ApplySavedLanguage()
    {
        try
        {
            var path = GetLanguageSettingsPath();
            if (!File.Exists(path))
            {
                ApplyLanguage("de-CH");
                return;
            }

            var culture = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(culture))
                culture = "de-CH";
            ApplyLanguage(culture);
        }
        catch
        {
            ApplyLanguage("de-CH");
        }
    }

    private void OnLayoutPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_loadingUiLayout || string.IsNullOrEmpty(e.PropertyName))
            return;

        if (e.PropertyName.StartsWith("Show", StringComparison.Ordinal)
            || e.PropertyName is nameof(IsAutoReloadEnabled) or nameof(ShowFilterBar) or nameof(IsFilterPanelExpanded) or nameof(ShowInactiveEmployees))
            PersistUiLayout();

        if (e.PropertyName == nameof(ShowInactiveEmployees) && !_loadingUiLayout)
            _ = LoadDataAsync(reloadEmployees: false);

        if (e.PropertyName == nameof(IsAutoReloadEnabled))
            UpdateAutoReloadTimer();
    }

    private void ApplyUiLayout()
    {
        _loadingUiLayout = true;
        try
        {
            var s = UiLayoutSettings.Load();
            IsAutoReloadEnabled = s.IsAutoReloadEnabled ?? true;
            ShowFilterBar = s.ShowFilterBar ?? true;
            IsFilterPanelExpanded = s.IsFilterPanelExpanded ?? false;
            ShowPrevWeekButton = s.ShowPrevWeekButton ?? true;
            ShowPrevDayButton = s.ShowPrevDayButton ?? true;
            ShowPrevMonthButton = s.ShowPrevMonthButton ?? true;
            ShowTodayButton = s.ShowTodayButton ?? true;
            ShowCurrentWeekButton = s.ShowCurrentWeekButton ?? false;
            ShowNextWeekButton = s.ShowNextWeekButton ?? true;
            ShowCurrentDayButton = s.ShowCurrentDayButton ?? true;
            ShowNextDayButton = s.ShowNextDayButton ?? true;
            ShowCurrentMonthButton = s.ShowCurrentMonthButton ?? true;
            ShowNextMonthButton = s.ShowNextMonthButton ?? true;
            ShowViewDayButton = s.ShowViewDayButton ?? true;
            ShowViewWeekButton = s.ShowViewWeekButton ?? true;
            ShowViewMonthButton = s.ShowViewMonthButton ?? true;
            ShowExcelButton = s.ShowExcelButton ?? true;
            ShowPdfButton = s.ShowPdfButton ?? true;
            ShowAddEmployeeButton = s.ShowAddEmployeeButton ?? true;
            ShowEditEmployeeButton = s.ShowEditEmployeeButton ?? true;
            ShowDeleteEmployeeButton = s.ShowDeleteEmployeeButton ?? true;
            ShowAddTaskButton = s.ShowAddTaskButton ?? true;
            ShowEditTaskButton = s.ShowEditTaskButton ?? true;
            ShowDeleteTaskButton = s.ShowDeleteTaskButton ?? true;
            ShowUndoButton = s.ShowUndoButton ?? true;
            ShowRedoButton = s.ShowRedoButton ?? true;
            ShowHolidayButton = s.ShowHolidayButton ?? true;
            ShowCompanyVacationButton = s.ShowCompanyVacationButton ?? true;
            ShowImportButton = s.ShowImportButton ?? true;
            ShowPrintButton = s.ShowPrintButton ?? true;
            ShowBackupButton = s.ShowBackupButton ?? true;
            ShowCopyWeekButton = s.ShowCopyWeekButton ?? true;
            ShowStatisticsButton = s.ShowStatisticsButton ?? true;
            ShowAbsenceButton = s.ShowAbsenceButton ?? true;
            ShowRestoreButton = s.ShowRestoreButton ?? true;
            ShowWeekend = s.ShowWeekend ?? false;
            ShowInactiveEmployees = s.ShowInactiveEmployees ?? false;
        }
        finally
        {
            _loadingUiLayout = false;
        }
    }

    private void PersistUiLayout()
    {
        var state = UiLayoutSettings.Load();
        state.IsAutoReloadEnabled = IsAutoReloadEnabled;
        state.ShowFilterBar = ShowFilterBar;
        state.IsFilterPanelExpanded = IsFilterPanelExpanded;
        state.ShowPrevWeekButton = ShowPrevWeekButton;
        state.ShowPrevDayButton = ShowPrevDayButton;
        state.ShowPrevMonthButton = ShowPrevMonthButton;
        state.ShowTodayButton = ShowTodayButton;
        state.ShowCurrentWeekButton = ShowCurrentWeekButton;
        state.ShowNextWeekButton = ShowNextWeekButton;
        state.ShowCurrentDayButton = ShowCurrentDayButton;
        state.ShowNextDayButton = ShowNextDayButton;
        state.ShowCurrentMonthButton = ShowCurrentMonthButton;
        state.ShowNextMonthButton = ShowNextMonthButton;
        state.ShowViewDayButton = ShowViewDayButton;
        state.ShowViewWeekButton = ShowViewWeekButton;
        state.ShowViewMonthButton = ShowViewMonthButton;
        state.ShowExcelButton = ShowExcelButton;
        state.ShowPdfButton = ShowPdfButton;
        state.ShowAddEmployeeButton = ShowAddEmployeeButton;
        state.ShowEditEmployeeButton = ShowEditEmployeeButton;
        state.ShowDeleteEmployeeButton = ShowDeleteEmployeeButton;
        state.ShowAddTaskButton = ShowAddTaskButton;
        state.ShowEditTaskButton = ShowEditTaskButton;
        state.ShowDeleteTaskButton = ShowDeleteTaskButton;
        state.ShowUndoButton = ShowUndoButton;
        state.ShowRedoButton = ShowRedoButton;
        state.ShowHolidayButton = ShowHolidayButton;
        state.ShowCompanyVacationButton = ShowCompanyVacationButton;
        state.ShowImportButton = ShowImportButton;
        state.ShowPrintButton = ShowPrintButton;
        state.ShowBackupButton = ShowBackupButton;
        state.ShowCopyWeekButton = ShowCopyWeekButton;
        state.ShowStatisticsButton = ShowStatisticsButton;
        state.ShowAbsenceButton = ShowAbsenceButton;
        state.ShowRestoreButton = ShowRestoreButton;
        state.ShowWeekend = ShowWeekend;
        state.ShowInactiveEmployees = ShowInactiveEmployees;
        UiLayoutSettings.Save(state);
    }

    private void UpdateAutoReloadTimer()
    {
        if (IsServerMode && IsAutoReloadEnabled)
            _autoReloadTimer.Start();
        else
            _autoReloadTimer.Stop();
    }

    private async void AutoReloadTimerOnTick(object? sender, EventArgs e)
    {
        if (!IsServerMode || !IsAutoReloadEnabled)
            return;

        try
        {
            await LoadDataAsync(reloadEmployees: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Automatisches Neuladen des Plans fehlgeschlagen.");
        }
    }

    private void ApplyLanguage(string cultureCode)
    {
        var culture = new CultureInfo(cultureCode);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        SelectedLanguageCode = cultureCode;
        Directory.CreateDirectory(GetAppDataFolder());
        File.WriteAllText(GetLanguageSettingsPath(), cultureCode);
    }

    [RelayCommand]
    private void BackupAsync()
    {
        try
        {
            if (_databaseOptions.IsServer)
            {
                var choice = MessageBox.Show(
                    Tr(
                        "Die gemeinsamen Daten liegen auf SQL Server. Eine echte Sicherung macht ihr in SSMS (Datenbank «Wochenplanung»).\n\nDer Knopf «Sicherung» speichert nur die lokale Datei dieses PCs – nicht den Server-Stand.\n\nTrotzdem lokale Datei speichern?",
                        "Shared data lives on SQL Server. Create a real backup in SSMS (database «Wochenplanung»).\n\nThe Backup button only saves this PC's local file — not the server copy.\n\nSave the local file anyway?",
                        "Les données partagées sont sur SQL Server. La vraie sauvegarde se fait dans SSMS (base « Wochenplanung »).\n\nLe bouton enregistre seulement le fichier local de ce PC — pas le serveur.\n\nEnregistrer le fichier local quand même ?",
                        "I dati condivisi sono su SQL Server. Il backup vero si fa in SSMS (database «Wochenplanung»).\n\nIl pulsante salva solo il file locale di questo PC — non il server.\n\nSalvare comunque il file locale?"),
                    Tr("Sicherung", "Backup", "Sauvegarde", "Backup"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (choice != MessageBoxResult.Yes)
                    return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = Tr("Datenbank (*.db)|*.db|Alle Dateien (*.*)|*.*", "Database (*.db)|*.db|All files (*.*)|*.*", "Base de données (*.db)|*.db|Tous les fichiers (*.*)|*.*", "Database (*.db)|*.db|Tutti i file (*.*)|*.*"),
                FileName = _backupService.SuggestedFileName,
                Title = Tr("Backup speichern unter", "Save backup as", "Enregistrer la sauvegarde sous", "Salva backup con nome"),
                InitialDirectory = _backupService.DefaultBackupDirectory
            };
            if (saveDialog.ShowDialog(System.Windows.Application.Current.MainWindow) != true)
                return;

            var result = _backupService.CopyLocalDatabase(saveDialog.FileName);
            if (!result.Succeeded)
            {
                MessageBox.Show(result.Error ?? Tr("Backup fehlgeschlagen.", "Backup failed.", "Échec de la sauvegarde.", "Backup non riuscito."), Tr("Backup", "Backup", "Sauvegarde", "Backup"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(
                $"{Tr("Backup erstellt", "Backup created", "Sauvegarde créée", "Backup creato")}:\n{result.DatabasePath}\n{result.TemplatesPath}",
                Tr("Backup", "Backup", "Sauvegarde", "Backup"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Backup");
            MessageBox.Show($"{Tr("Fehler", "Error", "Erreur", "Errore")}: {ex.Message}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        try
        {
            if (_databaseOptions.IsServer)
            {
                MessageBox.Show(
                    Tr(
                        "Wiederherstellen ersetzt nur die lokale SQLite-Datei, nicht SQL Server. Im Server-Modus ist das nicht vorgesehen.\n\nZum Zurückspielen einer Server-Sicherung SSMS verwenden.",
                        "Restore only replaces the local SQLite file, not SQL Server. It is not available in server mode.\n\nUse SSMS to restore a server backup.",
                        "La restauration remplace uniquement le fichier SQLite local, pas SQL Server. Ce n'est pas prévu en mode serveur.\n\nUtilisez SSMS pour restaurer une sauvegarde serveur.",
                        "Il ripristino sostituisce solo il file SQLite locale, non SQL Server. Non è previsto in modalità server.\n\nUsa SSMS per ripristinare un backup del server."),
                    Tr("Wiederherstellung", "Restore", "Restauration", "Ripristino"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                Tr("Eine Wiederherstellung ersetzt die aktuelle Datenbank. Die Anwendung wird danach neu gestartet. Fortfahren?", "Restore will replace the current database. The app will restart afterwards. Continue?", "La restauration remplacera la base actuelle. L'application redémarrera ensuite. Continuer ?", "Il ripristino sostituirà il database attuale. L'app verrà riavviata dopo. Continuare?"),
                Tr("Wiederherstellung", "Restore", "Restauration", "Ripristino"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
            var openDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Tr("Datenbank (*.db)|*.db|Alle Dateien (*.*)|*.*", "Database (*.db)|*.db|All files (*.*)|*.*", "Base de données (*.db)|*.db|Tous les fichiers (*.*)|*.*", "Database (*.db)|*.db|Tutti i file (*.*)|*.*"),
                Title = Tr("Backup-Datei (schedule.db) auswählen", "Select backup file (schedule.db)", "Sélectionner le fichier de sauvegarde (schedule.db)", "Seleziona file di backup (schedule.db)")
            };
            if (openDialog.ShowDialog(System.Windows.Application.Current.MainWindow) != true)
                return;
            _backupService.RestoreLocalDatabase(openDialog.FileName);
            MessageBox.Show(Tr("Datenbank wiederhergestellt. Die Anwendung wird neu gestartet.", "Database restored. The application will restart.", "Base de données restaurée. L'application va redémarrer.", "Database ripristinato. L'applicazione verrà riavviata."), Tr("Wiederherstellung", "Restore", "Restauration", "Ripristino"), MessageBoxButton.OK, MessageBoxImage.Information);
            System.Windows.Application.Current.Shutdown();
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei der Wiederherstellung");
            MessageBox.Show($"{Tr("Fehler", "Error", "Erreur", "Errore")}: {ex.Message}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<List<EmployeeStatRow>> GetStatisticsRowsAsync(DateTime start, DateTime end)
    {
        var tasks = await _scheduleService.GetTasksForDateRangeAsync(start, end);
        var rows = new List<EmployeeStatRow>();
        foreach (var emp in Employees.OrderBy(e => e.DisplayOrder).ThenBy(e => e.LastName))
        {
            var empTasks = tasks.Where(t => t.EmployeeId == emp.Id);
            var (days, hours) = WorkloadCalculator.ForTasks(empTasks, start, end);
            rows.Add(new EmployeeStatRow { EmployeeName = emp.FullName, TaskDays = days, TaskHours = hours });
        }
        return rows;
    }

    [RelayCommand]
    private async Task ShowStatisticsAsync()
    {
        var (start, end) = GetDefaultExportRange();
        var vm = new StatisticsViewModel
        {
            StartDate = start,
            EndDate = end
        };
        foreach (var r in await GetStatisticsRowsAsync(start, end))
            vm.Rows.Add(r);
        var dialog = new StatisticsDialog();
        dialog.SetViewModel(vm);
        dialog.ComputeStatsAsync = GetStatisticsRowsAsync;
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
    }

    private async Task<List<AbsenceReportRow>> GetAbsenceReportAsync(AbsenceReportViewModel filter)
    {
        var start = filter.StartDate.Date;
        var end = filter.EndDate.Date;
        if (end < start)
            (start, end) = (end, start);

        var tasks = await _scheduleService.GetTasksForDateRangeAsync(start, end);
        var absenceTypes = new[] { TaskType.Vacation, TaskType.Absence, TaskType.School };
        var filtered = tasks.Where(t =>
            (t.EndDate == null ? t.StartDate.Date >= start && t.StartDate.Date <= end
                : t.EndDate.Value.Date >= start && t.StartDate.Date <= end) &&
            absenceTypes.Contains(t.Type)).ToList();
        if (filter.SelectedEmployeeId is { } employeeId)
            filtered = filtered.Where(t => t.EmployeeId == employeeId).ToList();

        var typeNames = new Dictionary<TaskType, string>
        {
            { TaskType.Vacation, UiText.TaskTypeVacation },
            { TaskType.Absence, UiText.TaskTypeAbsence },
            { TaskType.School, UiText.TaskTypeSchool }
        };
        var rows = new List<AbsenceReportRow>();
        var empDict = Employees.ToDictionary(e => e.Id);
        foreach (var t in filtered.OrderBy(t => t.StartDate))
        {
            var taskStart = t.StartDate.Date < start ? start : t.StartDate.Date;
            var taskEnd = (t.EndDate ?? t.StartDate).Date > end ? end : (t.EndDate ?? t.StartDate).Date;
            for (var d = taskStart; d <= taskEnd; d = d.AddDays(1))
            {
                rows.Add(new AbsenceReportRow
                {
                    EmployeeName = empDict.TryGetValue(t.EmployeeId, out var e) ? e.FullName : "?",
                    Date = d,
                    Description = t.Description ?? "",
                    TypeDisplay = typeNames.TryGetValue(t.Type, out var name) ? name : t.Type.ToString()
                });
            }
        }
        return rows;
    }

    [RelayCommand]
    private async Task ShowAbsenceReportAsync()
    {
        var vm = new AbsenceReportViewModel
        {
            SelectedYear = SelectedDate.Year
        };
        vm.SetEmployees(Employees);
        vm.ApplyYearToRange();
        foreach (var r in await GetAbsenceReportAsync(vm))
            vm.Rows.Add(r);
        vm.NotifyTotalChanged();
        var dialog = new AbsenceReportDialog();
        dialog.SetViewModel(vm);
        dialog.ComputeReportAsync = GetAbsenceReportAsync;
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        App.ToggleTheme();
        OnPropertyChanged(nameof(IsDarkModeEnabled));
    }

    [RelayCommand]
    private void SetDarkMode()
    {
        App.SetTheme(true);
        OnPropertyChanged(nameof(IsDarkModeEnabled));
    }

    [RelayCommand]
    private void SetLightMode()
    {
        App.SetTheme(false);
        OnPropertyChanged(nameof(IsDarkModeEnabled));
    }

    [RelayCommand]
    private void SetLanguageGerman()
    {
        ApplyLanguage("de-CH");
        MessageBox.Show(Tr("Sprache auf Deutsch gesetzt. Bitte Anwendung neu starten, damit alle Texte vollständig aktualisiert werden.", "Language set to German. Please restart the app so all texts are fully refreshed.", "Langue réglée sur l'allemand. Veuillez redémarrer l'application pour mettre à jour tous les textes.", "Lingua impostata su tedesco. Riavvia l'app per aggiornare completamente i testi."), Tr("Sprache", "Language", "Langue", "Lingua"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void SetLanguageEnglish()
    {
        ApplyLanguage("en-US");
        MessageBox.Show(Tr("Sprache auf Englisch gesetzt. Bitte Anwendung neu starten, damit alle Texte vollständig aktualisiert werden.", "Language switched to English. Please restart the application so all texts are fully updated.", "Langue définie sur l'anglais. Veuillez redémarrer l'application pour mettre à jour tous les textes.", "Lingua impostata su inglese. Riavvia l'applicazione per aggiornare tutti i testi."), Tr("Sprache", "Language", "Langue", "Lingua"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void SetLanguageFrench()
    {
        ApplyLanguage("fr-FR");
        MessageBox.Show(Tr("Sprache auf Französisch gesetzt. Bitte Anwendung neu starten, damit alle Texte vollständig aktualisiert werden.", "Language set to French. Please restart the application so all texts are fully updated.", "Langue définie sur le français. Veuillez redémarrer l'application pour mettre à jour tous les textes.", "Lingua impostata su francese. Riavvia l'applicazione per aggiornare tutti i testi."), Tr("Sprache", "Language", "Langue", "Lingua"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void SetLanguageItalian()
    {
        ApplyLanguage("it-IT");
        MessageBox.Show(Tr("Sprache auf Italienisch gesetzt. Bitte Anwendung neu starten, damit alle Texte vollständig aktualisiert werden.", "Language set to Italian. Please restart the application so all texts are fully updated.", "Langue définie sur l'italien. Veuillez redémarrer l'application pour mettre à jour tous les textes.", "Lingua impostata su italiano. Riavvia l'applicazione per aggiornare tutti i testi."), Tr("Sprache", "Language", "Langue", "Lingua"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ToggleOptionsPanel()
    {
        IsOptionsPanelOpen = !IsOptionsPanelOpen;
    }

    [RelayCommand]
    private void OpenDatabaseSettings()
    {
        IsOptionsPanelOpen = false;
        var dialog = new DatabaseSettingsDialog
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dialog.ShowDialog() == true)
        {
            MessageBox.Show(
                UiText.MsgRestartAfterDatabaseChange,
                TxtDatabaseSettings,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            App.Restart();
        }
    }

    [RelayCommand]
    private async Task ImportFromCsvAsync()
    {
        try
        {
            var openDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Tr("CSV/Excel (*.csv;*.xlsx)|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx|Alle Dateien (*.*)|*.*", "CSV/Excel (*.csv;*.xlsx)|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx|All files (*.*)|*.*", "CSV/Excel (*.csv;*.xlsx)|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx|Tous les fichiers (*.*)|*.*", "CSV/Excel (*.csv;*.xlsx)|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx|Tutti i file (*.*)|*.*"),
                Title = Tr("Datei zum Importieren wählen", "Select file to import", "Sélectionner le fichier à importer", "Seleziona file da importare")
            };
            if (openDialog.ShowDialog(System.Windows.Application.Current.MainWindow) != true)
                return;

            var rows = await ReadImportRowsAsync(openDialog.FileName);
            if (rows.Count == 0)
            {
                MessageBox.Show(Tr("Die Datei enthält keine importierbaren Zeilen.", "The file contains no importable rows.", "Le fichier ne contient aucune ligne importable.", "Il file non contiene righe importabili."), Tr("Import", "Import", "Import", "Importa"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var headers = rows[0].Keys.ToList();
            var employeeHeader = ResolveHeader(headers, "mitarbeiter", "employee", "name", "person");
            var dateHeader = ResolveHeader(headers, "datum", "date", "tag");
            var descriptionHeader = ResolveHeader(headers, "beschreibung", "description", "aufgabe", "task", "titel");
            var mappingVm = new ImportMappingViewModel(headers, employeeHeader, dateHeader, descriptionHeader, rows.Count);
            var mappingDialog = new ImportMappingDialog();
            mappingDialog.SetViewModel(mappingVm);
            mappingDialog.Owner = System.Windows.Application.Current.MainWindow;
            if (mappingDialog.ShowDialog() != true)
                return;

            employeeHeader = mappingVm.EmployeeColumn;
            dateHeader = mappingVm.DateColumn;
            descriptionHeader = mappingVm.DescriptionColumn;
            if (employeeHeader == null || dateHeader == null || descriptionHeader == null)
                return;

            var empByName = Employees.ToDictionary(e => e.FullName.Trim(), StringComparer.OrdinalIgnoreCase);
            int added = 0, skipped = 0;
            foreach (var row in rows)
            {
                var name = (row.GetValueOrDefault(employeeHeader) ?? string.Empty).Trim();
                var dateRaw = (row.GetValueOrDefault(dateHeader) ?? string.Empty).Trim();
                var desc = (row.GetValueOrDefault(descriptionHeader) ?? string.Empty).Trim();
                if (!TryParseImportDate(dateRaw, out var date))
                { skipped++; continue; }
                if (string.IsNullOrEmpty(desc)) { skipped++; continue; }
                if (!empByName.TryGetValue(name, out var emp)) { skipped++; continue; }
                var task = new WorkTask
                {
                    EmployeeId = emp.Id,
                    Description = desc,
                    StartDate = date.Date,
                    EndDate = null,
                    Type = TaskType.Work,
                    Color = TaskColor.Orange
                };
                await _scheduleService.CreateTaskAsync(task);
                added++;
            }
            await LoadDataAsync();
            MessageBox.Show(
                string.Format(
                    Tr("Import abgeschlossen: {0} Aufgabe(n) angelegt, {1} Zeile(n) übersprungen.\n\nVerwendetes Mapping:\nMitarbeiter={2}\nDatum={3}\nBeschreibung={4}", "Import completed: {0} task(s) created, {1} row(s) skipped.\n\nMapping used:\nEmployee={2}\nDate={3}\nDescription={4}", "Import terminé : {0} tâche(s) créée(s), {1} ligne(s) ignorée(s).\n\nMapping utilisé :\nEmployé={2}\nDate={3}\nDescription={4}", "Import completato: {0} attività creata/e, {1} riga/e saltata/e.\n\nMapping usato:\nDipendente={2}\nData={3}\nDescrizione={4}"),
                    added, skipped, employeeHeader, dateHeader, descriptionHeader),
                Tr("Import", "Import", "Import", "Importa"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim CSV-Import");
            MessageBox.Show($"{Tr("Fehler", "Error", "Erreur", "Errore")}: {ex.Message}", Tr("Fehler", "Error", "Erreur", "Errore"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? ResolveHeader(IEnumerable<string> headers, params string[] aliases)
    {
        var normalized = headers
            .Select(h => new { Original = h, Key = NormalizeHeader(h) })
            .ToList();

        foreach (var alias in aliases.Select(NormalizeHeader))
        {
            var exact = normalized.FirstOrDefault(h => h.Key == alias);
            if (exact != null)
                return exact.Original;

            var contains = normalized.FirstOrDefault(h => h.Key.Contains(alias, StringComparison.OrdinalIgnoreCase));
            if (contains != null)
                return contains.Original;
        }

        return null;
    }

    private static string NormalizeHeader(string value)
    {
        return new string(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static bool TryParseImportDate(string raw, out DateTime date)
    {
        var cultures = new[] { CultureInfo.GetCultureInfo("de-CH"), CultureInfo.GetCultureInfo("de-DE"), CultureInfo.InvariantCulture };
        foreach (var culture in cultures)
        {
            if (DateTime.TryParse(raw, culture, DateTimeStyles.None, out date))
            {
                date = date.Date;
                return true;
            }
        }

        date = default;
        return false;
    }

    private static async Task<List<Dictionary<string, string>>> ReadImportRowsAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".xlsx")
            return await Task.FromResult(ReadExcelRows(filePath));

        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length < 2)
            return new List<Dictionary<string, string>>();

        var separator = lines[0].Count(c => c == ';') >= lines[0].Count(c => c == ',') ? ';' : ',';
        var headers = ParseCsvLine(lines[0], separator);
        var rows = new List<Dictionary<string, string>>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var values = ParseCsvLine(lines[i], separator);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int h = 0; h < headers.Count; h++)
            {
                var header = headers[h].Trim();
                if (string.IsNullOrWhiteSpace(header))
                    continue;
                row[header] = h < values.Count ? values[h].Trim() : string.Empty;
            }
            if (row.Count > 0)
                rows.Add(row);
        }

        return rows;
    }

    private static List<Dictionary<string, string>> ReadExcelRows(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();
        var firstRow = sheet.FirstRowUsed();
        if (firstRow == null)
            return new List<Dictionary<string, string>>();

        var headerCells = firstRow.CellsUsed().ToList();
        var headers = headerCells.Select(c => c.GetString().Trim()).ToList();
        var startRow = firstRow.RowNumber() + 1;
        var endRow = sheet.LastRowUsed()?.RowNumber() ?? startRow - 1;
        var rows = new List<Dictionary<string, string>>();
        for (int r = startRow; r <= endRow; r++)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Count; c++)
            {
                if (string.IsNullOrWhiteSpace(headers[c]))
                    continue;
                var cell = sheet.Cell(r, c + 1);
                row[headers[c]] = cell.GetFormattedString().Trim();
            }
            if (row.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                rows.Add(row);
        }

        return rows;
    }

    private static List<string> ParseCsvLine(string line, char separator)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == separator && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }
        values.Add(current.ToString());
        return values;
    }
}
