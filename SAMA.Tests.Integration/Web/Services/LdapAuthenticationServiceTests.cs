using System.Net.Security;
using System.Security.Authentication;
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
public class LdapAuthenticationServiceTests : IntegrationTestBase
{
    private LdapAuthenticationService _service = null!;
    private UserManager<ApplicationUser> _userManager = null!;
    private RoleManager<IdentityRole<Guid>> _roleManager = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _userManager = ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _roleManager = ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        await EnsureAdminRoleExistsAsync();

        var globalSettings = Substitute.For<GlobalSettingsService>(null!, null!, null!, null!);
        _service = new LdapAuthenticationService(globalSettings, ServiceProvider, Substitute.For<ILogger<LdapAuthenticationService>>());
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldCreateNewUser()
    {
        var ldapResult = LdapLoginResult.Success(
            "CN=Test User,DC=example,DC=com",
            "testuser@example.com",
            "Test User",
            []);

        var user = await _service.ProvisionOrUpdateUserAsync(ldapResult);

        Assert.IsNotNull(user);
        Assert.AreEqual("testuser@example.com", user.Email);
        Assert.IsTrue(user.EmailConfirmed);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldAddLdapLoginToExistingUser()
    {
        var existingUser = await CreateUserAsync("existing@example.com");

        var ldapResult = LdapLoginResult.Success(
            "CN=Existing User,DC=example,DC=com",
            "existing@example.com",
            "Existing User",
            []);

        var user = await _service.ProvisionOrUpdateUserAsync(ldapResult);

        Assert.AreEqual(existingUser.Id, user.Id);

        var logins = await _userManager.GetLoginsAsync(user);
        Assert.IsTrue(logins.Any(l => l.LoginProvider == AuthConstants.LdapSource));
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldSkipGroupMappingsWhenNoneDefined()
    {
        var ldapResult = LdapLoginResult.Success(
            "CN=Test User,DC=example,DC=com",
            "nogroups@example.com",
            "Test User",
            ["CN=Developers,OU=Groups,DC=example,DC=com"]);

        var user = await _service.ProvisionOrUpdateUserAsync(ldapResult);

        var assignments = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id)
            .ToListAsync();

        Assert.HasCount(0, assignments);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldGrantAdminRoleFromGroupMapping()
    {
        await CreateGroupMappingAsync(null, "LDAP", "Admins", AuthConstants.AdminRole);

        var ldapResult = LdapLoginResult.Success(
            "CN=Admin User,DC=example,DC=com",
            "admin@example.com",
            "Admin User",
            ["CN=Admins,OU=Groups,DC=example,DC=com"]);

        var user = await _service.ProvisionOrUpdateUserAsync(ldapResult);

        var isAdmin = await _userManager.IsInRoleAsync(user, AuthConstants.AdminRole);
        Assert.IsTrue(isAdmin);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldRevokeAdminRoleWhenGroupNoLongerMatches()
    {
        // First login with admin group to establish the role
        await CreateGroupMappingAsync(null, "LDAP", "Admins", AuthConstants.AdminRole);

        var firstLogin = LdapLoginResult.Success(
            "CN=Admin User,DC=example,DC=com",
            "admin@example.com",
            "Admin User",
            ["CN=Admins,OU=Groups,DC=example,DC=com"]);
        await _service.ProvisionOrUpdateUserAsync(firstLogin);
        DbContext.ChangeTracker.Clear();

        // Second login without admin group should revoke
        var secondLogin = LdapLoginResult.Success(
            "CN=Admin User,DC=example,DC=com",
            "admin@example.com",
            "Admin User",
            ["CN=Developers,OU=Groups,DC=example,DC=com"]);
        await _service.ProvisionOrUpdateUserAsync(secondLogin);

        DbContext.ChangeTracker.Clear();
        var refreshedUser = await _userManager.FindByEmailAsync("admin@example.com");
        var isAdmin = await _userManager.IsInRoleAsync(refreshedUser!, AuthConstants.AdminRole);
        Assert.IsFalse(isAdmin);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldAssignWorkspacesFromGroupMapping()
    {
        var workspace = await CreateWorkspaceAsync("Dev Workspace");
        await CreateGroupMappingAsync(workspace.Id, "LDAP", "Developers", AuthConstants.EditorRole);

        var ldapResult = LdapLoginResult.Success(
            "CN=Dev User,DC=example,DC=com",
            "dev@example.com",
            "Dev User",
            ["CN=Developers,OU=Groups,DC=example,DC=com"]);

        var user = await _service.ProvisionOrUpdateUserAsync(ldapResult);

        var assignments = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id && uw.Source == AuthConstants.LdapSource)
            .ToListAsync();

        Assert.HasCount(1, assignments);
        Assert.AreEqual(workspace.Id, assignments[0].WorkspaceId);
        Assert.AreEqual(AuthConstants.EditorRole, assignments[0].Role);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldMatchByFullDn()
    {
        var workspace = await CreateWorkspaceAsync("DN Workspace");
        await CreateGroupMappingAsync(workspace.Id, "LDAP", "CN=Developers,OU=Groups,DC=example,DC=com", AuthConstants.ViewerRole);

        var ldapResult = LdapLoginResult.Success(
            "CN=User,DC=example,DC=com",
            "dn@example.com",
            "DN User",
            ["CN=Developers,OU=Groups,DC=example,DC=com"]);

        var user = await _service.ProvisionOrUpdateUserAsync(ldapResult);

        var assignments = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id && uw.Source == AuthConstants.LdapSource)
            .ToListAsync();

        Assert.HasCount(1, assignments);
        Assert.AreEqual(workspace.Id, assignments[0].WorkspaceId);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldNotDuplicateAssignmentsOnRepeatedLogin()
    {
        var workspace = await CreateWorkspaceAsync("Stable Workspace");
        await CreateGroupMappingAsync(workspace.Id, "LDAP", "Developers", AuthConstants.EditorRole);

        var ldapResult = LdapLoginResult.Success(
            "CN=User,DC=example,DC=com",
            "stable@example.com",
            "Stable User",
            ["CN=Developers,OU=Groups,DC=example,DC=com"]);

        await _service.ProvisionOrUpdateUserAsync(ldapResult);
        DbContext.ChangeTracker.Clear();

        await _service.ProvisionOrUpdateUserAsync(ldapResult);
        DbContext.ChangeTracker.Clear();

        var user = await _userManager.FindByEmailAsync("stable@example.com");
        var assignments = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user!.Id && uw.Source == AuthConstants.LdapSource)
            .ToListAsync();

        Assert.HasCount(1, assignments);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldRemoveStaleAssignments()
    {
        var workspace1 = await CreateWorkspaceAsync("Keep Workspace");
        var workspace2 = await CreateWorkspaceAsync("Remove Workspace");
        await CreateGroupMappingAsync(workspace1.Id, "LDAP", "TeamA", AuthConstants.EditorRole);
        await CreateGroupMappingAsync(workspace2.Id, "LDAP", "TeamB", AuthConstants.ViewerRole);

        var firstLogin = LdapLoginResult.Success(
            "CN=User,DC=example,DC=com",
            "changing@example.com",
            "User",
            ["CN=TeamA,OU=Groups,DC=example,DC=com", "CN=TeamB,OU=Groups,DC=example,DC=com"]);

        var user = await _service.ProvisionOrUpdateUserAsync(firstLogin);
        DbContext.ChangeTracker.Clear();

        var secondLogin = LdapLoginResult.Success(
            "CN=User,DC=example,DC=com",
            "changing@example.com",
            "User",
            ["CN=TeamA,OU=Groups,DC=example,DC=com"]);

        await _service.ProvisionOrUpdateUserAsync(secondLogin);
        DbContext.ChangeTracker.Clear();

        var assignments = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id && uw.Source == AuthConstants.LdapSource)
            .ToListAsync();

        Assert.HasCount(1, assignments);
        Assert.AreEqual(workspace1.Id, assignments[0].WorkspaceId);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldNotAffectManualAssignments()
    {
        var workspace = await CreateWorkspaceAsync("Manual Workspace");
        var user = await CreateUserAsync("mixed@example.com");
        await CreateWorkspaceAssignmentAsync(user.Id, workspace.Id, AuthConstants.EditorRole, AuthConstants.ManualSource);

        var ldapResult = LdapLoginResult.Success(
            "CN=User,DC=example,DC=com",
            "mixed@example.com",
            "Mixed User",
            []);

        await _service.ProvisionOrUpdateUserAsync(ldapResult);
        DbContext.ChangeTracker.Clear();

        var assignments = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id)
            .ToListAsync();

        Assert.HasCount(1, assignments);
        Assert.AreEqual(AuthConstants.ManualSource, assignments[0].Source);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldHandleMultipleWorkspaceMappings()
    {
        var workspace1 = await CreateWorkspaceAsync("Workspace A");
        var workspace2 = await CreateWorkspaceAsync("Workspace B");
        var workspace3 = await CreateWorkspaceAsync("Workspace C");
        await CreateGroupMappingAsync(workspace1.Id, "LDAP", "AllStaff", AuthConstants.ViewerRole);
        await CreateGroupMappingAsync(workspace2.Id, "LDAP", "AllStaff", AuthConstants.ViewerRole);
        await CreateGroupMappingAsync(workspace3.Id, "LDAP", "Developers", AuthConstants.EditorRole);

        var ldapResult = LdapLoginResult.Success(
            "CN=User,DC=example,DC=com",
            "multi@example.com",
            "Multi User",
            ["CN=AllStaff,OU=Groups,DC=example,DC=com", "CN=Developers,OU=Groups,DC=example,DC=com"]);

        var user = await _service.ProvisionOrUpdateUserAsync(ldapResult);

        var assignments = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id && uw.Source == AuthConstants.LdapSource)
            .OrderBy(uw => uw.WorkspaceId)
            .ToListAsync();

        Assert.HasCount(3, assignments);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldIgnoreNonLdapMappings()
    {
        var workspace = await CreateWorkspaceAsync("OIDC Workspace");
        await CreateGroupMappingAsync(workspace.Id, "Generic-OIDC", "Developers", AuthConstants.EditorRole);

        var ldapResult = LdapLoginResult.Success(
            "CN=User,DC=example,DC=com",
            "oidcignore@example.com",
            "User",
            ["CN=Developers,OU=Groups,DC=example,DC=com"]);

        var user = await _service.ProvisionOrUpdateUserAsync(ldapResult);

        var assignments = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id)
            .ToListAsync();

        Assert.HasCount(0, assignments);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldMatchGroupsCaseInsensitively()
    {
        var workspace = await CreateWorkspaceAsync("Case Workspace");
        await CreateGroupMappingAsync(workspace.Id, "LDAP", "developers", AuthConstants.EditorRole);

        var ldapResult = LdapLoginResult.Success(
            "CN=User,DC=example,DC=com",
            "casetest@example.com",
            "User",
            ["CN=Developers,OU=Groups,DC=example,DC=com"]);

        var user = await _service.ProvisionOrUpdateUserAsync(ldapResult);

        var assignments = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id && uw.Source == AuthConstants.LdapSource)
            .ToListAsync();

        Assert.HasCount(1, assignments);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldKeepAdminRoleOnRepeatedLogin()
    {
        await CreateGroupMappingAsync(null, "LDAP", "Admins", AuthConstants.AdminRole);

        var ldapResult = LdapLoginResult.Success(
            "CN=Admin,DC=example,DC=com",
            "persistadmin@example.com",
            "Admin User",
            ["CN=Admins,OU=Groups,DC=example,DC=com"]);

        await _service.ProvisionOrUpdateUserAsync(ldapResult);
        DbContext.ChangeTracker.Clear();

        var user = await _service.ProvisionOrUpdateUserAsync(ldapResult);

        var isAdmin = await _userManager.IsInRoleAsync(user, AuthConstants.AdminRole);
        Assert.IsTrue(isAdmin);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldRevokeAdminWhenItWasTheOnlyMapping()
    {
        await CreateGroupMappingAsync(null, "LDAP", "Admins", AuthConstants.AdminRole);

        var firstLogin = LdapLoginResult.Success(
            "CN=Solo Admin,DC=example,DC=com",
            "soloadmin@example.com",
            "Solo Admin",
            ["CN=Admins,OU=Groups,DC=example,DC=com"]);
        var user = await _service.ProvisionOrUpdateUserAsync(firstLogin);
        DbContext.ChangeTracker.Clear();

        var isAdminBefore = await _userManager.IsInRoleAsync(user, AuthConstants.AdminRole);
        Assert.IsTrue(isAdminBefore);

        var secondLogin = LdapLoginResult.Success(
            "CN=Solo Admin,DC=example,DC=com",
            "soloadmin@example.com",
            "Solo Admin",
            []);
        await _service.ProvisionOrUpdateUserAsync(secondLogin);
        DbContext.ChangeTracker.Clear();

        var refreshedUser = await _userManager.FindByEmailAsync("soloadmin@example.com");
        var isAdminAfter = await _userManager.IsInRoleAsync(refreshedUser!, AuthConstants.AdminRole);
        Assert.IsFalse(isAdminAfter);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldRevokeAdminWhenGroupMappingIsDeleted()
    {
        var mapping = new WorkspaceGroupMapping
        {
            WorkspaceId = null,
            IdentityProvider = "LDAP",
            ExternalGroupId = "Admins",
            Role = AuthConstants.AdminRole,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        DbContext.WorkspaceGroupMappings.Add(mapping);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var firstLogin = LdapLoginResult.Success(
            "CN=Admin,DC=example,DC=com",
            "deletedmapping@example.com",
            "Admin",
            ["CN=Admins,OU=Groups,DC=example,DC=com"]);
        var user = await _service.ProvisionOrUpdateUserAsync(firstLogin);
        DbContext.ChangeTracker.Clear();

        var isAdminBefore = await _userManager.IsInRoleAsync(user, AuthConstants.AdminRole);
        Assert.IsTrue(isAdminBefore);

        var storedMapping = await DbContext.WorkspaceGroupMappings.FindAsync(mapping.Id);
        DbContext.WorkspaceGroupMappings.Remove(storedMapping!);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var secondLogin = LdapLoginResult.Success(
            "CN=Admin,DC=example,DC=com",
            "deletedmapping@example.com",
            "Admin",
            ["CN=Admins,OU=Groups,DC=example,DC=com"]);
        await _service.ProvisionOrUpdateUserAsync(secondLogin);
        DbContext.ChangeTracker.Clear();

        var refreshedUser = await _userManager.FindByEmailAsync("deletedmapping@example.com");
        var isAdminAfter = await _userManager.IsInRoleAsync(refreshedUser!, AuthConstants.AdminRole);
        Assert.IsFalse(isAdminAfter);
    }

    [TestMethod]
    public async Task ProvisionOrUpdateUserShouldRemoveWorkspaceAssignmentWhenGroupMappingIsDeleted()
    {
        var workspace = await CreateWorkspaceAsync("Mapped Workspace");
        var mapping = new WorkspaceGroupMapping
        {
            WorkspaceId = workspace.Id,
            IdentityProvider = "LDAP",
            ExternalGroupId = "Developers",
            Role = AuthConstants.EditorRole,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        DbContext.WorkspaceGroupMappings.Add(mapping);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var firstLogin = LdapLoginResult.Success(
            "CN=Dev User,DC=example,DC=com",
            "deletedwsmap@example.com",
            "Dev User",
            ["CN=Developers,OU=Groups,DC=example,DC=com"]);
        var user = await _service.ProvisionOrUpdateUserAsync(firstLogin);
        DbContext.ChangeTracker.Clear();

        var assignmentsBefore = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id && uw.Source == AuthConstants.LdapSource)
            .ToListAsync();
        Assert.HasCount(1, assignmentsBefore);

        var storedMapping = await DbContext.WorkspaceGroupMappings.FindAsync(mapping.Id);
        DbContext.WorkspaceGroupMappings.Remove(storedMapping!);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var secondLogin = LdapLoginResult.Success(
            "CN=Dev User,DC=example,DC=com",
            "deletedwsmap@example.com",
            "Dev User",
            ["CN=Developers,OU=Groups,DC=example,DC=com"]);
        await _service.ProvisionOrUpdateUserAsync(secondLogin);
        DbContext.ChangeTracker.Clear();

        var assignmentsAfter = await DbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id && uw.Source == AuthConstants.LdapSource)
            .ToListAsync();
        Assert.HasCount(0, assignmentsAfter);
    }

    [TestMethod]
    public void ValidateWithCustomCaShouldReturnTrueWhenNoErrors()
    {
        var result = LdapAuthenticationService.ValidateWithCustomCa(
            null, null, SslPolicyErrors.None, "not-used");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ValidateWithCustomCaShouldThrowForNameMismatch()
    {
        var ex = Assert.Throws<AuthenticationException>(() =>
            LdapAuthenticationService.ValidateWithCustomCa(
                null, null, SslPolicyErrors.RemoteCertificateNameMismatch, "not-used"));

        StringAssert.Contains(ex.Message, "RemoteCertificateNameMismatch");
    }

    [TestMethod]
    public void ValidateWithCustomCaShouldThrowForNameMismatchWithChainErrors()
    {
        var ex = Assert.Throws<AuthenticationException>(() =>
            LdapAuthenticationService.ValidateWithCustomCa(
                null,
                null,
                SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors,
                "not-used"));

        StringAssert.Contains(ex.Message, "RemoteCertificateNameMismatch");
    }

    [TestMethod]
    public void ValidateWithCustomCaShouldThrowForNotAvailable()
    {
        var ex = Assert.Throws<AuthenticationException>(() =>
            LdapAuthenticationService.ValidateWithCustomCa(
                null, null, SslPolicyErrors.RemoteCertificateNotAvailable, "not-used"));

        StringAssert.Contains(ex.Message, "RemoteCertificateNotAvailable");
    }

    [TestMethod]
    public void ValidateWithCustomCaShouldThrowWhenCertIsNull()
    {
        var ex = Assert.Throws<AuthenticationException>(() =>
            LdapAuthenticationService.ValidateWithCustomCa(
                null, null, SslPolicyErrors.RemoteCertificateChainErrors, "not-used"));

        StringAssert.Contains(ex.Message, "certificate or chain is null");
    }

    [TestMethod]
    public void ExtractCnFromDnShouldParseCnFromFullDn()
    {
        var result = LdapAuthenticationService.ExtractCnFromDn("CN=Developers,OU=Groups,DC=example,DC=com");

        Assert.AreEqual("Developers", result);
    }

    [TestMethod]
    public void ExtractCnFromDnShouldReturnNullForNonCnDn()
    {
        var result = LdapAuthenticationService.ExtractCnFromDn("OU=Groups,DC=example,DC=com");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ExtractCnFromDnShouldReturnNullForNullOrWhitespace()
    {
        Assert.IsNull(LdapAuthenticationService.ExtractCnFromDn(""));
        Assert.IsNull(LdapAuthenticationService.ExtractCnFromDn("  "));
    }

    [TestMethod]
    public void ExtractCnFromDnShouldHandleCnOnly()
    {
        var result = LdapAuthenticationService.ExtractCnFromDn("CN=Admins");

        Assert.AreEqual("Admins", result);
    }

    [TestMethod]
    public void ResolveBindDnShouldApplyTemplateForPlainUsername()
    {
        var result = LdapAuthenticationService.ResolveBindDn("jdoe", "DOMAIN\\{0}");

        Assert.AreEqual("DOMAIN\\jdoe", result);
    }

    [TestMethod]
    public void ResolveBindDnShouldUseEmailDirectlyWhenInputContainsAt()
    {
        var result = LdapAuthenticationService.ResolveBindDn("jdoe@example.com", "DOMAIN\\{0}");

        Assert.AreEqual("jdoe@example.com", result);
    }

    [TestMethod]
    public void ResolveBindDnShouldEscapeSpecialCharsInUsername()
    {
        var result = LdapAuthenticationService.ResolveBindDn("j(doe)", "uid={0},ou=users,dc=example,dc=com");

        Assert.AreEqual("uid=j\\28doe\\29,ou=users,dc=example,dc=com", result);
    }

    [TestMethod]
    public void ResolveBindDnShouldNotEscapeEmailInput()
    {
        var result = LdapAuthenticationService.ResolveBindDn("j(doe)@example.com", "DOMAIN\\{0}");

        Assert.AreEqual("j(doe)@example.com", result);
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

    private async Task CreateWorkspaceAssignmentAsync(Guid userId, Guid workspaceId, string role, string source)
    {
        DbContext.UserWorkspaces.Add(new UserWorkspace
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = role,
            Source = source,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }
}
