using ClosedXML.Excel;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Globalization;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Domain.ValueObjects;

namespace Wochenplanung.Presentation.Services;

public class ExportService
{
    /// <summary>PDF-Seitenformat 16:9 (etwas größer als v1.1.3, gleiche Proportion).</summary>
    private const double PageWidthMm = 584;
    private const double PageHeightMm = 329;

    /// <summary>Exportiert für eine Woche (Mo–So, wie im Kalender).</summary>
    public async Task ExportToExcelAsync(List<Employee> employees, List<WorkTask> tasks, WeekInfo week, string filePath)
    {
        var days = week.GetDays();
        await ExportToExcelAsync(employees, tasks, days, $"Arbeitsprogramm {week.Year} KW {week.WeekNumber}", filePath, 9);
    }

    /// <summary>Exportiert für einen beliebigen Zeitraum (Tage von Von bis Bis).</summary>
    public async Task ExportToExcelAsync(List<Employee> employees, List<WorkTask> tasks, DateTime startDate, DateTime endDate, string filePath, int fontSize = 9)
    {
        var days = new List<DateTime>();
        for (var d = startDate.Date; d <= endDate.Date; d = d.AddDays(1))
            days.Add(d);
        var title = days.Count == 0 ? "Arbeitsplan" : $"Arbeitsplan {startDate:dd.MM.yyyy} – {endDate:dd.MM.yyyy}";
        await ExportToExcelAsync(employees, tasks, days, title, filePath, fontSize);
    }

