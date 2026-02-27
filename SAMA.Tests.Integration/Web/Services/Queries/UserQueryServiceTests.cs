using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SAMA.Data.Entities;
using SAMA.Web.Constants;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Integration.Web.Services.Queries;

[TestClass]
public class UserQueryServiceTests : IntegrationTestBase
{
    private UserQueryService _service = null!;
    private UserManager<ApplicationUser> _userManager = null!;
    private RoleManager<IdentityRole<Guid>> _roleManager = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _userManager = ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _roleManager = ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        _service = new UserQueryService(_userManager, DbContext);

        await EnsureAdminRoleExistsAsync();
    }

    [TestMethod]
    public async Task GetUserByIdAsyncShouldReturnNullWhenUserDoesNotExist()
    {
        var result = await _service.GetUserByIdAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetUserByIdAsyncShouldReturnUserWithBasicProperties()
    {
        var user = await CreateUserAsync("test@example.com", false);

        var result = await _service.GetUserByIdAsync(user.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(user.Id, result.Id);
        Assert.AreEqual("test@example.com", result.Email);
        Assert.IsFalse(result.IsAdmin);
        Assert.IsFalse(result.IsLockedOut);
        Assert.AreEqual(0, result.Workspaces.Count);
    }

    [TestMethod]
    public async Task GetUserByIdAsyncShouldReturnUserWithAdminRole()
    {
        var user = await CreateUserAsync("admin@example.com", true);

        var result = await _service.GetUserByIdAsync(user.Id);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsAdmin);
    }

    [TestMethod]
    public async Task GetUserByIdAsyncShouldReturnLockedOutStatus()
    {
        var user = await CreateUserAsync("locked@example.com", false);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1));

        var result = await _service.GetUserByIdAsync(user.Id);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsLockedOut);
    }

    [TestMethod]
    public async Task GetUserByIdAsyncShouldNotShowExpiredLockout()
    {
        var user = await CreateUserAsync("expired@example.com", false);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(-1));

        var result = await _service.GetUserByIdAsync(user.Id);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsLockedOut);
    }

    [TestMethod]
    public async Task GetUserByIdAsyncShouldReturnWorkspaceAssignments()
    {
        var user = await CreateUserAsync("workspace@example.com", false);
        var workspace1 = await CreateWorkspaceAsync("Workspace 1");
        var workspace2 = await CreateWorkspaceAsync("Workspace 2");

        await CreateWorkspaceAssignmentAsync(user.Id, workspace1.Id, AuthConstants.ViewerRole);
        await CreateWorkspaceAssignmentAsync(user.Id, workspace2.Id, AuthConstants.EditorRole, AuthConstants.LdapSource);

        var result = await _service.GetUserByIdAsync(user.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Workspaces.Count);

        var ws1 = result.Workspaces.First(w => w.WorkspaceName == "Workspace 1");
        Assert.AreEqual(workspace1.Id, ws1.WorkspaceId);
        Assert.AreEqual(AuthConstants.ViewerRole, ws1.Role);
        Assert.AreEqual(AuthConstants.ManualSource, ws1.Source);

        var ws2 = result.Workspaces.First(w => w.WorkspaceName == "Workspace 2");
        Assert.AreEqual(workspace2.Id, ws2.WorkspaceId);
        Assert.AreEqual(AuthConstants.EditorRole, ws2.Role);
        Assert.AreEqual(AuthConstants.LdapSource, ws2.Source);
    }

    [TestMethod]
    public async Task GetAllUsersAsyncShouldReturnEmptyListWhenNoUsers()
    {
        var result = await _service.GetAllUsersAsync();

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task GetAllUsersAsyncShouldReturnAllUsers()
    {
        await CreateUserAsync("user1@example.com", false);
        await CreateUserAsync("user2@example.com", true);
        await CreateUserAsync("user3@example.com", false);

        var result = await _service.GetAllUsersAsync();

        Assert.HasCount(3, result);
    }

    [TestMethod]
    public async Task GetAllUsersAsyncShouldOrderByEmail()
    {
        await CreateUserAsync("charlie@example.com", false);
        await CreateUserAsync("alice@example.com", false);
        await CreateUserAsync("bob@example.com", false);

        var result = await _service.GetAllUsersAsync();

        Assert.HasCount(3, result);
        Assert.AreEqual("alice@example.com", result[0].Email);
        Assert.AreEqual("bob@example.com", result[1].Email);
        Assert.AreEqual("charlie@example.com", result[2].Email);
    }

    [TestMethod]
    public async Task GetAllUsersAsyncShouldIdentifyAdmins()
    {
        await CreateUserAsync("user@example.com", false);
        await CreateUserAsync("admin@example.com", true);

        var result = await _service.GetAllUsersAsync();

        Assert.HasCount(2, result);
        var admin = result.First(u => u.Email == "admin@example.com");
        var user = result.First(u => u.Email == "user@example.com");

        Assert.IsTrue(admin.IsAdmin);
        Assert.IsFalse(user.IsAdmin);
    }

    [TestMethod]
    public async Task GetWorkspacesWithManualAssignmentStatusAsyncShouldReturnAllWorkspacesWhenUserIdIsNull()
    {
        await CreateWorkspaceAsync("Workspace A");
        await CreateWorkspaceAsync("Workspace B");

        var result = await _service.GetWorkspacesWithManualAssignmentStatusAsync(null);

        Assert.HasCount(2, result);
        Assert.IsFalse(result[0].IsAssigned);
        Assert.IsFalse(result[1].IsAssigned);
        Assert.AreEqual(AuthConstants.ViewerRole, result[0].Role);
    }

    [TestMethod]
    public async Task GetWorkspacesWithManualAssignmentStatusAsyncShouldMarkAssignedWorkspaces()
    {
        var user = await CreateUserAsync("user@example.com", false);
        var workspace1 = await CreateWorkspaceAsync("Assigned Workspace");
        var workspace2 = await CreateWorkspaceAsync("Unassigned Workspace");

        await CreateWorkspaceAssignmentAsync(user.Id, workspace1.Id, AuthConstants.EditorRole);

        var result = await _service.GetWorkspacesWithManualAssignmentStatusAsync(user.Id);

        Assert.HasCount(2, result);
        var assigned = result.First(w => w.WorkspaceName == "Assigned Workspace");
        var unassigned = result.First(w => w.WorkspaceName == "Unassigned Workspace");

        Assert.IsTrue(assigned.IsAssigned);
        Assert.AreEqual(AuthConstants.EditorRole, assigned.Role);
        Assert.IsFalse(unassigned.IsAssigned);
        Assert.AreEqual(AuthConstants.ViewerRole, unassigned.Role);
    }

    [TestMethod]
    public async Task GetWorkspacesWithManualAssignmentStatusAsyncShouldOnlyShowManualAssignments()
    {
        var user = await CreateUserAsync("user@example.com", false);
        var workspace1 = await CreateWorkspaceAsync("Manual Workspace");
        var workspace2 = await CreateWorkspaceAsync("OIDC Workspace");

        await CreateWorkspaceAssignmentAsync(user.Id, workspace1.Id, AuthConstants.EditorRole, AuthConstants.ManualSource);
        await CreateWorkspaceAssignmentAsync(user.Id, workspace2.Id, AuthConstants.ViewerRole, AuthConstants.OidcSource);

        var result = await _service.GetWorkspacesWithManualAssignmentStatusAsync(user.Id);

        Assert.HasCount(2, result);
        var manual = result.First(w => w.WorkspaceName == "Manual Workspace");
        var oidc = result.First(w => w.WorkspaceName == "OIDC Workspace");

        Assert.IsTrue(manual.IsAssigned);
        Assert.IsFalse(oidc.IsAssigned);
    }

    [TestMethod]
    public async Task GetWorkspacesWithManualAssignmentStatusAsyncShouldOrderByWorkspaceName()
    {
        await CreateWorkspaceAsync("Zebra");
        await CreateWorkspaceAsync("Alpha");
        await CreateWorkspaceAsync("Mike");

        var result = await _service.GetWorkspacesWithManualAssignmentStatusAsync(null);

        Assert.HasCount(3, result);
        Assert.AreEqual("Alpha", result[0].WorkspaceName);
        Assert.AreEqual("Mike", result[1].WorkspaceName);
        Assert.AreEqual("Zebra", result[2].WorkspaceName);
    }

    private async Task EnsureAdminRoleExistsAsync()
    {
        if (!await _roleManager.RoleExistsAsync(AuthConstants.AdminRole))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(AuthConstants.AdminRole));
        }
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, bool isAdmin)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, "Test-Password-123456789!");
        Assert.IsTrue(result.Succeeded, $"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        if (isAdmin)
        {
            await _userManager.AddToRoleAsync(user, AuthConstants.AdminRole);
        }

        return user;
    }

    private async Task<Workspace> CreateWorkspaceAsync(string name)
    {
        var workspace = new Workspace
        {
            Name = name,
            Description = null,
            IsPublic = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return workspace;
    }

    private async Task CreateWorkspaceAssignmentAsync(Guid userId, Guid workspaceId, string role, string source = AuthConstants.ManualSource)
    {
        var assignment = new UserWorkspace
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = role,
            Source = source,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.UserWorkspaces.Add(assignment);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }
}
