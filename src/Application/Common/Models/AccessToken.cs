namespace Kart.Identity.Application.Common.Models;

public sealed record AccessToken(string Token, int ExpiresInSeconds);