    private static async Task ExportToExcelAsync(List<Employee> employees, List<WorkTask> tasks, List<DateTime> days, string title, string filePath, int fontSize)
    {
        await Task.Run(() =>
        {
            var dayNames = new[] { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" };
            var numDays = days.Count;
            if (numDays == 0) return;

            var baseFont = Math.Clamp(fontSize, 9, 18);
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Wochenplan");

            var kw = ISOWeek.GetWeekOfYear(days[0]);
            var excelTitle = $"Arbeitsplan KW {kw} {days[0]:dd.MM.yyyy} – {days[^1]:dd.MM.yyyy}";
            // Titel (über alle Spalten) – größer, dezent hervorgehoben
            worksheet.Cell(1, 1).Value = excelTitle;
            worksheet.Range(1, 1, 1, 1 + numDays).Merge();
            worksheet.Range(1, 1, 1, 1 + numDays).Style.Font.Bold = true;
            worksheet.Range(1, 1, 1, 1 + numDays).Style.Font.FontSize = baseFont + 5;
            worksheet.Range(1, 1, 1, 1 + numDays).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(1, 1, 1, 1 + numDays).Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
            worksheet.Row(1).Height = 28;

            // Spaltenüberschriften: Arbeiter/In + Tage
            worksheet.Cell(2, 1).Value = "Arbeiter/In";
            for (int i = 0; i < numDays; i++)
            {
                worksheet.Cell(2, i + 2).Value = $"{dayNames[(int)days[i].DayOfWeek]}\n{days[i]:dd.MM.yyyy}";
                worksheet.Cell(2, i + 2).Style.Alignment.WrapText = true;
            }
            var headerRange = worksheet.Range(2, 1, 2, 1 + numDays);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontSize = baseFont + 1;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(226, 232, 240);
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Alignment.WrapText = true;
            worksheet.Row(2).Height = Math.Max(38, baseFont * 3.6);

            int row = 3;
            foreach (var employee in employees)
            {
                var weekLayout = ScheduleTableLayout.PackLayoutForExport(
                    ScheduleTableLayout.BuildEmployeeWeekLayout(employee, tasks, days));
                var maxSlots = weekLayout.Count == 0 ? 1 : Math.Max(1, weekLayout.Values.Max(v => v.Count));
                var employeeStartRow = row;
                for (int slot = 0; slot < maxSlots; slot++)
                {
                    worksheet.Cell(row, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Cell(row, 1).Style.Fill.BackgroundColor = (row - 2) % 2 == 0 ? XLColor.FromArgb(248, 250, 252) : XLColor.White;
                    worksheet.Cell(row, 1).Style.Font.FontSize = baseFont;
                    worksheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    worksheet.Cell(row, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    var maxLines = 2;
                    for (int dayIndex = 0; dayIndex < numDays; dayIndex++)
                    {
                        var day = days[dayIndex];
                        if (!weekLayout.TryGetValue(day.Date, out var orderedSlots))
                            orderedSlots = new List<WorkTask?>();
                        var cell = worksheet.Cell(row, dayIndex + 2);
                        cell.Style.Alignment.WrapText = true;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Font.FontSize = baseFont;
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        cell.Style.Border.InsideBorder = XLBorderStyleValues.Hair;

                        if (slot < orderedSlots.Count && orderedSlots[slot] is { } task)
                        {
                            var text = FormatExportTaskText(task, includeLocation: true);
                            cell.Value = text;
                            cell.Style.Fill.BackgroundColor = GetExcelColor(task.Color);
                            var colWidth = days[dayIndex].DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 13 : 16;
                            maxLines = Math.Max(maxLines, CountWrapLines(text, ExcelCharsPerColumn(colWidth)));
                        }
                        else
                        {
                            cell.Style.Fill.BackgroundColor = (row - 2) % 2 == 0 ? XLColor.FromArgb(248, 250, 252) : XLColor.White;
                        }
                    }
                    worksheet.Row(row).Height = ExcelRowHeight(baseFont, maxLines);
                    row++;
                }

                var employeeEndRow = row - 1;
                var nameCell = worksheet.Cell(employeeStartRow, 1);
                nameCell.Value = string.Join("\n", new[]
                {
                    string.IsNullOrWhiteSpace(employee.EmployeeNumber) ? "–" : employee.EmployeeNumber.Trim(),
                    employee.FirstName?.Trim() ?? "",
                    employee.LastName?.Trim() ?? ""
                }.Where(s => !string.IsNullOrEmpty(s)));
                nameCell.Style.Alignment.WrapText = true;
                nameCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                nameCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                if (employeeEndRow > employeeStartRow)
                    worksheet.Range(employeeStartRow, 1, employeeEndRow, 1).Merge();
            }

            worksheet.Column(1).Width = 18;
            for (int i = 0; i < numDays; i++)
            {
                var isWeekend = days[i].DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                worksheet.Column(i + 2).Width = isWeekend ? 13 : 16;
            }
            worksheet.SheetView.FreezeRows(2);

            workbook.SaveAs(filePath);
        });
    }

    /// <summary>PDF-Export für eine Woche (Mo–So, wie im Kalender).</summary>
    public async Task ExportToPdfAsync(List<Employee> employees, List<WorkTask> tasks, WeekInfo week, string filePath)
    {
        var days = week.GetDays();
        await ExportToPdfAsync(employees, tasks, days, $"Arbeitsprogramm {week.Year} KW {week.WeekNumber}", filePath, 9);
    }

    /// <summary>PDF-Export für einen beliebigen Zeitraum.</summary>
    public async Task ExportToPdfAsync(List<Employee> employees, List<WorkTask> tasks, DateTime startDate, DateTime endDate, string filePath, int fontSize = 9)
    {
        var days = new List<DateTime>();
        for (var d = startDate.Date; d <= endDate.Date; d = d.AddDays(1))
            days.Add(d);
        var title = days.Count == 0 ? "Arbeitsplan" : $"Arbeitsplan {startDate:dd.MM.yyyy} – {endDate:dd.MM.yyyy}";
        await ExportToPdfAsync(employees, tasks, days, title, filePath, fontSize);
    }

    private static async Task ExportToPdfAsync(List<Employee> employees, List<WorkTask> tasks, List<DateTime> days, string title, string filePath, int fontSize)
    {
        await Task.Run(() =>
        {
            if (days.Count == 0)
                return;

            var dayNames = new[] { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" };
            // Längere Zeiträume werden immer wochenweise (ISO-KW) exportiert.
            var dayChunks = days
                .GroupBy(d => (Year: ISOWeek.GetYear(d), Week: ISOWeek.GetWeekOfYear(d)))
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Week)
                .Select(g => g.OrderBy(d => d).ToList())
                .ToList();

            using var document = new PdfDocument();

            foreach (var chunk in dayChunks)
            {
                var chunkWeek = ISOWeek.GetWeekOfYear(chunk.First());
                var chunkDateFrom = chunk.First();
                var chunkDateTo = chunk.Last();

                var page = document.AddPage();
                // 16:9, etwas größer als zuvor (gleiche Proportionen)
                page.Width = XUnit.FromMillimeter(PageWidthMm);
                page.Height = XUnit.FromMillimeter(PageHeightMm);
                var gfx = XGraphics.FromPdfPage(page);

                var baseFont = Math.Clamp(fontSize, 9, 22);
                var fontTitle = new XFont("Arial", Math.Min(30, baseFont + 8), XFontStyleEx.Bold);
                var fontHeader = new XFont("Arial", Math.Max(10, baseFont + 1), XFontStyleEx.Bold);
                var fontHeaderDate = new XFont("Arial", Math.Max(9, baseFont), XFontStyleEx.Bold);
                var fontName = new XFont("Arial", Math.Max(10, baseFont + 1), XFontStyleEx.Bold);
                var fontNameNumber = new XFont("Arial", Math.Max(14, baseFont + 5), XFontStyleEx.Bold);
                var fontCell = new XFont("Arial", Math.Max(9, baseFont));
                // Mindesthöhe für Namenskarte (Nr. + Vorname + Nachname), auch ohne Aufgaben
                var minEmployeeRowHeight = fontNameNumber.Height + (2 * fontName.Height) + 28;

                const double margin = 18;
                const double titleHeight = 34;
                const double headerHeight = 50;
                var rowHeight = Math.Max(26, baseFont * 2.1);
                const double cellPadding = 3;
                const double taskInset = 1.5;
                const double namePad = 2;
                // Abstand zwischen Mitarbeiter-Blöcken = klarste visuelle Trennung
                const double employeeGap = 7;
                // Interne Linien dezent, Außenrahmen pro Mitarbeiter kräftig
                var gridPen = new XPen(XColor.FromArgb(197, 206, 216), 0.9);
                var employeeOutlinePen = new XPen(XColor.FromArgb(74, 95, 120), 2.4);
                var headerBg = new XSolidBrush(XColor.FromArgb(228, 233, 240));
                var titleBg = new XSolidBrush(XColor.FromArgb(238, 241, 245));
                var nameBgA = new XSolidBrush(XColor.FromArgb(220, 230, 240));
                var nameBgB = new XSolidBrush(XColor.FromArgb(232, 238, 245));
                var accentA = new XSolidBrush(XColor.FromArgb(47, 95, 138));
                var accentB = new XSolidBrush(XColor.FromArgb(122, 144, 168));
                var emptyCellA = new XSolidBrush(XColor.FromArgb(252, 253, 255));
                var emptyCellB = new XSolidBrush(XColor.FromArgb(238, 243, 249));
                var kwBrush = new XSolidBrush(XColor.FromArgb(200, 32, 32));

                var totalWidth = page.Width.Point - 2 * margin;
                var measuredName = employees.Count == 0
                    ? 80
                    : employees.Max(e =>
                    {
                        var nr = string.IsNullOrWhiteSpace(e.EmployeeNumber) ? "–" : e.EmployeeNumber.Trim();
                        var wNr = gfx.MeasureString(nr, fontNameNumber).Width;
                        var wFirst = gfx.MeasureString(e.FirstName ?? "", fontName).Width;
                        var wLast = gfx.MeasureString(e.LastName ?? "", fontName).Width;
                        return Math.Max(wNr, Math.Max(wFirst, wLast));
                    });
                var headerNameWidth = gfx.MeasureString("Arbeiter/In", fontHeader).Width;
                var nameColWidth = Math.Clamp(Math.Max(measuredName, headerNameWidth) + 28, 88, totalWidth * 0.18);
                var dayColWidths = BuildDayColumnWidths(chunk, totalWidth - nameColWidth);
                var pageContentBottom = page.Height.Point - margin;

                // 1) Höhe = höchster Tagesstapel, Karten kleben oben aneinander.
                var employeeLayouts = new List<(Employee Employee, Dictionary<DateTime, List<WorkTask>> TasksByDay, double Height)>();
                foreach (var employee in employees)
                {
                    var packed = ScheduleTableLayout.PackLayoutForExport(
                        ScheduleTableLayout.BuildEmployeeWeekLayout(employee, tasks, chunk));
                    var tasksByDay = new Dictionary<DateTime, List<WorkTask>>();
                    var maxStack = rowHeight;
                    for (var dayIdx = 0; dayIdx < chunk.Count; dayIdx++)
                    {
                        var day = chunk[dayIdx];
                        var dayTasks = packed.TryGetValue(day.Date, out var slots)
                            ? slots.Where(t => t != null).Cast<WorkTask>().ToList()
                            : new List<WorkTask>();
                        tasksByDay[day.Date] = dayTasks;

                        var stack = taskInset;
                        foreach (var task in dayTasks)
                        {
                            var text = FormatExportTaskText(task, includeLocation: false);
                            var textWidth = dayColWidths[dayIdx] - (2 * (cellPadding + taskInset));
                            var lines = WrapText(gfx, text, fontCell, textWidth);
                            stack += MeasureWrappedBlockHeight(lines.Count, fontCell, cellPadding, 0) + taskInset;
                        }

                        maxStack = Math.Max(maxStack, stack);
                    }

                    employeeLayouts.Add((employee, tasksByDay, Math.Max(minEmployeeRowHeight, maxStack)));
                }

                double DrawPageHeader(double startY)
                {
                    var titleRect = new XRect(margin, startY, totalWidth, titleHeight);
                    gfx.DrawRectangle(titleBg, titleRect);
                    gfx.DrawRectangle(gridPen, titleRect);
                    DrawTitleWithRedKw(gfx, titleRect, fontTitle, chunkWeek, chunkDateFrom, chunkDateTo, kwBrush);
                    var y = startY + titleHeight + 2;

                    return DrawDayHeaderRow(y);
                }

                double DrawDayHeaderRow(double y)
                {
                    var headerRect = new XRect(margin, y, totalWidth, headerHeight);
                    gfx.DrawRectangle(headerBg, headerRect);
                    gfx.DrawRectangle(gridPen, headerRect);
                    gfx.DrawString("Arbeiter/In", fontHeader, XBrushes.Black,
                        new XRect(margin + namePad, y, nameColWidth - (2 * namePad), headerHeight), XStringFormats.Center);

                    var x = margin + nameColWidth;
                    for (var dayIdx = 0; dayIdx < chunk.Count; dayIdx++)
                    {
                        var day = chunk[dayIdx];
                        var w = dayColWidths[dayIdx];
                        var rect = new XRect(x, y, w, headerHeight);
                        gfx.DrawRectangle(gridPen, rect);
                        var top = new XRect(rect.X, rect.Y + 2, rect.Width, rect.Height / 2 - 2);
                        var bottom = new XRect(rect.X, rect.Y + rect.Height / 2, rect.Width, rect.Height / 2 - 2);
                        gfx.DrawString(dayNames[(int)day.DayOfWeek], fontHeader, XBrushes.Black, top, XStringFormats.Center);
                        gfx.DrawString(day.ToString("dd.MM.yyyy"), fontHeaderDate, XBrushes.Black, bottom, XStringFormats.Center);
                        x += w;
                    }
                    return y + headerHeight;
                }

                // 2) Seitenweise zeichnen – Zeilen behalten natürliche Höhe (kein Aufblasen auf Seitenhöhe).
                var pageStartIndex = 0;
                var isFirstPage = true;
                while (pageStartIndex < employeeLayouts.Count)
                {
                    if (!isFirstPage)
                    {
                        page = document.AddPage();
                        page.Width = XUnit.FromMillimeter(PageWidthMm);
                        page.Height = XUnit.FromMillimeter(PageHeightMm);
                        gfx = XGraphics.FromPdfPage(page);
                        pageContentBottom = page.Height.Point - margin;
                    }

                    isFirstPage = false;
                    var yPos = DrawPageHeader(margin);
                    var availableHeight = pageContentBottom - yPos;

                    var pageEndIndex = pageStartIndex;
                    var packedHeight = 0.0;
                    while (pageEndIndex < employeeLayouts.Count)
                    {
                        var nextHeight = employeeLayouts[pageEndIndex].Height;
                        var gap = pageEndIndex > pageStartIndex ? employeeGap : 0;
                        if (pageEndIndex > pageStartIndex && packedHeight + gap + nextHeight > availableHeight)
                            break;
                        packedHeight += gap + nextHeight;
                        pageEndIndex++;
                        if (nextHeight > availableHeight)
                            break;
                    }

                    if (pageEndIndex == pageStartIndex)
                        pageEndIndex = pageStartIndex + 1;

                    var pageItems = employeeLayouts.GetRange(pageStartIndex, pageEndIndex - pageStartIndex);

                    var rowIndex = 0;
                    foreach (var (employee, tasksByDay, totalEmployeeHeight) in pageItems)
                    {
                        if (rowIndex > 0)
                            yPos += employeeGap;

                        var employeeTop = yPos;
                        var isAlt = rowIndex % 2 == 1;
                        var nameBg = isAlt ? nameBgB : nameBgA;
                        var accent = isAlt ? accentB : accentA;
                        var emptyBg = isAlt ? emptyCellB : emptyCellA;

                        var nameRect = new XRect(margin, yPos, nameColWidth, totalEmployeeHeight);
                        gfx.DrawRectangle(nameBg, nameRect);
                        gfx.DrawRectangle(gridPen, nameRect);
                        // Schmaler Akzentstreifen links – Bandzugehörigkeit auch von Weitem
                        gfx.DrawRectangle(accent, new XRect(margin, employeeTop, 4.5, totalEmployeeHeight));
                        DrawStackedEmployeeName(
                            gfx,
                            employee,
                            new XRect(nameRect.X + namePad + 6, nameRect.Y + 4, nameRect.Width - (2 * namePad) - 10, nameRect.Height - 8),
                            fontNameNumber,
                            fontName);

                        // Tages-Spalten: zuerst durchgehender Hintergrund (ohne leere Slot-Gitter),
                        // danach nur Aufgaben zeichnen – leere Bereiche bleiben ruhig.
                        var xPos = margin + nameColWidth;
                        for (var dayIdx = 0; dayIdx < chunk.Count; dayIdx++)
                        {
                            var colWidth = dayColWidths[dayIdx];
                            var dayRect = new XRect(xPos, employeeTop, colWidth, totalEmployeeHeight);
                            gfx.DrawRectangle(emptyBg, dayRect);

                            var day = chunk[dayIdx];
                            if (!tasksByDay.TryGetValue(day.Date, out var dayTasks))
                                dayTasks = new List<WorkTask>();

                            var chipY = employeeTop + taskInset;
                            foreach (var task in dayTasks)
                            {
                                var text = FormatExportTaskText(task, includeLocation: false);
                                var textWidth = colWidth - (2 * (cellPadding + taskInset));
                                var lines = WrapText(gfx, text, fontCell, textWidth);
                                var chipHeight = MeasureWrappedBlockHeight(lines.Count, fontCell, cellPadding, 0);
                                if (chipY + chipHeight > employeeTop + totalEmployeeHeight - taskInset)
                                    chipHeight = Math.Max(rowHeight - 4, employeeTop + totalEmployeeHeight - taskInset - chipY);
                                var taskRect = new XRect(
                                    xPos + taskInset,
                                    chipY,
                                    colWidth - (2 * taskInset),
                                    chipHeight);
                                gfx.DrawRectangle(new XSolidBrush(GetPdfColor(task.Color)), taskRect);
                                DrawWrappedText(gfx, text, fontCell, XBrushes.Black,
                                    new XRect(taskRect.X + cellPadding, taskRect.Y + cellPadding,
                                        taskRect.Width - (2 * cellPadding), taskRect.Height - (2 * cellPadding)));
                                chipY += chipHeight + taskInset;
                            }

                            // Nur Aussenlinie der Tages-Spalte
                            gfx.DrawRectangle(gridPen, dayRect);
                            xPos += colWidth;
                        }

                        // Kräftiger Rahmen um den ganzen Mitarbeiter-Block
                        gfx.DrawRectangle(employeeOutlinePen, new XRect(margin, employeeTop, totalWidth, totalEmployeeHeight));

                        yPos += totalEmployeeHeight;
                        rowIndex++;
                    }

                    pageStartIndex = pageEndIndex;
                }
            }

            document.Save(filePath);
        });
    }

    /// <summary>Namenskarte im PDF: Nummer / Vorname / Nachname zentriert gestapelt.</summary>
    private static void DrawStackedEmployeeName(
        XGraphics gfx,
        Employee employee,
        XRect area,
        XFont fontNumber,
        XFont fontName)
    {
        var number = string.IsNullOrWhiteSpace(employee.EmployeeNumber) ? "–" : employee.EmployeeNumber.Trim();
        var first = (employee.FirstName ?? "").Trim();
        var last = (employee.LastName ?? "").Trim();

        var lineHNumber = fontNumber.Height;
        var lineHName = fontName.Height;
        var totalH = lineHNumber
            + (string.IsNullOrEmpty(first) ? 0 : lineHName + 2)
            + (string.IsNullOrEmpty(last) ? 0 : lineHName + 2);
        var y = area.Y + Math.Max(0, (area.Height - totalH) / 2);

        // Weisse Karte hinter dem Text
        var cardPad = 6;
        var cardRect = new XRect(area.X + 2, y - cardPad, area.Width - 4, totalH + (2 * cardPad));
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, 255, 255)), cardRect);
        gfx.DrawRectangle(new XPen(XColor.FromArgb(197, 206, 216), 0.8), cardRect);

        var centerFormat = new XStringFormat
        {
            Alignment = XStringAlignment.Center,
            LineAlignment = XLineAlignment.Near
        };

        gfx.DrawString(number, fontNumber, XBrushes.Black,
            new XRect(area.X, y, area.Width, lineHNumber), centerFormat);
        y += lineHNumber + 2;

        if (!string.IsNullOrEmpty(first))
        {
            gfx.DrawString(first, fontName, XBrushes.Black,
                new XRect(area.X, y, area.Width, lineHName), centerFormat);
            y += lineHName + 2;
        }

        if (!string.IsNullOrEmpty(last))
        {
            gfx.DrawString(last, fontName, XBrushes.Black,
                new XRect(area.X, y, area.Width, lineHName), centerFormat);
        }
    }

