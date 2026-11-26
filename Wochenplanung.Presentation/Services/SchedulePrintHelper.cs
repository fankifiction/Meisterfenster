using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wochenplanung.Presentation.Views;

namespace Wochenplanung.Presentation.Services;

public static class SchedulePrintHelper
{
    public static bool TryPrint(FrameworkElement header, FrameworkElement body, string jobName, Window? owner = null)
    {
        var bitmap = RenderStacked(header, body) ?? Render(body);
        if (bitmap == null)
            return false;

        var previewTitle = string.IsNullOrWhiteSpace(jobName)
            ? Localization.UiText.PrintPreviewTitle
            : $"{Localization.UiText.PrintPreviewTitle} – {jobName}";
        var preview = new PrintPreviewDialog(bitmap, previewTitle)
        {
            Owner = owner ?? System.Windows.Application.Current?.MainWindow
        };
        if (preview.ShowDialog() != true)
            return false;

        return PrintBitmap(bitmap, jobName, preview.SelectedQueue, preview.SelectedTicket);
    }

    private static RenderTargetBitmap? RenderStacked(FrameworkElement header, FrameworkElement body)
    {
        if (header == null || body == null)
            return null;

        header.UpdateLayout();
        body.UpdateLayout();
        var headerBmp = Render(header);
        var bodyBmp = Render(body);
        if (headerBmp == null || bodyBmp == null)
            return null;

        var dpi = VisualTreeHelper.GetDpi(body);
        var width = Math.Max(header.ActualWidth, body.ActualWidth);
        var height = header.ActualHeight + body.ActualHeight;
        if (width < 8 || height < 8)
            return null;

        var pixelsX = Math.Max(1, (int)Math.Ceiling(width * dpi.PixelsPerInchX / 96.0));
        var pixelsY = Math.Max(1, (int)Math.Ceiling(height * dpi.PixelsPerInchY / 96.0));
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            context.DrawImage(headerBmp, new Rect(0, 0, header.ActualWidth, header.ActualHeight));
            context.DrawImage(bodyBmp, new Rect(0, header.ActualHeight, body.ActualWidth, body.ActualHeight));
        }

        var bitmap = new RenderTargetBitmap(pixelsX, pixelsY, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static RenderTargetBitmap? Render(FrameworkElement visual)
    {
        if (visual == null)
            return null;

        visual.UpdateLayout();
        var width = visual.ActualWidth;
        var height = visual.ActualHeight;
        if (width < 8 || height < 8)
            return null;

        var dpi = VisualTreeHelper.GetDpi(visual);
        var pixelsX = Math.Max(1, (int)Math.Ceiling(width * dpi.PixelsPerInchX / 96.0));
        var pixelsY = Math.Max(1, (int)Math.Ceiling(height * dpi.PixelsPerInchY / 96.0));
        var bitmap = new RenderTargetBitmap(pixelsX, pixelsY, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static bool PrintBitmap(ImageSource bitmap, string jobName, PrintQueue? queue, PrintTicket? ticket)
    {
        var dialog = new PrintDialog();
        if (queue != null)
            dialog.PrintQueue = queue;
        if (ticket != null)
            dialog.PrintTicket = ticket;
        else if (dialog.PrintTicket != null)
            dialog.PrintTicket.PageOrientation = PageOrientation.Landscape;

        var pageWidth = dialog.PrintableAreaWidth;
        var pageHeight = dialog.PrintableAreaHeight;
        if (pageWidth < 1 || pageHeight < 1)
            return false;

        var width = bitmap.Width;
        var height = bitmap.Height;
        if (width < 1 || height < 1)
            return false;

        var fit = Math.Min(pageWidth / width, pageHeight / height);
        var contentWidth = width * fit;
        var contentHeight = height * fit;

        if (contentHeight <= pageHeight + 0.5)
        {
            dialog.PrintVisual(CreatePageVisual(bitmap, contentWidth, contentHeight), jobName);
            return true;
        }

        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);
        var pageCount = (int)Math.Ceiling(contentHeight / pageHeight);
        for (var i = 0; i < pageCount; i++)
        {
            var page = new FixedPage
            {
                Width = pageWidth,
                Height = pageHeight,
                Background = Brushes.White
            };
            page.Clip = new RectangleGeometry(new Rect(0, 0, pageWidth, pageHeight));

            var image = new Image
            {
                Source = bitmap,
                Width = contentWidth,
                Height = contentHeight,
                Stretch = Stretch.Fill
            };
            FixedPage.SetLeft(image, 0);
            FixedPage.SetTop(image, -i * pageHeight);
            page.Children.Add(image);

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(page);
            document.Pages.Add(pageContent);
        }

        dialog.PrintDocument(document.DocumentPaginator, jobName);
        return true;
    }

    private static DrawingVisual CreatePageVisual(ImageSource bitmap, double width, double height)
    {
        var visual = new DrawingVisual();
        using var context = visual.RenderOpen();
        context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
        context.DrawImage(bitmap, new Rect(0, 0, width, height));
        return visual;
    }
}
