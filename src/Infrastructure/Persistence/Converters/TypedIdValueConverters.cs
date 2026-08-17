using Kart.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kart.Identity.Infrastructure.Persistence.Converters;

/// <summary>
/// One generic <see cref="ValueConverter{TModel,TProvider}"/> factory for every Guid-backed
/// strongly-typed ID in the domain (<see cref="ITypedEntityId{TSelf}"/>), instead of a
/// hand-written converter per ID type — the mapping (unwrap `.Value` in, `TId.From(...)` out) is
/// identical for all of them.
/// </summary>
internal static class TypedIdValueConverters
{
    public static ValueConverter<TId, Guid> For<TId>() where TId : struct, ITypedEntityId<TId>
    {
        // TId.From is a static abstract interface member — referencing it directly inside the
        // conversion lambda below would put an "access of static virtual/abstract interface
        // member" into the expression tree ValueConverter compiles that lambda into, which the
        // compiler rejects (CS8927). Capturing it as an ordinary delegate first, then invoking
        // that delegate inside the lambda, keeps the expression tree itself free of the
        // unsupported construct — the delegate invocation is not the abstract-member reference
        // itself.
        Func<Guid, TId> fromGuid = TId.From;
        return new ValueConverter<TId, Guid>(id => id.Value, value => fromGuid(value));
    }
}