    /// <summary>Titel: „Arbeitsplan“ + rote KW + Datumsbereich.</summary>
    private static void DrawTitleWithRedKw(
        XGraphics gfx,
        XRect titleRect,
        XFont font,
        int weekNumber,
        DateTime from,
        DateTime to,
        XBrush kwBrush)
    {
        var prefix = "Arbeitsplan ";
        var kwText = $"KW {weekNumber}";
        var suffix = $" {from:dd.MM.yyyy} – {to:dd.MM.yyyy}";

        var wPrefix = gfx.MeasureString(prefix, font).Width;
        var wKw = gfx.MeasureString(kwText, font).Width;
        var wSuffix = gfx.MeasureString(suffix, font).Width;
        var total = wPrefix + wKw + wSuffix;
        var x = titleRect.X + Math.Max(0, (titleRect.Width - total) / 2);
        var format = new XStringFormat
        {
            Alignment = XStringAlignment.Near,
            LineAlignment = XLineAlignment.Center
        };

        gfx.DrawString(prefix, font, XBrushes.Black, new XRect(x, titleRect.Y, wPrefix + 1, titleRect.Height), format);
        x += wPrefix;
        gfx.DrawString(kwText, font, kwBrush, new XRect(x, titleRect.Y, wKw + 1, titleRect.Height), format);
        x += wKw;
        gfx.DrawString(suffix, font, XBrushes.Black, new XRect(x, titleRect.Y, wSuffix + 1, titleRect.Height), format);
    }

