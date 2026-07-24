using Kart.Identity.Application.Common.Interfaces;

namespace Kart.Identity.Infrastructure.Security;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
