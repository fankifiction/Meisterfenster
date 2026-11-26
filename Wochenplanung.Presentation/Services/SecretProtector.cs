using System.Security.Cryptography;
using System.Text;

namespace Wochenplanung.Presentation.Services;

/// <summary>Protects SQL passwords in database.json with Windows DPAPI (current user).</summary>
public static class SecretProtector
{
    private const string Prefix = "dpapi:";

    public static string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;
        if (IsProtected(plaintext))
            return plaintext;

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(bytes, OptionalEntropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
            return string.Empty;
        if (!IsProtected(stored))
            return stored;

        try
        {
            var protectedBytes = Convert.FromBase64String(stored[Prefix.Length..]);
            var bytes = ProtectedData.Unprotect(protectedBytes, OptionalEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    public static bool IsProtected(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("Meisterfenster.database.json");
}
