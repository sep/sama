using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SAMA.Data.Entities;
using SAMA.Web.Constants;
using SAMA.Web.Services;

namespace SAMA.Tests.Integration.Web.Services;

[TestClass]
public class WorkspaceAuthorizationServiceTests : IntegrationTestBase
{
    private WorkspaceAuthorizationService _service = null!;
    private UserManager<ApplicationUser> _userManager = null!;
    private RoleManager<IdentityRole<Guid>> _roleManager = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _userManager = ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _roleManager = ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        await EnsureAdminRoleExistsAsync();

        _service = new WorkspaceAuthorizationService(DbContext, _userManager);
    }

    [TestMethod]
    public async Task CanViewWorkspaceShouldReturnTrueForGlobalAdmin()
    {
        var admin = await CreateUserAsync("admin@example.com", isAdmin: true);
        var workspace = await CreateWorkspaceAsync("Test Workspace", isPublic: false);

        var result = await _service.CanViewWorkspace(admin.Id, workspace.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task CanViewWorkspaceShouldReturnTrueForWorkspaceEditor()
    {
        var user = await CreateUserAsync("editor@example.com");
        var workspace = await CreateWorkspaceAsync("Test Workspace", isPublic: false);
        await AddUserToWorkspaceAsync(user.Id, workspace.Id, AuthConstants.EditorRole);

        var result = await _service.CanViewWorkspace(user.Id, workspace.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task CanViewWorkspaceShouldReturnTrueForWorkspaceViewer()
    {
        var user = await CreateUserAsync("viewer@example.com");
        var workspace = await CreateWorkspaceAsync("Test Workspace", isPublic: false);
        await AddUserToWorkspaceAsync(user.Id, workspace.Id, AuthConstants.ViewerRole);

        var result = await _service.CanViewWorkspace(user.Id, workspace.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task CanViewWorkspaceShouldReturnTrueForPublicWorkspace()
    {
        var user = await CreateUserAsync("user@example.com");
        var workspace = await CreateWorkspaceAsync("Public Workspace", isPublic: true);

        var result = await _service.CanViewWorkspace(user.Id, workspace.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task CanViewWorkspaceShouldReturnTrueForPublicWorkspaceForGuests()
    {
        var workspace = await CreateWorkspaceAsync("Public Workspace", isPublic: true);

        var result = await _service.CanViewWorkspace(null, workspace.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task CanViewWorkspaceShouldReturnFalseForPrivateWorkspaceWithoutAccess()
    {
        var user = await CreateUserAsync("user@example.com");
        var workspace = await CreateWorkspaceAsync("Private Workspace", isPublic: false);

        var result = await _service.CanViewWorkspace(user.Id, workspace.Id);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task CanViewWorkspaceShouldReturnFalseForPrivateWorkspaceForGuests()
    {
        var workspace = await CreateWorkspaceAsync("Private Workspace", isPublic: false);

        var result = await _service.CanViewWorkspace(null, workspace.Id);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task CanEditWorkspaceShouldReturnTrueForGlobalAdmin()
    {
        var admin = await CreateUserAsync("admin@example.com", isAdmin: true);
        var workspace = await CreateWorkspaceAsync("Test Workspace", isPublic: false);

        var result = await _service.CanEditWorkspace(admin.Id, workspace.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task CanEditWorkspaceShouldReturnTrueForWorkspaceEditor()
    {
        var user = await CreateUserAsync("editor@example.com");
        var workspace = await CreateWorkspaceAsync("Test Workspace", isPublic: false);
        await AddUserToWorkspaceAsync(user.Id, workspace.Id, AuthConstants.EditorRole);

        var result = await _service.CanEditWorkspace(user.Id, workspace.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task CanEditWorkspaceShouldReturnFalseForWorkspaceViewer()
    {
        var user = await CreateUserAsync("viewer@example.com");
        var workspace = await CreateWorkspaceAsync("Test Workspace", isPublic: false);
        await AddUserToWorkspaceAsync(user.Id, workspace.Id, AuthConstants.ViewerRole);

        var result = await _service.CanEditWorkspace(user.Id, workspace.Id);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task CanEditWorkspaceShouldReturnFalseForNonMember()
    {
        var user = await CreateUserAsync("user@example.com");
        var workspace = await CreateWorkspaceAsync("Test Workspace", isPublic: false);

        var result = await _service.CanEditWorkspace(user.Id, workspace.Id);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task CanEditWorkspaceShouldReturnFalseForPublicWorkspaceNonMember()
    {
        var user = await CreateUserAsync("user@example.com");
        var workspace = await CreateWorkspaceAsync("Public Workspace", isPublic: true);

        var result = await _service.CanEditWorkspace(user.Id, workspace.Id);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task IsGlobalAdminShouldReturnTrueForAdmin()
    {
        var admin = await CreateUserAsync("admin@example.com", isAdmin: true);

        var result = await _service.IsGlobalAdmin(admin.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsGlobalAdminShouldReturnFalseForNonAdmin()
    {
        var user = await CreateUserAsync("user@example.com");

        var result = await _service.IsGlobalAdmin(user.Id);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task IsGlobalAdminShouldReturnFalseForNonExistentUser()
    {
        var result = await _service.IsGlobalAdmin(Guid.NewGuid());

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetAccessibleWorkspaceIdsShouldReturnAllWorkspacesForAdmin()
    {
        var admin = await CreateUserAsync("admin@example.com", isAdmin: true);
        var workspace1 = await CreateWorkspaceAsync("Workspace 1", isPublic: false);
        var workspace2 = await CreateWorkspaceAsync("Workspace 2", isPublic: true);
        var workspace3 = await CreateWorkspaceAsync("Workspace 3", isPublic: false);

        var result = await _service.GetAccessibleWorkspaceIds(admin.Id);

        Assert.HasCount(3, result);
        Assert.Contains(workspace1.Id, result);
        Assert.Contains(workspace2.Id, result);
        Assert.Contains(workspace3.Id, result);
    }

    [TestMethod]
    public async Task GetAccessibleWorkspaceIdsShouldReturnUserWorkspacesAndPublicWorkspaces()
    {
        var user = await CreateUserAsync("user@example.com");
        var privateWorkspace1 = await CreateWorkspaceAsync("Private 1", isPublic: false);
        var privateWorkspace2 = await CreateWorkspaceAsync("Private 2", isPublic: false);
        var publicWorkspace = await CreateWorkspaceAsync("Public", isPublic: true);
        await AddUserToWorkspaceAsync(user.Id, privateWorkspace1.Id, AuthConstants.EditorRole);

        var result = await _service.GetAccessibleWorkspaceIds(user.Id);

        Assert.HasCount(2, result);
        Assert.Contains(privateWorkspace1.Id, result);
        Assert.Contains(publicWorkspace.Id, result);
        Assert.DoesNotContain(privateWorkspace2.Id, result);
    }

    [TestMethod]
    public async Task GetAccessibleWorkspaceIdsShouldReturnOnlyPublicWorkspacesForNonMember()
    {
        var user = await CreateUserAsync("user@example.com");
        await CreateWorkspaceAsync("Private 1", isPublic: false);
        await CreateWorkspaceAsync("Private 2", isPublic: false);
        var publicWorkspace = await CreateWorkspaceAsync("Public", isPublic: true);

        var result = await _service.GetAccessibleWorkspaceIds(user.Id);

        Assert.HasCount(1, result);
        Assert.Contains(publicWorkspace.Id, result);
    }

    [TestMethod]
    public async Task GetAccessibleWorkspaceIdsShouldReturnEmptyListWhenNoWorkspaces()
    {
        var user = await CreateUserAsync("user@example.com");

        var result = await _service.GetAccessibleWorkspaceIds(user.Id);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetAccessibleWorkspaceIdsShouldNotDuplicateWorkspaceIds()
    {
        var user = await CreateUserAsync("user@example.com");
        var workspace = await CreateWorkspaceAsync("Workspace", isPublic: true);
        await AddUserToWorkspaceAsync(user.Id, workspace.Id, AuthConstants.EditorRole);

        var result = await _service.GetAccessibleWorkspaceIds(user.Id);

        Assert.HasCount(1, result);
        Assert.AreEqual(workspace.Id, result[0]);
    }

    [TestMethod]
    public async Task GetAccessibleWorkspaceIdsShouldReturnOnlyPublicWorkspacesForGuests()
    {
        await CreateWorkspaceAsync("Private 1", isPublic: false);
        await CreateWorkspaceAsync("Private 2", isPublic: false);
        var publicWorkspace1 = await CreateWorkspaceAsync("Public 1", isPublic: true);
        var publicWorkspace2 = await CreateWorkspaceAsync("Public 2", isPublic: true);

        var result = await _service.GetAccessibleWorkspaceIds(null);

        Assert.HasCount(2, result);
        Assert.Contains(publicWorkspace1.Id, result);
        Assert.Contains(publicWorkspace2.Id, result);
    }

    [TestMethod]
    public async Task GetAccessibleWorkspaceIdsShouldReturnEmptyListForGuestsWhenNoPublicWorkspaces()
    {
        await CreateWorkspaceAsync("Private 1", isPublic: false);
        await CreateWorkspaceAsync("Private 2", isPublic: false);

        var result = await _service.GetAccessibleWorkspaceIds(null);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetWorkspaceMembersShouldReturnEmptyListWhenNoMembers()
    {
        var workspace = await CreateWorkspaceAsync("Test Workspace", isPublic: false);

        var result = await _service.GetWorkspaceMembers(workspace.Id);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetWorkspaceMembersShouldReturnAllMembersOrderedByEmail()
    {
        var workspace = await CreateWorkspaceAsync("Test Workspace", isPublic: false);
        var user1 = await CreateUserAsync("zebra@example.com");
        var user2 = await CreateUserAsync("alpha@example.com");
        var user3 = await CreateUserAsync("beta@example.com");

        await AddUserToWorkspaceAsync(user1.Id, workspace.Id, AuthConstants.EditorRole);
        await AddUserToWorkspaceAsync(user2.Id, workspace.Id, AuthConstants.ViewerRole);
        await AddUserToWorkspaceAsync(user3.Id, workspace.Id, AuthConstants.EditorRole);

        var result = await _service.GetWorkspaceMembers(workspace.Id);

        Assert.HasCount(3, result);
        Assert.AreEqual("alpha@example.com", result[0].Email);
        Assert.AreEqual("beta@example.com", result[1].Email);
        Assert.AreEqual("zebra@example.com", result[2].Email);
    }

    [TestMethod]
    public async Task GetWorkspaceMembersShouldIncludeAllProperties()
    {
        var workspace = await CreateWorkspaceAsync("Test Workspace", isPublic: false);
        var user = await CreateUserAsync("user@example.com");
        await AddUserToWorkspaceAsync(user.Id, workspace.Id, AuthConstants.EditorRole, AuthConstants.ManualSource);

        var result = await _service.GetWorkspaceMembers(workspace.Id);

        var member = result.Single();
        Assert.AreEqual(user.Id, member.UserId);
        Assert.AreEqual("user@example.com", member.Email);
        Assert.AreEqual(AuthConstants.EditorRole, member.Role);
        Assert.AreEqual(AuthConstants.ManualSource, member.Source);
        Assert.AreNotEqual(DateTimeOffset.MinValue, member.CreatedAt);
    }

    [TestMethod]
    public async Task GetWorkspaceMembersShouldNotIncludeMembersFromOtherWorkspaces()
    {
        var workspace1 = await CreateWorkspaceAsync("Workspace 1", isPublic: false);
        var workspace2 = await CreateWorkspaceAsync("Workspace 2", isPublic: false);
        var user1 = await CreateUserAsync("user1@example.com");
        var user2 = await CreateUserAsync("user2@example.com");

        await AddUserToWorkspaceAsync(user1.Id, workspace1.Id, AuthConstants.EditorRole);
        await AddUserToWorkspaceAsync(user2.Id, workspace2.Id, AuthConstants.ViewerRole);

        var result = await _service.GetWorkspaceMembers(workspace1.Id);

        Assert.HasCount(1, result);
        Assert.AreEqual(user1.Id, result[0].UserId);
    }

    [TestMethod]
    public async Task GetWorkspaceMembersShouldDistinguishBetweenManualAndOidcSources()
    {
        var workspace = await CreateWorkspaceAsync("Test Workspace", isPublic: false);
        var user1 = await CreateUserAsync("manual@example.com");
        var user2 = await CreateUserAsync("oidc@example.com");

        await AddUserToWorkspaceAsync(user1.Id, workspace.Id, AuthConstants.EditorRole, AuthConstants.ManualSource);
        await AddUserToWorkspaceAsync(user2.Id, workspace.Id, AuthConstants.ViewerRole, AuthConstants.OidcSource);

        var result = await _service.GetWorkspaceMembers(workspace.Id);

        Assert.HasCount(2, result);
        Assert.AreEqual(AuthConstants.ManualSource, result.First(m => m.Email == "manual@example.com").Source);
        Assert.AreEqual(AuthConstants.OidcSource, result.First(m => m.Email == "oidc@example.com").Source);
    }

    private async Task EnsureAdminRoleExistsAsync()
    {
        if (!await _roleManager.RoleExistsAsync(AuthConstants.AdminRole))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(AuthConstants.AdminRole));
        }
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, bool isAdmin = false)
    {
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

        if (isAdmin)
        {
            await _userManager.AddToRoleAsync(user, AuthConstants.AdminRole);
        }

        DbContext.ChangeTracker.Clear();
        return user;
    }

    private async Task<Workspace> CreateWorkspaceAsync(string name, bool isPublic)
    {
        var workspace = new Workspace
        {
            Name = name,
            IsPublic = isPublic,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return workspace;
    }

    private async Task AddUserToWorkspaceAsync(Guid userId, Guid workspaceId, string role, string source = "Manual")
    {
        var userWorkspace = new UserWorkspace
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = role,
            Source = source,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.UserWorkspaces.Add(userWorkspace);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }
}
