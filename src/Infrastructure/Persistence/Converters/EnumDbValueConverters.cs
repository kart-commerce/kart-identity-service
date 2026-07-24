using Kart.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kart.Identity.Infrastructure.Persistence.Converters;

/// <summary>
/// database-design.md's CHECK constraints use lowercase/snake_case string
/// vocabularies (e.g. 'support_agent', not 'SupportAgent') — these converters keep
/// the stored values byte-for-byte identical to the approved schema rather than
/// relying on EF's default enum-name string conversion. Uses static methods (not
/// inline switch/throw expressions) because <see cref="ValueConverter{TModel,TProvider}"/>
/// compiles its lambdas as expression trees, which can't contain either construct.
/// </summary>
internal static class EnumDbValueConverters
{
    public static readonly ValueConverter<AccountOrigin, string> AccountOrigin = new(
        v => AccountOriginToDbValue(v),
        v => AccountOriginFromDbValue(v));

    public static readonly ValueConverter<PlatformRole, string> PlatformRole = new(
        v => PlatformRoleToDbValue(v),
        v => PlatformRoleFromDbValue(v));

    public static readonly ValueConverter<SessionRevocationReason?, string?> SessionRevocationReason = new(
        v => SessionRevocationReasonToDbValue(v),
        v => SessionRevocationReasonFromDbValue(v));

    public static readonly ValueConverter<MfaCredentialStatus, string> MfaCredentialStatus = new(
        v => MfaCredentialStatusToDbValue(v),
        v => MfaCredentialStatusFromDbValue(v));

    public static readonly ValueConverter<ServicePrincipalStatus, string> ServicePrincipalStatus = new(
        v => ServicePrincipalStatusToDbValue(v),
        v => ServicePrincipalStatusFromDbValue(v));

    public static readonly ValueConverter<FederatedIdpType, string> FederatedIdpType = new(
        v => FederatedIdpTypeToDbValue(v),
        v => FederatedIdpTypeFromDbValue(v));

    private static string AccountOriginToDbValue(AccountOrigin v) => v switch
    {
        Domain.Enums.AccountOrigin.Native => "native",
        Domain.Enums.AccountOrigin.Social => "social",
        Domain.Enums.AccountOrigin.Enterprise => "enterprise",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static AccountOrigin AccountOriginFromDbValue(string v) => v switch
    {
        "native" => Domain.Enums.AccountOrigin.Native,
        "social" => Domain.Enums.AccountOrigin.Social,
        "enterprise" => Domain.Enums.AccountOrigin.Enterprise,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static string PlatformRoleToDbValue(PlatformRole v) => v switch
    {
        Domain.Enums.PlatformRole.Customer => "customer",
        Domain.Enums.PlatformRole.SupportAgent => "support_agent",
        Domain.Enums.PlatformRole.Admin => "admin",
        Domain.Enums.PlatformRole.PartnerApi => "partner_api",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static PlatformRole PlatformRoleFromDbValue(string v) => v switch
    {
        "customer" => Domain.Enums.PlatformRole.Customer,
        "support_agent" => Domain.Enums.PlatformRole.SupportAgent,
        "admin" => Domain.Enums.PlatformRole.Admin,
        "partner_api" => Domain.Enums.PlatformRole.PartnerApi,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static string? SessionRevocationReasonToDbValue(SessionRevocationReason? v) => v switch
    {
        null => null,
        Domain.Enums.SessionRevocationReason.Logout => "logout",
        Domain.Enums.SessionRevocationReason.ReuseDetected => "reuse_detected",
        Domain.Enums.SessionRevocationReason.AdminLock => "admin_lock",
        Domain.Enums.SessionRevocationReason.RoleChange => "role_change",
        Domain.Enums.SessionRevocationReason.PasswordReset => "password_reset",
        Domain.Enums.SessionRevocationReason.Erasure => "erasure",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static SessionRevocationReason? SessionRevocationReasonFromDbValue(string? v) => v switch
    {
        null => null,
        "logout" => Domain.Enums.SessionRevocationReason.Logout,
        "reuse_detected" => Domain.Enums.SessionRevocationReason.ReuseDetected,
        "admin_lock" => Domain.Enums.SessionRevocationReason.AdminLock,
        "role_change" => Domain.Enums.SessionRevocationReason.RoleChange,
        "password_reset" => Domain.Enums.SessionRevocationReason.PasswordReset,
        "erasure" => Domain.Enums.SessionRevocationReason.Erasure,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static string MfaCredentialStatusToDbValue(MfaCredentialStatus v) => v switch
    {
        Domain.Enums.MfaCredentialStatus.Pending => "pending",
        Domain.Enums.MfaCredentialStatus.Active => "active",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static MfaCredentialStatus MfaCredentialStatusFromDbValue(string v) => v switch
    {
        "pending" => Domain.Enums.MfaCredentialStatus.Pending,
        "active" => Domain.Enums.MfaCredentialStatus.Active,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static string ServicePrincipalStatusToDbValue(ServicePrincipalStatus v) => v switch
    {
        Domain.Enums.ServicePrincipalStatus.Active => "active",
        Domain.Enums.ServicePrincipalStatus.Revoked => "revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static ServicePrincipalStatus ServicePrincipalStatusFromDbValue(string v) => v switch
    {
        "active" => Domain.Enums.ServicePrincipalStatus.Active,
        "revoked" => Domain.Enums.ServicePrincipalStatus.Revoked,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static string FederatedIdpTypeToDbValue(FederatedIdpType v) => v switch
    {
        Domain.Enums.FederatedIdpType.Enterprise => "enterprise",
        Domain.Enums.FederatedIdpType.Social => "social",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static FederatedIdpType FederatedIdpTypeFromDbValue(string v) => v switch
    {
        "enterprise" => Domain.Enums.FederatedIdpType.Enterprise,
        "social" => Domain.Enums.FederatedIdpType.Social,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };
}
