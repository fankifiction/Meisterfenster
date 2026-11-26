# Meisterfenster 2

Neues GitHub-Projekt auf Basis von Wochenplanung / Meisterfenster 1.4.7. Die Büro-Linie 1.4.x bleibt unangetastet (`fankifiction/Wochenplanung`). Dieses Repo installiert nach `Programme\Meisterfenster2` und speichert lokale Daten unter `%LOCALAPPDATA%\Meisterfenster`.

Eine moderne WPF-Anwendung zur Verwaltung von Wochenplänen.

## Neu in 2.0.0

- Tabellen immer in `dbo`; Windows-Benutzer bekommt `DEFAULT_SCHEMA = dbo`
- Wochenansicht standardmässig Mo–Fr (Sa/So im Hamburger zuschaltbar)
- Warnung und lokale Kopie, wenn die EXE von einer Freigabe startet
- Mitarbeiter ausblenden statt löschen; ausgeblendete optional wieder einblenden
- Suche über alle Wochen
- Wiederholungen als Serie (nur diese / ab hier / ganze Serie)
- Endzeit im Aufgabendialog; Stundenrechnung nutzt echte Zeiten, sonst 8 h
- SQL-Rollen `meisterfenster_planen` und `meisterfenster_lesen` (`installer/sql-rollen.sql`)
- Vorlagen werden gemerged, nicht mehr komplett gelöscht und neu geschrieben
- Plan im Büro-WLAN als Nur-Lesen-Seite (Button «Plan im WLAN», Port 5088)
- GitHub-Release mit Setup bei Tag `v*`
- Schweizer Feiertage in der Tageskopfzeile; Farbpunkte in der Farbwahl

## Neueste Updates (Meisterfenster v1.4.7)

- Farben im Aufgabendialog auf Deutsch (Gelb statt Yellow)
- Heutiger Tag: ganze Spalte hellrot, Kopfzeile fluchtet mit den Zellen

## Neueste Updates (Meisterfenster v1.4.6)

- Navigation: «Vorherige Woche» steht direkt links neben «Heute»
- Titel überdeckt die Navigationsbuttons nicht mehr
- Rechtsklick auf eine Aufgabe: «Zu Favoriten hinzufügen» (Vorlage links oben)
- Standard-Aufgaben: Markierung klarer, Doppelklick öffnet das Aufgabenfenster
- «Aktualisieren» und Auto-Reload holen die Vorlagen vom Server neu
- Startzeit akzeptiert `1000` und `9:30`; Wiederholung zeigt «Keine» statt `None`

## Neueste Updates (Meisterfenster v1.4.5)

