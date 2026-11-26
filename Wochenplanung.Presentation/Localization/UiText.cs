namespace Wochenplanung.Presentation.Localization;

public static class UiText
{
    public static string Tr(string de, string en, string fr, string it) =>
        UiCatalog.Get(de, en, fr, it);

    public static string FilterAll => Tr("(Alle)", "(All)", "(Tous)", "(Tutti)");
    public static string PickDate => Tr("Datum wählen", "Pick date", "Choisir une date", "Scegli data");
    public static string ActionPrint => Tr("Drucken", "Print", "Imprimer", "Stampa");
    public static string PrintPreviewTitle => Tr("Druckvorschau", "Print preview", "Aperçu avant impression", "Anteprima di stampa");
    public static string AllEmployees => Tr("Alle Mitarbeiter", "All employees", "Tous les employés", "Tutti i dipendenti");
    public static string LabelYearMode => Tr("Jahr", "Year", "Année", "Anno");
    public static string LabelRangeMode => Tr("Zeitraum", "Date range", "Période", "Intervallo");
    public static string AbsenceTotalFormat => Tr("Gesamt: {0} Tag(e)", "Total: {0} day(s)", "Total : {0} jour(s)", "Totale: {0} giorno/i");
    public static string SwissHolidaysHint => Tr(
        "Jahr wählen, dann Feiertage ankreuzen. Erneuter Klick auf die Buttons hebt die Auswahl wieder auf.",
        "Pick a year, then tick holidays. Click the buttons again to clear the selection.",
        "Choisir une année, puis cocher les jours fériés. Un second clic désélectionne.",
        "Scegli un anno, poi seleziona le festività. Un secondo clic deseleziona.");
    public static string LabelPrinter => Tr("Drucker", "Printer", "Imprimante", "Stampante");
    public static string LabelOrientation => Tr("Ausrichtung", "Orientation", "Orientation", "Orientamento");
    public static string OrientationLandscape => Tr("Querformat", "Landscape", "Paysage", "Orizzontale");
    public static string OrientationPortrait => Tr("Hochformat", "Portrait", "Portrait", "Verticale");
    public static string LabelCopies => Tr("Kopien", "Copies", "Copies", "Copie");
    public static string LabelColorMode => Tr("Farbmodus", "Color mode", "Mode couleur", "Modalità colore");
    public static string ColorModeColor => Tr("Farbe", "Color", "Couleur", "Colore");
    public static string ColorModeGrayscale => Tr("Graustufen", "Grayscale", "Niveaux de gris", "Scala di grigi");
    public static string ActionMorePrintSettings => Tr("Weitere Einstellungen", "More settings", "Autres paramètres", "Altre impostazioni");
    public static string NoPrintersFound => Tr("Kein Drucker gefunden.", "No printer found.", "Aucune imprimante trouvée.", "Nessuna stampante trovata.");

    public static string CompanyVacationTitle => Tr("Ferien eintragen", "Add vacation", "Saisir les vacances", "Inserisci ferie");
    public static string CompanyVacationIntro => Tr(
        "Betriebsferien oder gemeinsame Ferien für alle Mitarbeiter eintragen.",
        "Add company vacation or shared time off for all employees.",
        "Saisir les vacances d'entreprise pour tous les employés.",
        "Inserisci ferie aziendali per tutti i dipendenti.");
    public static string LabelCompanyVacationName => Tr("Bezeichnung (z. B. Betriebsferien)", "Label (e.g. company vacation)", "Intitulé (p.ex. vacances d'entreprise)", "Etichetta (es. ferie aziendali)");
    public static string DefaultCompanyVacationName => Tr("Betriebsferien", "Company vacation", "Vacances d'entreprise", "Ferie aziendali");

    public static string TaskTypeWork => Tr("Arbeit", "Work", "Travail", "Lavoro");
    public static string TaskTypeAbsence => Tr("Abwesenheit", "Absence", "Absence", "Assenza");
    public static string TaskTypeSchool => Tr("Fortbildung", "Training", "Formation", "Formazione");
    public static string TaskTypeVacation => Tr("Ferien", "Vacation", "Vacances", "Ferie");
    public static string TaskTypeOther => Tr("Sonstiges", "Other", "Autre", "Altro");

