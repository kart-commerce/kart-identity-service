using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Application.Features.GetJwks;

public sealed record GetJwksResponse(IReadOnlyList<JsonWebKey> Keys);