- Start gegen SQL Server mit kurzem Timeout und klarer Fehlermeldung statt endlosem Ladekreis
- EXE nicht von einer Netzfreigabe starten; `Meisterfenster-Starten.bat` kopiert lokal
- SQL-Rechte: genau der Anmeldename der App braucht `db_owner` auf `Wochenplanung` (Gruppe allein reicht oft nicht)
- Schema-Nachzug schluckt fehlende ALTER/CREATE-Rechte und startet weiter, wenn die Spalten schon da sind
- Logs: `%LOCALAPPDATA%\Wochenplanung\logs\` (`startup.log`, `app-JJJJMMTT.log`)

## Neueste UI-Updates (Meisterfenster v1.4.4)

- PDF- und Excel-Export zeigen alle Aufgaben, ohne abgeschnittenen Text
- Tagesköpfe bleiben im Plan beim Scrollen oben; Excel friert die Kopfzeilen ein

## Neueste UI-Updates (Meisterfenster v1.4.3)

- Wochenplan bleibt nach dem Verkleinern/Maximieren des Fensters ausgerichtet
- Aufgaben einer Person zuordnen ohne Riesenkarte und ohne Ruckeln

## Neueste UI-Updates (Meisterfenster v1.4.2)

- Planzeilen bleiben über die Woche gerade: gleiche Tätigkeit auf gleicher Höhe, auch bei umbrochenem Text
- Kopieren-/Verlängern-Icons sitzen fest auf der Karte, ohne die Zeile zu verschieben

## Neueste UI-Updates (Meisterfenster v1.4.1)

- Button-Sichtbarkeit im Hamburger-Menü bleibt nach dem Schliessen erhalten
- Im Server-Modus: Plan alle 90 Sekunden neu laden; Sicherung erklärt, dass die Datei lokal ist, nicht SQL Server
- Solothurner Feiertage im Dialog (Fronleichnam, Auffahrt bleibt eidgenössisch, Maria Himmelfahrt, Allerheiligen, Maria Empfängnis)
- Druckvorschau mit Drucker/Ausrichtung, moderne Datumsfelder, Ferien für alle Mitarbeiter

## Neueste UI-Updates (Meisterfenster v1.4.0)

- SQL Server und lokale SQLite nutzen dieselbe Schema-Pflege (fehlende Spalten werden beim Start nachgezogen)
- SQL-Passwort in `database.json` wird mit Windows DPAPI verschlüsselt; Windows-Anmeldung bleibt empfohlen
- Demo-Daten nur noch in der Debug-Version im Hamburger; Release-Büro-PCs sehen den Button nicht
- Drucken der aktuellen Ansicht, Sicherungskopie mit einem Klick
- Schweizer Feiertage im Feiertags-Dialog vorausfüllen
- Monatsansicht: lesbare Tagesbreite plus horizontales Scrollen
- Tooltip und Aufgabendialog zeigen, wer zuletzt geändert hat
- Installer wird in der CI bei jedem Push auf `main` mitgebaut

## Neueste UI-Updates (Meisterfenster v1.2.2)

- 📄 **PDF-Text korrigiert** – Aufgabenhöhe berücksichtigt die echte Zeichenbreite; lange Wörter umbrechen und Texte bleiben im eigenen Block
- 👤 **Namenskarte** – Nummer / Vorname / Nachname gestapelt; leere Mitarbeiterzeilen im PDF mit Mindesthöhe
- ✨ **Werkstatt-modernes UI** – Stahlblau/Anthrazit, aufgeräumte Toolbar, klare Mitarbeiter-Bänder im Plan, Dark Mode abgestimmt
- 📄 **PDF-Export klarer** – rote KW im Titel; Mitarbeiter-Blöcke mit Abstand; leere Tage ohne Slot-Gitter
- ↕️ **Mitarbeiter-Reihenfolge** – manuell per ≡ verschieben bleibt nach Neustart erhalten; Sortierung Nr./Name per Klick auf den Spaltenkopf
- 👥 **Mitarbeiter sofort sichtbar** nach Hinzufügen/Löschen; Drag-Reihenfolge repariert
- 📝 **Lange Aufgabentexte** ohne Leerzeichen werden korrekt umgebrochen
- ⚡ **Deutlich flüssiger** – Einfügen/Verschieben ohne Full-Reload; Wochenwechsel mit sofortigem Header-Update
- ★ **Favoriten bei Standard-Aufgaben** – Stern markieren, Favoriten stehen oben und bleiben gespeichert
- 📐 **Intelligente Zeilenhöhe** – Aufgaben rutschen nach oben, wenn darüber an einem Tag nichts geplant ist (kompaktere Ansicht)
- 🎯 **Auswahl & Hervorhebung** – Klick auf Zelle, Spaltenkopf (Tag) oder Mitarbeiterzeile hebt den Bereich hervor; Klick ins Leere hebt die Auswahl auf
- 🖱️ **Modernes Kontextmenü** – abgerundet, mit Icons und Farbpunkten bei Vorlagen (Rechtsklick auf Zelle oder Aufgabe)
- 🚫 **Keine doppelten Aufgaben** – gleiche Beschreibung, gleicher Mitarbeiter, gleicher Tag wird blockiert
- 📋 **Mehrfach-Vorlagen** – Shift/Ctrl für Mehrfachauswahl; Drag über mehrere Tage legt Vorlagen für jeden Tag an
- ✏️ **Doppelklick** auf Vorlage oder Aufgabe öffnet den Bearbeitungsdialog
- ⌨️ **Enter bestätigt Löschen** in modernen Dialogen
- 📋 **Kopieren nur einmal** – nach dem Einfügen wird die Zwischenablage geleert
- ↔️ **Aufgaben per Drag** zwischen Tagen verschieben (auch rückwärts), mit Vorschau
- 🎚️ **Verlängern/Verkürzen** per Griff an erstem/letztem Tag einer Mehrtages-Aufgabe

### Weitere UI-Updates

- 🍔 **Modernes Hamburger-Menü** oben links mit sauberer Card-Optik
- 👁️ **Button-Sichtbarkeit pro Funktion** direkt im Menü per Toggle-Chips konfigurierbar
- 🌓 **Theme-Umschaltung (Dark/Light)** mit sofortiger visueller Aktualisierung der gesamten Oberfläche
- 🌍 **Mehrsprachigkeit** (DE/EN/FR/IT) inkl. lokalisierter UI-Texte und Dialoge
- 💬 **Moderne Message-Dialoge** statt klassischer Standard-MessageBox
- 🎨 **Verbesserte Lesbarkeit im Dark Theme** (u. a. Standard-Aufgaben, DatePicker, Kontraste)

## Features

- 📅 **Kalenderansicht** mit Tag-, Wochen- und Monatsansicht
- 👥 **Mitarbeiterverwaltung** inkl. Mitarbeiter-Nr. (optional), Rolle (optional) und Drag-&-Drop-Reihenfolge
- ✅ **Aufgabenverwaltung** mit Details (Zeit, Ort, Projekt, Notizen, Typ, Farbe)
- 🔄 **Mehrtages-Aufgaben** mit Anzeige an jedem betroffenen Tag
- 🎯 **Aufgaben-Steuerung im Plan**:
  - Greifpunkt `⋮` zum Verlängern/Verkürzen einer Aufgabe per Drag & Drop
  - `Ctrl + Klick` zur Auswahl einzelner Tage einer Mehrtages-Aufgabe
  - Löschen nur der ausgewählten Tage möglich
- 📋 **Standard-Aufgaben (Vorlagen)**:
  - Erstellen, Bearbeiten, Löschen
  - Doppelklick auf Vorlage öffnet das Aufgabenfenster; Drag auf einen Tag weist zu
  - Favoriten per Stern oder Rechtsklick auf eine bestehende Aufgabe
  - Mehrfachauswahl mit Shift/Ctrl; Drag über mehrere Tage
  - Zuweisung per Drag & Drop oder Rechtsklick auf Zelle (modernes Kontextmenü mit Farbpunkten)
  - Persistente Speicherung in `%LOCALAPPDATA%\Wochenplanung\task_templates.json`
- 🚫 **Duplikat-Schutz** – gleiche Aufgabe (Beschreibung) pro Mitarbeiter und Tag nur einmal
- 📐 **Kompakte Planansicht** – leere Slots oben werden pro Tag entfernt, Aufgaben rutschen nach oben
- 🎯 **Plan-Auswahl** – Zelle, ganze Tag-Spalte oder Mitarbeiter-Zeile hervorheben
- 📎 **Aufgaben kopieren/einfügen**:
  - `Ctrl + C` / `Ctrl + V`
  - Kontextmenü „Kopieren“ auf Aufgabe und „Kopierte Aufgabe hier einfügen“ auf Zielzelle
  - Bei Ctrl-markierten Tagen werden nur diese Tage kopiert
  - Drag-&-Drop-Kopie per Kopier-Icon in der Task-Card auf eine Zielzelle
- ↩️ **Undo/Redo** für Planänderungen (u. a. Anlegen, Bearbeiten, Löschen, Verlängern/Verkürzen, Einfügen)
- 🔁 **Wiederkehrende Aufgaben** beim Erstellen (wöchentlich/monatlich, Anzahl Termine)
- 🔎 **Einklappbare Filterleiste** (Suchtext, Mitarbeiter, Aufgabentyp)
- 📥 **Robuster Import (CSV/Excel)** mit Mapping-Assistent für Spaltenzuordnung
- 🌗 **Optimierter Dark Mode** mit angepassten Kontrasten und konsistenter Typografie-Skala
- ✨ **Verbesserte Task-Cards** mit Hover-States, Micro-Interactions und klaren Action-Icons
- 📦 **Installer-Workflow** (Inno Setup) zusätzlich zur Single-File-EXE
- 🧪 **Automatisierte Tests** (Unit + SQLite-Integration) und **GitHub Actions CI**
- 📝 **Strukturierte Logs** mit Serilog (Debug + Rolling File)
- 🎨 **Farbcodierung** für Aufgabentypen
- 💾 **SQLite-Datenbank** für lokale Speicherung
- 🔄 **Navigation** – Vorherige/Nächste Woche direkt neben „Heute“; Datumsfeld und Tag-/Monatsansicht
- ⏱️ **Startzeit** – Eingabe als `10:00`, `10.00` oder `1000`
- 📊 **Excel-Export** – Zeitraum frei wählbar, Schriftgröße einstellbar
- 📄 **PDF-Export** – Zeitraum frei wählbar, Schriftgröße einstellbar, längere Zeiträume automatisch wochenweise (KW) auf mehrere Seiten

## Technologie-Stack

- **.NET 8** - Modernes Framework
- **WPF** - Windows Presentation Foundation für die Benutzeroberfläche
- **MVVM** - Model-View-ViewModel Pattern mit CommunityToolkit.Mvvm
- **Entity Framework Core 8** - ORM für Datenbankzugriffe
- **SQLite** - Lokale Datenbank
- **Clean Architecture** - Schichtbasierte Architektur
- **ClosedXML** - Excel-Export
- **PdfSharp** - PDF-Export
- **Serilog** - Strukturierte Logs

## Projektstruktur

```
Wochenplanung/
├── Wochenplanung.Domain/          # Domain-Modelle und Entitäten
│   ├── Entities/
│   │   ├── Employee.cs
│   │   └── WorkTask.cs
│   └── ValueObjects/
│       └── WeekInfo.cs
├── Wochenplanung.Application/     # Business-Logik und Services
│   ├── Interfaces/
│   │   ├── IEmployeeRepository.cs
│   │   └── IWorkTaskRepository.cs
│   └── Services/
│       └── ScheduleService.cs
├── Wochenplanung.Infrastructure/  # Datenbank und externe Services
│   ├── Data/
│   │   ├── ScheduleDbContext.cs
│   │   ├── DatabaseInitializer.cs
│   │   └── Migrations/
│   └── Repositories/
│       ├── EmployeeRepository.cs
│       └── WorkTaskRepository.cs
└── Wochenplanung.Presentation/    # WPF UI mit MVVM
    ├── ViewModels/
    │   ├── MainViewModel.cs
    │   ├── EmployeeDialogViewModel.cs
    │   ├── TaskDialogViewModel.cs
    │   ├── TaskTemplateDialogViewModel.cs
    │   └── ExportRangeViewModel.cs
    ├── Views/
    │   ├── ScheduleView.xaml
    │   ├── EmployeeDialog.xaml
    │   ├── TaskDialog.xaml
    │   ├── TaskTemplateDialog.xaml
    │   └── ExportRangeDialog.xaml
    ├── Converters/
    └── Services/
        ├── ExportService.cs
        ├── ScheduleTableLayout.cs
        ├── TaskTemplateStorage.cs
        └── UpdateCheckService.cs