    public static string TaskColorOrange => Tr("Orange", "Orange", "Orange", "Arancione");
    public static string TaskColorGreen => Tr("Grün", "Green", "Vert", "Verde");
    public static string TaskColorBlue => Tr("Blau", "Blue", "Bleu", "Blu");
    public static string TaskColorBeige => Tr("Beige", "Beige", "Beige", "Beige");
    public static string TaskColorYellow => Tr("Gelb", "Yellow", "Jaune", "Giallo");
    public static string TaskColorRed => Tr("Rot", "Red", "Rouge", "Rosso");

    public static string TaskColorName(Wochenplanung.Domain.Entities.TaskColor color) => color switch
    {
        Wochenplanung.Domain.Entities.TaskColor.Orange => TaskColorOrange,
        Wochenplanung.Domain.Entities.TaskColor.Green => TaskColorGreen,
        Wochenplanung.Domain.Entities.TaskColor.Blue => TaskColorBlue,
        Wochenplanung.Domain.Entities.TaskColor.Beige => TaskColorBeige,
        Wochenplanung.Domain.Entities.TaskColor.Yellow => TaskColorYellow,
        Wochenplanung.Domain.Entities.TaskColor.Red => TaskColorRed,
        _ => color.ToString()
    };

    public static string TaskTypeName(Wochenplanung.Domain.Entities.TaskType type) => type switch
    {
        Wochenplanung.Domain.Entities.TaskType.Work => TaskTypeWork,
        Wochenplanung.Domain.Entities.TaskType.Absence => TaskTypeAbsence,
        Wochenplanung.Domain.Entities.TaskType.School => TaskTypeSchool,
        Wochenplanung.Domain.Entities.TaskType.Vacation => TaskTypeVacation,
        Wochenplanung.Domain.Entities.TaskType.Other => TaskTypeOther,
        _ => type.ToString()
    };

    public static bool TryParseTaskType(string value, out Wochenplanung.Domain.Entities.TaskType type)
    {
        var key = value.Trim();
        if (key is "Arbeit" or "Work" or "Travail" or "Lavoro") { type = Wochenplanung.Domain.Entities.TaskType.Work; return true; }
        if (key is "Abwesenheit" or "Absence" or "Assenza") { type = Wochenplanung.Domain.Entities.TaskType.Absence; return true; }
        if (key is "Fortbildung" or "Training" or "Formation" or "Formazione" or "Schule") { type = Wochenplanung.Domain.Entities.TaskType.School; return true; }
        if (key is "Ferien" or "Urlaub" or "Vacation" or "Vacances" or "Ferie") { type = Wochenplanung.Domain.Entities.TaskType.Vacation; return true; }
        if (key is "Sonstiges" or "Other" or "Autre" or "Altro") { type = Wochenplanung.Domain.Entities.TaskType.Other; return true; }
        type = default;
        return false;
    }

    public static string ActionToday => Tr("Heute", "Today", "Aujourd'hui", "Oggi");
    public static string PrintViewNotReady => Tr(
        "Die Ansicht ist noch nicht bereit zum Drucken.",
        "The view is not ready to print yet.",
        "La vue n'est pas encore prête pour l'impression.",
        "La vista non è ancora pronta per la stampa.");

    public static string DialogCancel => Tr("Abbrechen", "Cancel", "Annuler", "Annulla");
    public static string DialogSave => Tr("Speichern", "Save", "Enregistrer", "Salva");
    public static string DialogClose => Tr("Schließen", "Close", "Fermer", "Chiudi");
    public static string LabelFrom => Tr("Von", "From", "De", "Da");
    public static string LabelTo => Tr("Bis", "To", "À", "A");
    public static string CaptionHint => Tr("Hinweis", "Note", "Information", "Avviso");
    public static string CaptionImport => Tr("Import", "Import", "Import", "Importa");
    public static string CaptionInvalidRange => Tr("Ungültiger Zeitraum", "Invalid range", "Période non valide", "Intervallo non valido");

