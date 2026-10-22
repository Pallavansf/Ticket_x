using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Controllers;

[ApiController, Route("api/v1/auth")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class AuthenticationController(
    ILocalAuthenticationService authentication,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    TimeProvider clock) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await authentication.AuthenticateAsync(request.LoginName, request.Password, cancellationToken);
        if (user is null)
            return Unauthorized(new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "Invalid login name or password." });

        var signingKey = configuration["Auth:Local:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) && environment.IsEnvironment("Testing"))
            signingKey = configuration["Auth:TestSigningKey"];
        var issuer = configuration["Auth:Local:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer) && environment.IsEnvironment("Testing"))
            issuer = configuration["Auth:Issuer"];
        var audience = configuration["Auth:Audience"];
        var lifetimeMinutes = configuration.GetValue<int?>("Auth:Local:TokenLifetimeMinutes") ?? 30;
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32 ||
            string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) ||
            lifetimeMinutes is < 1 or > 1440)
            throw new InvalidOperationException("Local authentication issuer, audience, protected signing key, or token lifetime is invalid.");

        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(lifetimeMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.UniqueName, user.LoginName),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("password_change_required", user.PasswordRotationRequired ? "true" : "false")
        };
        claims.AddRange(user.Roles.Select(role => new Claim("roles", role)));
        claims.AddRange(user.Permissions.Select(permission => new Claim("permissions", permission)));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, now.UtcDateTime, expires.UtcDateTime, credentials);
        return Ok(new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            checked((int)(expires - now).TotalSeconds),
            user.UserId,
            user.LoginName,
            user.DisplayName,
            user.PasswordRotationRequired));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(rawUserId, out var userId) || userId <= 0)
            return Unauthorized();
        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "New password confirmation does not match." });
        var changed = await authentication.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
        return changed
            ? NoContent()
            : Unauthorized(new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "Current password is incorrect." });
    }
}

public sealed record LoginRequest(
    [Required, StringLength(200, MinimumLength = 1)] string LoginName,
    [Required, StringLength(1024, MinimumLength = 1)] string Password);

public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn, long UserId, string LoginName, string DisplayName, bool PasswordRotationRequired);
public sealed record ChangePasswordRequest(
    [Required, StringLength(1024, MinimumLength = 1)] string CurrentPassword,
    [Required, StringLength(1024, MinimumLength = 12)] string NewPassword,
    [Required, StringLength(1024, MinimumLength = 12)] string ConfirmPassword);
