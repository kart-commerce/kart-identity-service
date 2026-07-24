using System.Security.Cryptography;
using Kart.Identity.Application.Common.Interfaces;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>256 bits of CSPRNG entropy, URL-safe encoded — the raw refresh-token value returned to the client exactly once.</summary>
public sealed class SecureOpaqueTokenGenerator : IOpaqueTokenGenerator
{
    private const int TokenSizeInBytes = 32;

    public string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenSizeInBytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