    public static string TaskDialogTitle => Tr("Aufgabe", "Task", "Tâche", "Attività");
    public static string LabelEmployee => Tr("Mitarbeiter", "Employee", "Employé", "Dipendente");
    public static string LabelDescription => Tr("Beschreibung", "Description", "Description", "Descrizione");
    public static string LabelMultiDayTask => Tr("Mehrfachtages-Aufgabe", "Multi-day task", "Tâche sur plusieurs jours", "Attività multi-giorno");
    public static string LabelStartDate => Tr("Startdatum", "Start date", "Date de début", "Data di inizio");
    public static string LabelEndDate => Tr("Enddatum", "End date", "Date de fin", "Data di fine");
    public static string LabelStartTimeOptional => Tr(
        "Startzeit (optional, z. B. 10:00 oder 1000)",
        "Start time (optional, e.g. 10:00 or 1000)",
        "Heure de début (optionnel, p.ex. 10:00 ou 1000)",
        "Ora inizio (opzionale, es. 10:00 o 1000)");
    public static string LabelEndTimeOptional => Tr(
        "Endzeit (optional, z. B. 16:00 oder 1600) – sonst 8 Stunden",
        "End time (optional, e.g. 16:00 or 1600) – otherwise 8 hours",
        "Heure de fin (optionnel, p.ex. 16:00 ou 1600) – sinon 8 heures",
        "Ora fine (opzionale, es. 16:00 o 1600) – altrimenti 8 ore");
    public static string LabelLocationOptional => Tr("Ort (optional)", "Location (optional)", "Lieu (optionnel)", "Luogo (opzionale)");
    public static string LabelProjectOptional => Tr("Projekt (optional)", "Project (optional)", "Projet (optionnel)", "Progetto (opzionale)");
    public static string LabelTaskType => Tr("Aufgabentyp", "Task type", "Type de tâche", "Tipo attività");
    public static string LabelColor => Tr("Farbe", "Color", "Couleur", "Colore");
    public static string LabelNotesOptional => Tr("Notizen (optional)", "Notes (optional)", "Notes (optionnel)", "Note (opzionale)");
    public static string LabelRecurrence => Tr("Wiederholung", "Recurrence", "Répétition", "Ripetizione");
    public static string LabelSeriesScope => Tr("Änderung gilt für", "Change applies to", "La modification s'applique à", "La modifica vale per");
    public static string SeriesThisOnly => Tr("Nur diese Aufgabe", "This task only", "Cette tâche uniquement", "Solo questa attività");
    public static string SeriesThisAndFuture => Tr("Diese und alle folgenden", "This and following", "Celle-ci et les suivantes", "Questa e le successive");
    public static string SeriesEntire => Tr("Ganze Serie", "Entire series", "Toute la série", "Tutta la serie");
    public static string SeriesEditScopeName(Wochenplanung.Presentation.ViewModels.SeriesEditScope scope) => scope switch
    {
        Wochenplanung.Presentation.ViewModels.SeriesEditScope.ThisOnly => SeriesThisOnly,
        Wochenplanung.Presentation.ViewModels.SeriesEditScope.ThisAndFuture => SeriesThisAndFuture,
        Wochenplanung.Presentation.ViewModels.SeriesEditScope.EntireSeries => SeriesEntire,
        _ => scope.ToString()
    };
    public static string LabelEmployeeActive => Tr("Mitarbeiter ist aktiv (sichtbar im Plan)", "Employee is active (visible in the plan)", "L'employé est actif (visible dans le plan)", "Il dipendente è attivo (visibile nel piano)");
    public static string RecurrenceNone => Tr("Keine", "None", "Aucune", "Nessuna");
    public static string RecurrenceWeekly => Tr("Wöchentlich", "Weekly", "Hebdomadaire", "Settimanale");
    public static string RecurrenceMonthly => Tr("Monatlich", "Monthly", "Mensuel", "Mensile");
    public static string RecurrencePatternName(Wochenplanung.Presentation.ViewModels.RecurrencePattern pattern) => pattern switch
    {
        Wochenplanung.Presentation.ViewModels.RecurrencePattern.None => RecurrenceNone,
        Wochenplanung.Presentation.ViewModels.RecurrencePattern.Weekly => RecurrenceWeekly,
        Wochenplanung.Presentation.ViewModels.RecurrencePattern.Monthly => RecurrenceMonthly,
        _ => pattern.ToString()
    };
    public static string LabelRepeatCount => Tr("Anzahl Termine", "Number of occurrences", "Nombre d'occurrences", "Numero ricorrenze");
    public static string MsgSelectEmployee => Tr("Bitte wählen Sie einen Mitarbeiter.", "Please select an employee.", "Veuillez sélectionner un employé.", "Seleziona un dipendente.");
    public static string MsgEnterDescription => Tr("Bitte geben Sie eine Beschreibung ein.", "Please enter a description.", "Veuillez saisir une description.", "Inserisci una descrizione.");
    public static string MsgRepeatCountMinOne => Tr("Die Anzahl der Wiederholungen muss mindestens 1 sein.", "The number of occurrences must be at least 1.", "Le nombre d'occurrences doit être au moins de 1.", "Il numero di ricorrenze deve essere almeno 1.");

