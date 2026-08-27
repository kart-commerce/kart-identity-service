using Kart.Identity.Application.Features.Login;
using MediatR;

namespace Kart.Identity.Application.Features.VerifyOtp;

/// <summary>
/// User Registration, Login &amp; Authentication Journey — "OTP" step. Reuses Login's
/// <see cref="LoginResult"/> hierarchy: a verified code mints tokens exactly like a
/// verified password does, including the same mandatory-MFA-role gate.
/// </summary>
public sealed record VerifyOtpCommand(string Email, string Code, string IpAddress) : IRequest<LoginResult>;
