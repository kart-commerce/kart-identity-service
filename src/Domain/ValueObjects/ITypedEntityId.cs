namespace Kart.Identity.Domain.ValueObjects;

/// <summary>
/// Shared shape every Guid-backed strongly-typed entity ID in this domain implements. Exists so
/// Infrastructure's generic <c>TypedIdValueConverter&lt;TId&gt;</c> (Persistence/Converters) can
/// map any of them to/from a `uuid` column without a bespoke <c>ValueConverter</c> per ID type —
/// the identity concept itself (a validated wrapper around a single Guid) lives here in the
/// domain; Infrastructure only needs a uniform way to unwrap/rewrap it.
/// </summary>
public interface ITypedEntityId<TSelf> where TSelf : struct, ITypedEntityId<TSelf>
{
    Guid Value { get; }

    static abstract TSelf From(Guid value);
}