    public static string EmployeeDialogTitle => Tr("Mitarbeiter", "Employee", "Employé", "Dipendente");
    public static string LabelEmployeeNumberOptional => Tr("Mitarbeiter-Nr. (optional)", "Employee no. (optional)", "N° employé (optionnel)", "N. dipendente (opzionale)");
    public static string LabelFirstName => Tr("Vorname", "First name", "Prénom", "Nome");
    public static string LabelLastName => Tr("Nachname", "Last name", "Nom", "Cognome");
    public static string LabelRoleOptional => Tr("Rolle (optional)", "Role (optional)", "Rôle (optionnel)", "Ruolo (opzionale)");
    public static string MsgFillFirstLastName => Tr("Bitte Vorname und Nachname ausfüllen.", "Please fill in first and last name.", "Veuillez remplir le prénom et le nom.", "Compila nome e cognome.");

    public static string HolidayDialogTitle => Tr("Feiertag eintragen", "Add holiday", "Ajouter un jour férié", "Aggiungi festività");
    public static string HolidayDialogIntro => Tr("Feiertag oder freier Tag für alle Mitarbeiter eintragen", "Add a holiday or day off for all employees", "Ajouter un jour férié ou libre pour tous les employés", "Aggiungi una festività o giorno libero per tutti i dipendenti");
    public static string LabelHolidayName => Tr("Bezeichnung (z. B. Weihnachten, Brückentag)", "Label (e.g. Christmas, bridge day)", "Intitulé (p.ex. Noël, pont)", "Etichetta (es. Natale, ponte)");
    public static string LabelHolidayRange => Tr("Zeitraum (mehrere Tage)", "Range (multiple days)", "Période (plusieurs jours)", "Intervallo (più giorni)");
    public static string LabelSwissHolidays => Tr("Oder Schweizer Feiertage vorausfüllen", "Or prefill Swiss public holidays", "Ou préremplir les jours fériés suisses", "Oppure precompila le festività svizzere");
    public static string LabelYear => Tr("Jahr", "Year", "Année", "Anno");
    public static string ActionSwissFederal => Tr("Nur eidgenössische", "Federal only", "Fédéraux seulement", "Solo federali");
    public static string ActionSwissSolothurn => Tr("Solothurn", "Solothurn", "Soleure", "Soletta");
    public static string ActionSwissAll => Tr("Alle anwählen", "Select all", "Tout sélectionner", "Seleziona tutti");
    public static string ActionApply => Tr("Eintragen", "Apply", "Valider", "Applica");
    public static string LabelLastChanged => Tr("Zuletzt geändert", "Last changed", "Dernière modification", "Ultima modifica");
    public static string DatabasePasswordHint => Tr(
        "Windows-Anmeldung ist sicherer. Ein SQL-Passwort wird auf diesem PC verschlüsselt gespeichert.",
        "Windows authentication is safer. A SQL password is stored encrypted on this PC.",
        "L'authentification Windows est plus sûre. Un mot de passe SQL est stocké chiffré sur ce PC.",
        "L'autenticazione Windows è più sicura. Una password SQL viene salvata cifrata su questo PC.");
    public static string MsgEnterHolidayName => Tr("Bitte geben Sie eine Bezeichnung ein oder wählen Sie Feiertage.", "Please enter a label or select holidays.", "Veuillez saisir un intitulé ou choisir des jours fériés.", "Inserisci un'etichetta o seleziona festività.");
    public static string MsgEndBeforeStart => Tr("Das Enddatum darf nicht vor dem Startdatum liegen.", "The end date cannot be before the start date.", "La date de fin ne peut pas être antérieure à la date de début.", "La data di fine non può essere precedente alla data di inizio.");