    /// <summary>
    /// Namenskarte braucht Mindesthöhe, aber vorhandene Aufgaben-Zeilen
    /// dürfen nicht zu einer einzigen Fläche zusammenfallen.
    /// </summary>
    public static double[] PadSlotHeightsToMinimum(double[] slotHeights, double minHeight)
    {
        if (slotHeights.Length == 0)
            return new[] { minHeight };

        var sum = slotHeights.Sum();
        if (sum >= minHeight)
            return slotHeights;

        var padded = (double[])slotHeights.Clone();
        padded[^1] += minHeight - sum;
        return padded;
    }

    /// <summary>Wochentage normal, Sa/So schmaler – Datum bleibt lesbar.</summary>
    private static double[] BuildDayColumnWidths(IReadOnlyList<DateTime> days, double availableWidth)
    {
        if (days.Count == 0)
            return Array.Empty<double>();

        var weights = days
            .Select(d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0.72 : 1.0)
            .ToArray();
        var sum = weights.Sum();
        return weights.Select(w => availableWidth * (w / sum)).ToArray();
    }

    private static XLColor GetExcelColor(TaskColor color)
    {
        return color switch
        {
            TaskColor.Orange => XLColor.FromArgb(255, 196, 140),
            TaskColor.Green => XLColor.FromArgb(155, 220, 160),
            TaskColor.Blue => XLColor.FromArgb(155, 195, 235),
            TaskColor.Beige => XLColor.FromArgb(235, 228, 200),
            TaskColor.Yellow => XLColor.FromArgb(250, 235, 130),
            TaskColor.Red => XLColor.FromArgb(240, 160, 155),
            _ => XLColor.White
        };
    }

