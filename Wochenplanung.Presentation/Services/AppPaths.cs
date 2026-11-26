using System.IO;

namespace Wochenplanung.Presentation.Services;

/// <summary>
/// Local data for Meisterfenster 2. Kept apart from Wochenplanung 1.4.x
/// (%LOCALAPPDATA%\Wochenplanung) so both can run on the same PC.
/// </summary>
public static class AppPaths
{
    public const string AppDataFolderName = "Meisterfenster";

    public static string Root
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppDataFolderName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string Combine(params string[] parts) =>
        Path.Combine(new[] { Root }.Concat(parts).ToArray());
}