    public static string ExportRangeDialogTitle => Tr("Export - Zeitraum wählen", "Export - select range", "Export - choisir la période", "Esporta - scegli intervallo");
    public static string ExportRangeIntro => Tr("Zeitraum für den Report festlegen", "Define range for the report", "Définir la période du rapport", "Definisci intervallo per il report");
    public static string LabelExportFontSize => Tr("Schriftgröße (Export)", "Font size (export)", "Taille de police (export)", "Dimensione font (export)");
    public static string ExportRangeHint => Tr("Hinweis: Enddatum darf nicht vor Startdatum liegen.", "Note: end date cannot be before start date.", "Remarque: la date de fin ne peut pas être avant la date de début.", "Nota: la data di fine non può precedere la data di inizio.");
    public static string ActionExport => Tr("Export", "Export", "Exporter", "Esporta");

    public static string ImportMappingDialogTitle => Tr("Import-Mapping-Assistent", "Import mapping assistant", "Assistant de mapping d'import", "Assistente mapping import");
    public static string ImportMappingIntro => Tr("Spalten zuordnen", "Map columns", "Mapper les colonnes", "Mappa colonne");
    public static string ImportMappingRowsDetectedFormat => Tr("{0} Datenzeilen erkannt.", "{0} data rows detected.", "{0} lignes de données détectées.", "{0} righe dati rilevate.");
    public static string LabelEmployeeColumn => Tr("Mitarbeiter-Spalte", "Employee column", "Colonne employé", "Colonna dipendente");
    public static string LabelDateColumn => Tr("Datum-Spalte", "Date column", "Colonne date", "Colonna data");
    public static string LabelDescriptionColumn => Tr("Beschreibung-Spalte", "Description column", "Colonne description", "Colonna descrizione");
    public static string ActionStartImport => Tr("Import starten", "Start import", "Démarrer l'import", "Avvia import");
    public static string MsgMapAllRequiredColumns => Tr("Bitte alle drei Pflichtspalten zuordnen.", "Please map all three required columns.", "Veuillez associer les trois colonnes obligatoires.", "Associa tutte e tre le colonne obbligatorie.");

    public static string TaskTemplateDialogTitle => Tr("Standard-Aufgabe (Vorlage)", "Default task (template)", "Tâche standard (modèle)", "Attività standard (modello)");

