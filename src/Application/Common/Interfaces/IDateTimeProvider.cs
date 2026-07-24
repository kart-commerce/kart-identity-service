namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>Testability seam for "now" — session/refresh-token expiry math must be deterministic in tests.</summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
