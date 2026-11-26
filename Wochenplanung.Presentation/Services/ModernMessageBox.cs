using System.Windows;
using Wochenplanung.Presentation.Views;

namespace Wochenplanung.Presentation.Services;

public static class ModernMessageBox
{
    public static MessageBoxResult Show(
        string messageBoxText,
        string caption = "",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None)
    {
        var dialog = new ModernMessageDialog(messageBoxText, caption, button);
        DialogOwner.Attach(dialog, System.Windows.Application.Current?.MainWindow);

        var result = dialog.ShowDialog();
        return button switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.OKCancel => result == true ? MessageBoxResult.OK : MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => result == true ? MessageBoxResult.Yes : MessageBoxResult.No,
            MessageBoxButton.YesNoCancel when result == true => MessageBoxResult.Yes,
            MessageBoxButton.YesNoCancel when result == false => MessageBoxResult.No,
            _ => MessageBoxResult.Cancel
        };
    }
}