    public static string StatisticsDialogTitle => Tr("Statistik / Auslastung", "Statistics / workload", "Statistiques / charge", "Statistiche / carico");
    public static string StatisticsIntro => Tr(
        "Arbeitstage je Mitarbeiter im gewählten Zeitraum. Stunden: hinterlegte Zeiten, sonst 8 Stunden pro Arbeitstag. Ferien, Abwesenheit und Fortbildung zählen nicht.",
        "Workdays per employee in the selected range. Hours: entered times, otherwise 8 hours per workday. Vacation, absence and training are excluded.",
        "Jours travaillés par employé sur la période. Heures : horaires saisis, sinon 8 h par jour. Vacances, absences et formation sont exclus.",
        "Giorni lavorativi per dipendente nell'intervallo. Ore: orari inseriti, altrimenti 8 ore al giorno. Ferie, assenze e formazione esclusi.");
    public static string ActionRefreshStatistics => Tr("Statistik aktualisieren", "Refresh statistics", "Actualiser les statistiques", "Aggiorna statistiche");
    public static string ColEmployee => Tr("Mitarbeiter", "Employee", "Employé", "Dipendente");
    public static string ColDays => Tr("Tage", "Days", "Jours", "Giorni");
    public static string ColHoursApprox => Tr("Stunden (ca.)", "Hours (approx.)", "Heures (env.)", "Ore (circa)");

    public static string AbsenceDialogTitle => Tr("Ferien", "Vacation", "Vacances", "Ferie");
    public static string AbsenceIntro => Tr(
        "Ferien und Abwesenheiten. Mitarbeiter und Jahr wählen – oder einen eigenen Zeitraum.",
        "Vacation and absences. Pick an employee and year — or a custom range.",
        "Vacances et absences. Choisir un employé et une année — ou une période.",
        "Ferie e assenze. Scegli dipendente e anno — oppure un intervallo.");
    public static string ActionRefresh => Tr("Aktualisieren", "Refresh", "Actualiser", "Aggiorna");
    public static string ColDate => Tr("Datum", "Date", "Date", "Data");
    public static string ColType => Tr("Art", "Type", "Type", "Tipo");

    public static string MsgNo => Tr("Nein", "No", "Non", "No");
    public static string MsgYes => Tr("Ja", "Yes", "Oui", "Sì");
    public static string MsgOk => Tr("OK", "OK", "OK", "OK");

