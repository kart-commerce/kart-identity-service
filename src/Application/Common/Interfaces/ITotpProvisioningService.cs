using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>Generates a new TOTP (RFC 6238) secret and its provisioning URI (api-contract.yaml POST /auth/mfa/enroll).</summary>
public interface ITotpProvisioningService
{
    TotpEnrollment GenerateEnrollment(string accountLabel);
}
