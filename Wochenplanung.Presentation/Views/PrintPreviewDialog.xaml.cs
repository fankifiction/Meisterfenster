using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wochenplanung.Presentation.Localization;
using MessageBox = Wochenplanung.Presentation.Services.ModernMessageBox;

namespace Wochenplanung.Presentation.Views;

public partial class PrintPreviewDialog : Window
{
    private readonly List<PrintQueue> _printers = new();
    private bool _loading;

    public PrintQueue? SelectedQueue { get; private set; }
    public PrintTicket? SelectedTicket { get; private set; }

    public PrintPreviewDialog(ImageSource preview, string? title = null)
    {
        InitializeComponent();
        PreviewImage.Source = preview;
        if (!string.IsNullOrWhiteSpace(title))
            Title = title;

        OrientationCombo.Items.Add(UiText.OrientationLandscape);
        OrientationCombo.Items.Add(UiText.OrientationPortrait);
        OrientationCombo.SelectedIndex = 0;

        for (var i = 1; i <= 9; i++)
            CopiesCombo.Items.Add(i);
        CopiesCombo.SelectedIndex = 0;

        ColorCombo.Items.Add(UiText.ColorModeColor);
        ColorCombo.Items.Add(UiText.ColorModeGrayscale);
        ColorCombo.SelectedIndex = 0;

        LoadPrinters();
    }

    private void LoadPrinters()
    {
        _loading = true;
        PrinterCombo.Items.Clear();
        _printers.Clear();

        try
        {
            using var server = new LocalPrintServer();
            var queues = server.GetPrintQueues(new[]
            {
                EnumeratedPrintQueueTypes.Local,
                EnumeratedPrintQueueTypes.Connections
            });
            foreach (var queue in queues.OrderBy(q => q.FullName, StringComparer.CurrentCultureIgnoreCase))
            {
                _printers.Add(queue);
                PrinterCombo.Items.Add(queue);
            }
        }
        catch
        {
            // Fallback: system print dialog via "Weitere Einstellungen".
        }

        if (PrinterCombo.Items.Count > 0)
        {
            var defaultName = (string?)null;
            try
            {
                defaultName = LocalPrintServer.GetDefaultPrintQueue()?.FullName;
            }
            catch
            {
                // Default printer is optional.
            }
            var match = !string.IsNullOrWhiteSpace(defaultName)
                ? _printers.FindIndex(q => string.Equals(q.FullName, defaultName, StringComparison.OrdinalIgnoreCase))
                : -1;
            PrinterCombo.SelectedIndex = match >= 0 ? match : 0;
        }

        PrintButton.IsEnabled = PrinterCombo.Items.Count > 0;
        _loading = false;
        ApplyTicketFromUi();
    }

    private void PrinterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        ApplyTicketFromUi();
    }

    private void PrintSetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !IsLoaded)
            return;
        ApplyTicketFromUi();
    }

    private void ApplyTicketFromUi()
    {
        SelectedQueue = PrinterCombo.SelectedItem as PrintQueue;
        if (SelectedQueue == null)
        {
            SelectedTicket = null;
            return;
        }

        var ticket = SelectedQueue.DefaultPrintTicket?.Clone() ?? new PrintTicket();
        ticket.PageOrientation = OrientationCombo.SelectedIndex == 1
            ? PageOrientation.Portrait
            : PageOrientation.Landscape;
        ticket.CopyCount = CopiesCombo.SelectedItem is int copies ? copies : 1;
        ticket.OutputColor = ColorCombo.SelectedIndex == 1
            ? OutputColor.Grayscale
            : OutputColor.Color;
        SelectedTicket = ticket;
    }

    private void MoreSettings_Click(object sender, RoutedEventArgs e)
    {
        ApplyTicketFromUi();
        var dialog = new PrintDialog();
        if (SelectedQueue != null)
            dialog.PrintQueue = SelectedQueue;
        if (SelectedTicket != null)
            dialog.PrintTicket = SelectedTicket;

        if (dialog.ShowDialog() != true)
            return;

        SelectedQueue = dialog.PrintQueue;
        SelectedTicket = dialog.PrintTicket;
        SyncUiFromTicket();
    }

    private void SyncUiFromTicket()
    {
        _loading = true;
        try
        {
            if (SelectedQueue != null)
            {
                var index = _printers.FindIndex(q => string.Equals(q.FullName, SelectedQueue.FullName, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                    PrinterCombo.SelectedIndex = index;
            }

            if (SelectedTicket != null)
            {
                OrientationCombo.SelectedIndex = SelectedTicket.PageOrientation == PageOrientation.Portrait ? 1 : 0;
                var copies = SelectedTicket.CopyCount ?? 1;
                CopiesCombo.SelectedItem = Math.Clamp(copies, 1, 9);
                ColorCombo.SelectedIndex = SelectedTicket.OutputColor == OutputColor.Grayscale ? 1 : 0;
            }
        }
        finally
        {
            _loading = false;
        }
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyTicketFromUi();
        if (SelectedQueue == null)
        {
            MessageBox.Show(UiText.NoPrintersFound, UiText.ActionPrint, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
