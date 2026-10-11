using System.Security.Cryptography;
using System.Text;

namespace Puntjes.Sdk.Auth;

internal static class TokenKey
{
    public static string For(string host, string clientId, string clientSecret) =>
        "puntjes:token:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{host}|{clientId}|{clientSecret}")))[..32].ToLowerInvariant();
}