├── Wochenplanung.Application.Tests/    # Unit-Tests
├── Wochenplanung.Infrastructure.Tests/ # SQLite-Integrationstests
├── installer/                          # Setup-Skript (Inno Setup)
└── .github/workflows/                  # CI/CD Workflows
```

## Voraussetzungen

- **.NET 8 SDK** oder höher
- **Visual Studio 2022**, **Visual Studio Code** oder ein anderer .NET-fähiger Editor
- **Windows** (WPF ist Windows-spezifisch)

## Installation

1. **Repository klonen:**
```bash
git clone <repository-url>
cd Wochenplanung
```

2. **NuGet-Pakete wiederherstellen:**
```bash
dotnet restore
```

3. **Projekt erstellen:**
```bash
dotnet build
```

## Verwendung

### Anwendung starten

**Mit Visual Studio Code:**
```bash
dotnet run --project Wochenplanung.Presentation
```

**Mit Visual Studio:**
- Öffnen Sie die `Wochenplanung.sln` Datei
- Drücken Sie F5 oder klicken Sie auf "Start"

**Mit .NET CLI:**
```bash
cd Wochenplanung.Presentation
dotnet run
```

### Datenbank

Die Anwendung erstellt automatisch eine SQLite-Datenbank beim ersten Start im lokalen AppData-Verzeichnis:
- **Pfad:** `%LOCALAPPDATA%\Wochenplanung\schedule.db`
- Die Datenbank wird automatisch erstellt, wenn sie nicht existiert

### Mitarbeiter verwalten

1. **Mitarbeiter hinzufügen:**
   - Klicken Sie auf den Button "+ Mitarbeiter" in der Toolbar
   - Geben Sie optional Mitarbeiter-Nr. und Rolle sowie Vorname/Nachname ein
   - Klicken Sie auf "Speichern"

2. **Mitarbeiter bearbeiten:**
   - Wählen Sie einen Mitarbeiter in der Liste aus (durch Klick auf den Namen)
   - Klicken Sie auf "✏ Mitarbeiter bearbeiten"
   - Ändern Sie die Daten und speichern Sie

3. **Mitarbeiter löschen:**
   - Wählen Sie einen Mitarbeiter aus
   - Klicken Sie auf "🗑 Mitarbeiter löschen"
   - Bestätigen Sie die Löschung

### Aufgaben verwalten

1. **Aufgabe hinzufügen:**
   - Es muss mindestens ein Mitarbeiter angelegt sein (sonst erscheint ein Hinweis).
   - Klicken Sie auf den Button "+ Aufgabe" in der Toolbar
   - Wählen Sie einen Mitarbeiter aus und geben Sie eine Beschreibung ein
   - Optional: Aktivieren Sie "Mehrfachtages-Aufgabe" – Enddatum darf nicht vor Startdatum liegen (wird ggf. automatisch angepasst)
   - Setzen Sie Startdatum (und Enddatum bei Mehrfachtages-Aufgaben)
   - Optional: Zeit, Ort, Projekt, Notizen, Aufgabentyp, Farbe
   - Klicken Sie auf "Speichern"

2. **Aufgabe bearbeiten:**
   - Wählen Sie eine Aufgabe aus (durch Klick auf die Aufgabe in der Tabelle)
   - Klicken Sie auf "✏ Aufgabe bearbeiten"
   - Ändern Sie die Daten und speichern Sie

3. **Aufgabe löschen:**
   - Wählen Sie eine Aufgabe aus
   - Klicken Sie auf "🗑 Aufgabe löschen"
   - Bestätigen Sie die Löschung

4. **Aufgabe verlängern/verkürzen im Plan:**
   - Aufgabe auswählen
   - Am Greifpunkt `⋮` in der Aufgabe ziehen und auf Ziel-Tag fallen lassen
   - Die Dauer wird entsprechend angepasst

5. **Einzelne Tage einer Mehrtages-Aufgabe löschen:**
   - Aufgabe auswählen
   - Mit `Ctrl + Klick` gewünschte Tage markieren
   - `Delete` oder `Backspace` drücken
   - Es werden nur die markierten Tage entfernt, nicht zwingend die ganze Aufgabe

6. **Undo/Redo:**
   - `Ctrl + Z` = letzte Planänderung rückgängig
   - `Ctrl + Y` = rückgängig gemachte Änderung wiederholen

7. **Wiederkehrende Aufgaben beim Erstellen:**
   - Im Aufgaben-Dialog Wiederholung wählen (`Keine`, `Wöchentlich`, `Monatlich`)
   - Anzahl Termine festlegen

### Ansicht und Navigation

- **Tag / Woche / Monat:** Drei Buttons im Header schalten zwischen Tages-, Wochen- und Monatsansicht (der aktive Button ist blau).
- **Navigation:** Je nach Ansicht erscheinen "Vorheriger/Nächster Tag", "Vorherige/Nächste Woche" oder "Vorheriger/Nächster Monat".
- **Heute:** Springt zum aktuellen Tag, zur aktuellen Woche oder zum aktuellen Monat.
- **Datumsauswahl:** Über das Kalenderfeld kann ein beliebiges Datum gewählt werden; die Ansicht springt entsprechend.

### Standard-Aufgaben (Vorlagen)

- Vorlagen (z. B. Schule, Ferien, Krank) erscheinen links in der Sidebar.
- **Per Drag & Drop** auf einen Tag oder **über mehrere Tage ziehen** (Mehrfachzuweisung).
- **Mehrfachauswahl:** Shift oder Ctrl + Klick auf mehrere Vorlagen, dann gemeinsam ziehen.
- **Per Rechtsklick** auf eine Tageszelle: modernes Menü mit Farbpunkten zur direkten Zuweisung.
- **Doppelklick** auf Vorlage öffnet den Bearbeitungsdialog.
- Vorlagen können mit "+ Vorlage", "Bearbeiten" und "Löschen" verwaltet werden.

### Plan-Auswahl und Navigation in der Tabelle

- **Klick auf leere Zelle** – markiert Zelle (Mitarbeiter + Tag).
- **Klick auf Spaltenkopf (Tag)** – hebt die gesamte Tag-Spalte hervor.
- **Klick auf Mitarbeiternamen** – hebt die gesamte Zeile hervor.
- **Klick ins Leere** – hebt die Auswahl auf.
- **Aufgabe auswählen** – Klick auf Task-Card; bei Mehrtages-Aufgaben mit Ctrl einzelne Tage markieren.

### Aufgaben kopieren/einfügen

- Aufgabe auswählen und **`Ctrl + C`** drücken (oder Kontextmenü „Kopieren“ mit Icon).
- Ziel-Zelle per Rechtsklick öffnen und „Kopierte Aufgabe hier einfügen“ wählen.
- Alternativ **`Ctrl + V`** (nutzt aktuellen Tag und ausgewählten Mitarbeiter).
- Nach dem Einfügen wird die Kopie automatisch geleert (nur einmal einfügbar).
- Bei Ctrl-markierten Mehrtages-Teilbereichen werden nur diese Teilbereiche kopiert.
- Zusätzlich kann eine Aufgabe direkt per Drag & Drop über das Kopier-Icon auf eine andere Zelle kopiert werden.

### Aufgaben verschieben und Dauer ändern

- **Verschieben:** Aufgabe anklicken und auf einen anderen Tag ziehen (auch rückwärts).
- **Verlängern/Verkürzen:** Griff `⋮` am **ersten oder letzten Tag** einer Mehrtages-Aufgabe ziehen.
- **Löschen:** Delete/Backspace – bei Ctrl-markierten Tagen nur diese Tage; Enter bestätigt im Dialog.

### Filter

- Die Filterleiste ist einklappbar und enthält:
  - Suchtext (Beschreibung/Ort/Projekt/Notizen)
  - Mitarbeiter-Filter
  - Aufgabentyp-Filter
  - „Filter zurücksetzen“

### Exportieren

1. **Excel-Export:**
   - Klicken Sie auf "📊 Excel" im Header
   - Im Dialog "Export – Zeitraum wählen" werden Von-/Bis-Datum und Schriftgröße festgelegt (Enddatum darf nicht vor Startdatum liegen)
   - Nach "Export" Speicherort und Dateiname wählen
   - Die Excel-Datei enthält den gewählten Zeitraum mit formatierter Tabelle; mehrere Aufgaben pro Tag in eigenen Zeilen mit jeweiliger Farbe

2. **PDF-Export:**
   - Klicken Sie auf "📄 PDF" im Header
   - Zeitraum und Schriftgröße wie beim Excel-Export wählen, dann Speicherort
   - Längere Zeiträume werden automatisch in Wochen (KW) auf mehrere Seiten aufgeteilt
   - Die PDF-Datei wird im Querformat mit farbigen Einträgen pro Tag erstellt

### Importieren (CSV/Excel)

- Über „CSV/Excel Import“ können `.csv` und `.xlsx` importiert werden.
- Der Mapping-Assistent erkennt Spalten automatisch und erlaubt manuelles Mapping für:
  - Mitarbeiter
  - Datum
  - Beschreibung

## Entwicklung

### Datenbank-Migrationen

Um eine neue Migration zu erstellen (falls benötigt):
```bash
dotnet ef migrations add <MigrationName> --project Wochenplanung.Infrastructure --startup-project Wochenplanung.Presentation
```

Um die Datenbank zu aktualisieren:
```bash
dotnet ef database update --project Wochenplanung.Infrastructure --startup-project Wochenplanung.Presentation
```

Die Anwendung startet die Migrationen beim Start automatisch über EF Core (`Migrate()`).

### Tests und CI

Tests lokal ausführen:
```bash
dotnet test
```

GitHub Actions:
- `ci.yml`: Restore, Build, Test, Publish-Artifact
- `setup.yml`: Setup-Erstellung als Artifact

### Release-Artefakte

Vollständiges Release (Tests, Publish, ZIP) erstellen:
```powershell
powershell -ExecutionPolicy Bypass -File installer/build-release.ps1
```

Erzeugt:
- `publish/` – lauffähige Single-File-EXE + Anleitung + VERSION.txt
- `Meisterfenster-Weitergabe.zip` – zum Weitergeben an andere PCs

Single-File-EXE manuell:
```bash
dotnet publish Wochenplanung.Presentation/Wochenplanung.Presentation.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Hinweis:
- Der `publish`-Ordner dient als lokales Auslieferungsziel auf dem Entwicklerrechner.
- Für Git sollte die EXE nicht versioniert werden (große Binärdatei), sondern als Release-Artefakt verteilt werden.

