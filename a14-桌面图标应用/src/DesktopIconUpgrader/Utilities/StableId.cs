using System.Security.Cryptography;
using System.Text;

namespace DesktopIconUpgrader.Utilities;

internal static class StableId
{
    public static string For(string kind, string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}|{value.ToUpperInvariant()}"));
        return Convert.ToHexString(bytes)[..20];
    }
}