    public static string DatabaseDialogTitle => Tr("Datenbank / Server", "Database / Server", "Base de données / Serveur", "Database / Server");
    public static string DatabaseDialogIntro => Tr(
        "Lokal speichert jeder PC eigene Daten. Mit SQL Server auf eurem Büroserver arbeiten alle mit demselben Plan. Bestehende lokale Daten können einmalig auf den Server kopiert werden.",
        "Local mode keeps data on this PC. With SQL Server on your office server everyone shares the same schedule. Existing local data can be copied to the server once.",
        "En local, chaque PC a ses propres données. Avec SQL Server, tout le monde partage le même planning. Les données locales existantes peuvent être copiées une fois vers le serveur.",
        "In locale ogni PC ha i propri dati. Con SQL Server tutti condividono lo stesso piano. I dati locali esistenti possono essere copiati una volta sul server.");
    public static string DatabaseModeLocal => Tr("Dieser PC (lokal, nur ich)", "This PC (local, only me)", "Ce PC (local, moi uniquement)", "Questo PC (locale, solo io)");
    public static string DatabaseModeServer => Tr("SQL Server (gemeinsam im Büro)", "SQL Server (shared in the office)", "SQL Server (partagé au bureau)", "SQL Server (condiviso in ufficio)");
    public static string LabelDatabaseServer => Tr("Server (z. B. BUEROSERVER\\SQLEXPRESS)", "Server (e.g. OFFICE-PC\\SQLEXPRESS)", "Serveur (p.ex. SERVEUR\\SQLEXPRESS)", "Server (es. SERVER\\SQLEXPRESS)");
    public static string LabelDatabaseName => Tr("Datenbankname", "Database name", "Nom de la base", "Nome database");
    public static string LabelWindowsAuth => Tr("Windows-Anmeldung (empfohlen)", "Windows authentication (recommended)", "Authentification Windows (recommandé)", "Autenticazione Windows (consigliata)");
    public static string LabelSqlUser => Tr("SQL-Benutzer", "SQL user", "Utilisateur SQL", "Utente SQL");
    public static string LabelSqlPassword => Tr("Passwort", "Password", "Mot de passe", "Password");
    public static string ActionTestConnection => Tr("Verbindung testen", "Test connection", "Tester la connexion", "Prova connessione");
    public static string MsgRestartAfterDatabaseChange => Tr(
        "Die Datenbank-Einstellung wurde gespeichert. Die App wird neu gestartet.",
        "The database setting was saved. The app will restart.",
        "Le paramètre de base a été enregistré. L'application va redémarrer.",
        "L'impostazione del database è stata salvata. L'app verrà riavviata.");
    public static string LabelCopyLocalData => Tr(
        "Lokale Daten auf den Server kopieren (Mitarbeiter, Aufgaben, Vorlagen)",
        "Copy local data to the server (employees, tasks, templates)",
        "Copier les données locales vers le serveur (employés, tâches, modèles)",
        "Copia i dati locali sul server (dipendenti, attività, modelli)");
    public static string CaptionImportLocalData => Tr("Daten übernehmen", "Import data", "Reprendre les données", "Importa dati");
    public static string MsgImportLocalDataQuestion => Tr(
        "Auf diesem PC gibt es bereits lokale Daten ({0} Mitarbeiter, {1} Aufgaben).\n\nSollen sie jetzt auf den SQL Server kopiert werden?\nDie lokale Datei bleibt als Sicherung erhalten.",
        "This PC already has local data ({0} employees, {1} tasks).\n\nCopy them to SQL Server now?\nThe local file is kept as a backup.",
        "Ce PC contient déjà des données locales ({0} employés, {1} tâches).\n\nLes copier maintenant vers SQL Server ?\nLe fichier local est conservé comme sauvegarde.",
        "Questo PC ha già dati locali ({0} dipendenti, {1} attività).\n\nCopiarli ora su SQL Server?\nIl file locale resta come copia di sicurezza.");
    public static string MsgImportLocalDataDone => Tr(
        "Übernommen: {0} Mitarbeiter, {1} Aufgaben, {2} Vorlagen.",
        "Copied: {0} employees, {1} tasks, {2} templates.",
        "Repris : {0} employés, {1} tâches, {2} modèles.",
        "Importati: {0} dipendenti, {1} attività, {2} modelli.");
    public static string MsgImportLocalDataServerNotEmpty => Tr(
        "Der Server hat schon Daten. Es wurde nichts kopiert, damit nichts überschrieben wird.",
        "The server already has data. Nothing was copied so existing records are not overwritten.",
        "Le serveur contient déjà des données. Rien n'a été copié pour éviter d'écraser l'existant.",
        "Il server ha già dei dati. Non è stato copiato nulla per non sovrascrivere.");
    public static string MsgImportLocalDataFailed => Tr(
        "Die lokalen Daten konnten nicht auf den Server kopiert werden.",
        "Local data could not be copied to the server.",
        "Les données locales n'ont pas pu être copiées vers le serveur.",
        "I dati locali non possono essere copiati sul server.");
    public static string MsgConcurrencyConflict => Tr(
        "Jemand anderes hat denselben Eintrag bereits geändert. Der Plan wird neu geladen.",
        "Someone else already changed this entry. The schedule will reload.",
        "Quelqu'un d'autre a déjà modifié cette entrée. Le planning va être rechargé.",
        "Qualcun altro ha già modificato questa voce. Il piano verrà ricaricato.");
    public static string SearchDialogTitle => Tr("Plan durchsuchen", "Search the plan", "Rechercher le plan", "Cerca nel piano");
    public static string SearchDialogHint => Tr(
        "Sucht in allen Wochen: Beschreibung, Ort, Projekt, Notizen. Doppelklick springt zur Woche.",
        "Searches all weeks: description, location, project, notes. Double-click jumps to that week.",
        "Recherche dans toutes les semaines. Double-clic pour ouvrir la semaine.",
        "Cerca in tutte le settimane. Doppio clic per aprire la settimana.");
    public static string SearchButton => Tr("Suchen", "Search", "Rechercher", "Cerca");
    public static string SearchJump => Tr("Zur Woche", "Go to week", "Aller à la semaine", "Vai alla settimana");
}
