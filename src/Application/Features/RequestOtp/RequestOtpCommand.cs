using MediatR;

namespace Kart.Identity.Application.Features.RequestOtp;

/// <summary>
/// User Registration, Login &amp; Authentication Journey — "Email/Phone → OTP" step.
/// <see cref="IpAddress"/> is resolved by the Api layer, not part of the request body,
/// same convention as LoginCommand.
/// </summary>
public sealed record RequestOtpCommand(string Email, string IpAddress) : IRequest;
