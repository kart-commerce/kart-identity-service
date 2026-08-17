using Kart.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kart.Identity.Infrastructure.Persistence.Converters;

/// <summary>
/// <see cref="ValueConverter{TModel,TProvider}"/>s for this domain's string-backed value objects
/// (as opposed to the Guid-backed strongly-typed IDs — see <see cref="TypedIdValueConverters"/>).
/// </summary>
internal static class DomainValueConverters
{
    public static readonly ValueConverter<EmailAddress, string> EmailAddress = new(
        email => email.Value,
        value => Domain.ValueObjects.EmailAddress.From(value));

    public static readonly ValueConverter<ServicePrincipalClientId, string> ServicePrincipalClientId = new(
        id => id.Value,
        value => Domain.ValueObjects.ServicePrincipalClientId.From(value));
}
