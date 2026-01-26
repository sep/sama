using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SAMA.Data.Entities;
using SAMA.Web.Services;

namespace SAMA.Tests.Integration.Web.Services;

[TestClass]
public class UserPreferencesServiceTests : IntegrationTestBase
{
    private UserPreferencesService _service = null!;
    private UserManager<ApplicationUser> _userManager = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _userManager = ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _service = new UserPreferencesService(_userManager);
    }

    [TestMethod]
    public async Task GetDefaultWorkspaceIdAsyncShouldReturnNullWhenNotSet()
    {
        var user = await CreateUserAsync();

        var result = await _service.GetDefaultWorkspaceIdAsync(user);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SetDefaultWorkspaceIdAsyncShouldAddClaim()
    {
        var user = await CreateUserAsync();
        var workspaceId = Guid.NewGuid();

        var result = await _service.SetDefaultWorkspaceIdAsync(user, workspaceId);

        Assert.IsTrue(result.Succeeded);
        var storedId = await _service.GetDefaultWorkspaceIdAsync(user);
        Assert.AreEqual(workspaceId, storedId);
    }

    [TestMethod]
    public async Task SetDefaultWorkspaceIdAsyncShouldReplacePreviousValue()
    {
        var user = await CreateUserAsync();
        var firstWorkspaceId = Guid.NewGuid();
        var secondWorkspaceId = Guid.NewGuid();

        await _service.SetDefaultWorkspaceIdAsync(user, firstWorkspaceId);
        var result = await _service.SetDefaultWorkspaceIdAsync(user, secondWorkspaceId);

        Assert.IsTrue(result.Succeeded);
        var storedId = await _service.GetDefaultWorkspaceIdAsync(user);
        Assert.AreEqual(secondWorkspaceId, storedId);
    }

    [TestMethod]
    public async Task SetDefaultWorkspaceIdAsyncShouldClearValueWhenNull()
    {
        var user = await CreateUserAsync();
        var workspaceId = Guid.NewGuid();

        await _service.SetDefaultWorkspaceIdAsync(user, workspaceId);
        var result = await _service.SetDefaultWorkspaceIdAsync(user, null);

        Assert.IsTrue(result.Succeeded);
        var storedId = await _service.GetDefaultWorkspaceIdAsync(user);
        Assert.IsNull(storedId);
    }

    [TestMethod]
    public async Task GetDefaultWorkspaceIdAsyncShouldReturnNullForInvalidGuidClaim()
    {
        var user = await CreateUserAsync();
        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim(
            UserPreferencesService.DefaultWorkspaceIdClaimType, "not-a-guid"));

        var result = await _service.GetDefaultWorkspaceIdAsync(user);

        Assert.IsNull(result);
    }

    private async Task<ApplicationUser> CreateUserAsync()
    {
        var email = $"user{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, "Test-Password-1234567");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await DbContext.SaveChangesAsync();
        return user;
    }
}
