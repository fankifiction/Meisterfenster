using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Wochenplanung.Presentation.Localization;

internal sealed record UiTranslation(string De, string En, string Fr, string It);

public static class UiCatalog
{
    private static readonly IReadOnlyDictionary<string, UiTranslation> Entries = Load();

    public static string Get(string de, string en, string fr, string it)
    {
        if (Entries.TryGetValue(de, out var entry))
            return Pick(entry);

        return Pick(new UiTranslation(de, en, fr, it));
    }

    private static string Pick(UiTranslation translation)
    {
        var code = CultureInfo.CurrentUICulture.Name;
        if (code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return translation.En;
        if (code.StartsWith("fr", StringComparison.OrdinalIgnoreCase)) return translation.Fr;
        if (code.StartsWith("it", StringComparison.OrdinalIgnoreCase)) return translation.It;
        return translation.De;
    }

    private static IReadOnlyDictionary<string, UiTranslation> Load()
    {
        var assembly = typeof(UiCatalog).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("strings.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName == null)
            return new Dictionary<string, UiTranslation>();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return new Dictionary<string, UiTranslation>();

        using var reader = new StreamReader(stream);
        var list = JsonSerializer.Deserialize<List<UiTranslation>>(reader.ReadToEnd(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<UiTranslation>();

        return list
            .Where(entry => !string.IsNullOrWhiteSpace(entry.De))
            .GroupBy(entry => entry.De)
            .ToDictionary(group => group.Key, group => group.First());
    }
}
