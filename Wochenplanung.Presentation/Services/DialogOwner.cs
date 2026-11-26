using System.Windows;

namespace Wochenplanung.Presentation.Services;

/// <summary>
/// WPF forbids setting Window.Owner to a window that has not been shown yet.
/// That crash hid the real startup error on colleague PCs (only a wait cursor).
/// </summary>
public static class DialogOwner
{
    public static bool CanOwn(Window? owner) =>
        owner is { IsLoaded: true, IsVisible: true };

    public static void Attach(Window dialog, Window? owner)
    {
        if (!CanOwn(owner))
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.ShowInTaskbar = true;
            return;
        }

        dialog.Owner = owner;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }
}