Setup (Inno Setup):
```powershell
powershell -ExecutionPolicy Bypass -File installer/build-setup.ps1
```

### Debugging

Wenn die Anwendung beim Start abstürzt:

1. **Überprüfen Sie die Konsolenausgabe** für Fehlermeldungen
2. **Überprüfen Sie die Datenbankdatei** im AppData-Verzeichnis
3. **Löschen Sie die Datenbankdatei** (`schedule.db`) und starten Sie neu, wenn die Datenbank beschädigt ist
4. **Überprüfen Sie die Logs** unter `%LOCALAPPDATA%\Wochenplanung\logs\` (`startup.log` und `app-*.log`)

### Bekannte Probleme

- **Anwendung startet nicht:** Stellen Sie sicher, dass .NET 8 SDK installiert ist
- **Datenbankfehler:** Löschen Sie die `schedule.db` Datei und starten Sie die Anwendung neu
- **Export-Fehler:** Stellen Sie sicher, dass Sie Schreibrechte im Zielverzeichnis haben

## Lizenz

Dieses Projekt ist für den internen Gebrauch bestimmt.

## Unterstützung

Bei Problemen oder Fragen:
1. Überprüfen Sie die Konsolenausgabe auf Fehlermeldungen
2. Überprüfen Sie, ob alle NuGet-Pakete korrekt installiert sind
3. Stellen Sie sicher, dass .NET 8 SDK installiert ist
