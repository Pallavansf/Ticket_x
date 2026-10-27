using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Api.Controllers;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class FeatureSwitchControllerTests
{
    [Fact]
    public void Administrative_and_availability_controllers_require_authentication()
    {
        Assert.NotNull(typeof(FeatureSwitchAdministrationController).GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
        Assert.NotNull(typeof(FeatureAvailabilityController).GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
    }

    [Fact]
    public async Task Update_rejects_missing_boolean_without_calling_the_service()
    {
        var service = new StubSwitchService();
        var controller = CreateController(service);

        var result = await controller.Set("CHAT", new SetFeatureSwitchRequest(null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(service.SetCalled);
    }

    [Fact]
    public async Task Authorized_update_forwards_actor_scope_reason_and_version()
    {
        var service = new StubSwitchService();
        var controller = CreateController(service);

        var result = await controller.Set("CHAT", new SetFeatureSwitchRequest(false, 18, 3, "maintenance"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal((71L, "CHAT", 18L, false, 3, "maintenance"), service.LastSet);
    }

    [Fact]
    public async Task Repository_rejects_read_and_write_without_CONFIG_ADMIN_before_persistence_access()
    {
        var authorization = new DenyingAuthorization();
        var db = new PA.Tickets.Infrastructure.Persistence.TicketsDbContext(
            new DbContextOptionsBuilder<PA.Tickets.Infrastructure.Persistence.TicketsDbContext>()
                .UseSqlite("Data Source=:memory:").Options);
        var repository = new PA.Tickets.Infrastructure.Foundations.FeatureSwitchRepository(db, authorization,
            auditWriter: null!, TimeProvider.System);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.ListAsync(71, null, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.SetAsync(71, "CHAT", null, true,
            0, "reason", CancellationToken.None));

        Assert.Equal(2, authorization.Calls);
        Assert.All(authorization.Permissions, permission => Assert.Equal("CONFIG_ADMIN", permission));
    }

    private static FeatureSwitchAdministrationController CreateController(StubSwitchService service)
    {
        var controller = new FeatureSwitchAdministrationController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "71")], "test"))
                }
            }
        };
        return controller;
    }

    private sealed class StubSwitchService : IFeatureSwitchService
    {
        public bool SetCalled { get; private set; }
        public (long Actor, string Code, long? DepartmentId, bool Enabled, int? Version, string? Reason)? LastSet { get; private set; }
        public Task<IReadOnlyCollection<FeatureSwitchView>> ListAsync(long actor, long? departmentId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<FeatureSwitchView>>([]);
        public Task<FeatureSwitchView> SetAsync(long actor, string code, long? departmentId, bool enabled, int? expectedRowVersion, string? reason, CancellationToken ct)
        {
            SetCalled = true;
            LastSet = (actor, code, departmentId, enabled, expectedRowVersion, reason);
            return Task.FromResult(new FeatureSwitchView(code, "Chat", departmentId, enabled, true, false, expectedRowVersion.GetValueOrDefault() + 1));
        }
        public Task<bool> IsEnabledAsync(string code, long? departmentId, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<FeatureAvailabilityView>> ForActorAsync(long actor, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<FeatureAvailabilityView>>([]);
    }

    private sealed class DenyingAuthorization : PA.Tickets.Application.Foundations.IAuthorizationService
    {
        public int Calls { get; private set; }
        public List<string> Permissions { get; } = [];
        public Task EnsurePermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct)
        {
            Calls++;
            Permissions.Add(permission);
            throw new UnauthorizedAccessException();
        }
        public Task<bool> HasPermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct) => Task.FromResult(false);
    }
}
