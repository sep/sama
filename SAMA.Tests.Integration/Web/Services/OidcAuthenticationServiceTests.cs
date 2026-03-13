using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Web.Constants;
using SAMA.Web.Services;

namespace SAMA.Tests.Integration.Web.Services;

[TestClass]
public class OidcAuthenticationServiceTests : IntegrationTestBase
{
    private UserManager<ApplicationUser> _userManager = null!;
    private RoleManager<IdentityRole<Guid>> _roleManager = null!;
    private OidcAuthenticationService _service = null!;
    private GlobalSettingsService _globalSettings = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _userManager = ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _roleManager = ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        _globalSettings = ServiceProvider.GetRequiredService<GlobalSettingsService>();

        await EnsureAdminRoleExistsAsync();

        var groupMappingSync = new GroupMappingSyncService(Substitute.For<ILogger<GroupMappingSyncService>>());
        _service = new OidcAuthenticationService(_globalSettings, groupMappingSync, ServiceProvider, Substitute.For<ILogger<OidcAuthenticationService>>());
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldCreateNewUser()
    {
        var principal = CreatePrincipal("newuser@example.com", "sub-123");

        var user = await _service.ProvisionOrUpdateUserAsync(principal);

        Assert.IsNotNull(user);
        Assert.AreEqual("newuser@example.com", user.Email);
        Assert.IsTrue(user.EmailConfirmed);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldAddOidcLoginToExistingUser()
    {
        var existingUser = await CreateUserAsync("existing@example.com");

        var principal = CreatePrincipal("existing@example.com", "sub-456");

        var user = await _service.ProvisionOrUpdateUserAsync(principal);

        Assert.AreEqual(existingUser.Id, user.Id);

        var logins = await _userManager.GetLoginsAsync(user);
        Assert.IsTrue(logins.Any(l => l.LoginProvider == AuthConstants.OidcSource));
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldNotDuplicateOidcLogin()
    {
        var principal = CreatePrincipal("repeat@example.com", "sub-789");

        await _service.ProvisionOrUpdateUserAsync(principal);
        await _service.ProvisionOrUpdateUserAsync(principal);

        var user = await _userManager.FindByEmailAsync("repeat@example.com");
        var logins = await _userManager.GetLoginsAsync(user!);
        Assert.AreEqual(1, logins.Count(l => l.LoginProvider == AuthConstants.OidcSource));
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldThrowWhenNoEmailClaim()
    {
        var claims = new List<Claim> { new("sub", "sub-no-email") };
        var identity = new ClaimsIdentity(claims, AuthConstants.OidcSource);
        var principal = new ClaimsPrincipal(identity);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.ProvisionOrUpdateUserAsync(principal));
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldNotAffectLdapMappings()
    {
        _globalSettings.OidcGroupClaimType = "groups";
        var workspace = await CreateWorkspaceAsync("Mixed Workspace");
        await CreateGroupMappingAsync(workspace.Id, AuthConstants.LdapSource, "ldap-team", AuthConstants.EditorRole);

        var principal = CreatePrincipal("mixed@example.com", "sub-mixed", ["ldap-team"]);

        var user = await _service.ProvisionOrUpdateUserAsync(principal);

        var assignments = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id)
            .ToListAsync();

        Assert.AreEqual(0, assignments.Count);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldUseConfiguredGroupClaimType()
    {
        _globalSettings.OidcGroupClaimType = "roles";
        await CreateGroupMappingAsync(null, AuthConstants.OidcSource, "admin-role", AuthConstants.AdminRole);

        var claims = new List<Claim>
        {
            new("sub", "sub-custom-claim"),
            new("email", "customclaim@example.com"),
            new("roles", "admin-role"),
        };
        var identity = new ClaimsIdentity(claims, AuthConstants.OidcSource);
        var principal = new ClaimsPrincipal(identity);

        var user = await _service.ProvisionOrUpdateUserAsync(principal);

        DbContext.ChangeTracker.Clear();
        var refreshedUser = await _userManager.FindByEmailAsync("customclaim@example.com");
        Assert.IsTrue(await _userManager.IsInRoleAsync(refreshedUser!, AuthConstants.AdminRole));
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldUseConfiguredEmailClaimType()
    {
        _globalSettings.OidcEmailClaimType = "preferred_username";

        var claims = new List<Claim>
        {
            new("sub", "sub-custom-email"),
            new("preferred_username", "upn-user@example.com"),
        };
        var identity = new ClaimsIdentity(claims, AuthConstants.OidcSource);
        var principal = new ClaimsPrincipal(identity);

        var user = await _service.ProvisionOrUpdateUserAsync(principal);

        Assert.AreEqual("upn-user@example.com", user.Email);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldSkipGroupsWhenClaimTypeEmpty()
    {
        _globalSettings.OidcGroupClaimType = "";
        await CreateGroupMappingAsync(null, AuthConstants.OidcSource, "admins-group", AuthConstants.AdminRole);

        var principal = CreatePrincipal("noclaimtype@example.com", "sub-no-ct", ["admins-group"]);

        var user = await _service.ProvisionOrUpdateUserAsync(principal);

        DbContext.ChangeTracker.Clear();
        var refreshedUser = await _userManager.FindByEmailAsync("noclaimtype@example.com");
        Assert.IsFalse(await _userManager.IsInRoleAsync(refreshedUser!, AuthConstants.AdminRole));
    }

    private static ClaimsPrincipal CreatePrincipal(string email, string subject, List<string>? groups = null)
    {
        var claims = new List<Claim>
        {
            new("sub", subject),
            new("email", email),
            new("name", email),
        };

        if (groups != null)
        {
            claims.AddRange(groups.Select(g => new Claim("groups", g)));
        }

        var identity = new ClaimsIdentity(claims, AuthConstants.OidcSource);
        return new ClaimsPrincipal(identity);
    }

    private async Task EnsureAdminRoleExistsAsync()
    {
        if (!await _roleManager.RoleExistsAsync(AuthConstants.AdminRole))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(AuthConstants.AdminRole));
        }
    }

    private async Task<ApplicationUser> CreateUserAsync(string email)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, "Test-Password-123456789!");
        Assert.IsTrue(result.Succeeded, $"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        DbContext.ChangeTracker.Clear();
        return user;
    }

    private async Task<Workspace> CreateWorkspaceAsync(string name)
    {
        var workspace = new Workspace
        {
            Name = name,
            IsPublic = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return workspace;
    }

    private async Task CreateGroupMappingAsync(Guid? workspaceId, string identityProvider, string externalGroupId, string role)
    {
        DbContext.WorkspaceGroupMappings.Add(new WorkspaceGroupMapping
        {
            WorkspaceId = workspaceId,
            IdentityProvider = identityProvider,
            ExternalGroupId = externalGroupId,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }
    }
}