    public static string FormatExportTaskText(WorkTask task, bool includeLocation)
    {
        var text = task.Description;
        if (task.StartTime.HasValue)
            text += $" ({task.StartTime.Value:hh\\:mm})";
        if (includeLocation && !string.IsNullOrEmpty(task.Location))
            text += $" - {task.Location}";
        return text;
    }

    public static int ExcelCharsPerColumn(double columnWidth) =>
        Math.Max(8, (int)Math.Floor(columnWidth * 1.05) - 1);

    public static int CountWrapLines(string text, int maxCharsPerLine)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 1;

        maxCharsPerLine = Math.Max(4, maxCharsPerLine);
        var lines = 0;
        var currentLen = 0;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length > maxCharsPerLine)
            {
                if (currentLen > 0)
                    lines++;
                lines += (word.Length + maxCharsPerLine - 1) / maxCharsPerLine;
                currentLen = 0;
                continue;
            }

            var nextLen = currentLen == 0 ? word.Length : currentLen + 1 + word.Length;
            if (nextLen > maxCharsPerLine && currentLen > 0)
            {
                lines++;
                currentLen = word.Length;
            }
            else
            {
                currentLen = nextLen;
            }
        }

        if (currentLen > 0)
            lines++;
        return Math.Max(1, lines);
    }

    public static double ExcelRowHeight(int fontSize, int lineCount) =>
        Math.Clamp(lineCount * (fontSize * 1.35) + 10, 32, 90);

    private static double PdfLineHeight(XFont font) => Math.Max(font.Height, font.Size * 1.28) + 2.2;

    private static double MeasureWrappedBlockHeight(int lineCount, XFont font, double cellPadding, double taskInset) =>
        Math.Max(1, lineCount) * PdfLineHeight(font) + (2 * cellPadding) + (2 * taskInset) + 3;

    private static List<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string> { string.Empty };

        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // Sehr lange Wörter/Projektcodes ebenfalls umbrechen, damit sie nicht
            // in die nächste Tages-Spalte laufen.
            if (gfx.MeasureString(word, font).Width > maxWidth)
            {
                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                    current = string.Empty;
                }

                var part = string.Empty;
                foreach (var character in word)
                {
                    var candidatePart = part + character;
                    if (gfx.MeasureString(candidatePart, font).Width > maxWidth && part.Length > 0)
                    {
                        lines.Add(part);
                        part = character.ToString();
                    }
                    else
                    {
                        part = candidatePart;
                    }
                }

                current = part;
                continue;
            }

            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (gfx.MeasureString(candidate, font).Width > maxWidth && !string.IsNullOrEmpty(current))
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (!string.IsNullOrEmpty(current))
            lines.Add(current);

        return lines.Count == 0 ? new List<string> { text } : lines;
    }

    private static void DrawWrappedText(XGraphics gfx, string text, XFont font, XBrush brush, XRect rect)
    {
        var lines = WrapText(gfx, text, font, rect.Width);
        var y = rect.Y;
        var lineHeight = PdfLineHeight(font);
        var state = gfx.Save();
        gfx.IntersectClip(rect);
        foreach (var line in lines)
        {
            if (y + lineHeight > rect.Bottom + 0.8)
                break;
            gfx.DrawString(line, font, brush, new XRect(rect.X, y, rect.Width, lineHeight), XStringFormats.TopLeft);
            y += lineHeight;
        }
        gfx.Restore(state);
    }

    private static XColor GetPdfColor(TaskColor color)
    {
        return color switch
        {
            TaskColor.Orange => XColor.FromArgb(255, 196, 140),
            TaskColor.Green => XColor.FromArgb(155, 220, 160),
            TaskColor.Blue => XColor.FromArgb(155, 195, 235),
            TaskColor.Beige => XColor.FromArgb(235, 228, 200),
            TaskColor.Yellow => XColor.FromArgb(250, 235, 130),
            TaskColor.Red => XColor.FromArgb(240, 160, 155),
            _ => XColor.FromArgb(255, 255, 255)
        };
    }
}
