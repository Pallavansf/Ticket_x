namespace PA.Tickets.Application.Foundations;
public enum MfaMode { Disabled, Optional, Required }
public enum SecurityEventType { MfaEnrolled, MfaEnabled, MfaDisabled, MfaSuccess, MfaFailure, MfaRecovery }
public sealed record AuthenticationProviderOptions(bool Enabled,string ProviderType,string? Authority,string? ClientId,string[] Scopes,string? SecretReference,IDictionary<string,string> ClaimsMapping,IDictionary<string,string> RoleMapping);
public sealed record MfaPolicy(string Code,MfaMode Mode,DateTimeOffset EffectiveFrom,DateTimeOffset? EffectiveTo,string? ScopeRule);
public sealed record MfaEnrollment(long UserId,string Method,string Status,DateTimeOffset EnrolledAt,DateTimeOffset? LastUsedAt);
public interface IMfaService { Task<MfaEnrollment> EnrollTotpAsync(long userId,CancellationToken ct); Task<bool> VerifyAsync(long userId,string code,CancellationToken ct); Task DisableAsync(long userId,long actor,string reason,CancellationToken ct); }
public interface ISecurityEventWriter { Task WriteAsync(SecurityEventType type,long? userId,string result,string? reason,CancellationToken ct); }
public interface IExternalIdentityProvider { string ProviderType { get; } Task<bool> ValidateAsync(string token,CancellationToken ct); }
