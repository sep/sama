using SAMA.Data.Entities;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Integration.Web.Services.Queries;

[TestClass]
public class GroupMappingQueryServiceTests : IntegrationTestBase
{
    private GroupMappingQueryService _service = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _service = new GroupMappingQueryService(DbContext);
    }

    [TestMethod]
    public async Task GetAllMappingsShouldReturnEmptyWhenNoMappings()
    {
        var result = await _service.GetAllMappingsAsync();

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task GetAllMappingsShouldReturnMappingsWithWorkspaceNames()
    {
        var workspace = new Workspace { Name = "Production" };
        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();

        DbContext.WorkspaceGroupMappings.Add(new WorkspaceGroupMapping
        {
            WorkspaceId = workspace.Id,
            IdentityProvider = "LDAP",
            ExternalGroupId = "DevOps",
            Role = "Editor",
        });
        DbContext.WorkspaceGroupMappings.Add(new WorkspaceGroupMapping
        {
            WorkspaceId = null,
            IdentityProvider = "LDAP",
            ExternalGroupId = "SysAdmins",
            Role = "Admin",
        });
        await DbContext.SaveChangesAsync();

        var result = await _service.GetAllMappingsAsync();

        Assert.HasCount(2, result);

        var workspaceMapping = result.First(m => m.WorkspaceId != null);
        Assert.AreEqual("Production", workspaceMapping.WorkspaceName);
        Assert.AreEqual("DevOps", workspaceMapping.ExternalGroupId);
        Assert.AreEqual("Editor", workspaceMapping.Role);

        var globalMapping = result.First(m => m.WorkspaceId == null);
        Assert.IsNull(globalMapping.WorkspaceName);
        Assert.AreEqual("SysAdmins", globalMapping.ExternalGroupId);
        Assert.AreEqual("Admin", globalMapping.Role);
    }

    [TestMethod]
    public async Task MappingExistsShouldReturnTrueForExistingMapping()
    {
        var workspace = new Workspace { Name = "Test" };
        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();

        DbContext.WorkspaceGroupMappings.Add(new WorkspaceGroupMapping
        {
            WorkspaceId = workspace.Id,
            IdentityProvider = "LDAP",
            ExternalGroupId = "Developers",
            Role = "Editor",
        });
        await DbContext.SaveChangesAsync();

        var exists = await _service.MappingExistsAsync(workspace.Id, "LDAP", "Developers");

        Assert.IsTrue(exists);
    }

    [TestMethod]
    public async Task MappingExistsShouldReturnFalseForDifferentWorkspace()
    {
        var workspace1 = new Workspace { Name = "Workspace 1" };
        var workspace2 = new Workspace { Name = "Workspace 2" };
        DbContext.Workspaces.AddRange(workspace1, workspace2);
        await DbContext.SaveChangesAsync();

        DbContext.WorkspaceGroupMappings.Add(new WorkspaceGroupMapping
        {
            WorkspaceId = workspace1.Id,
            IdentityProvider = "LDAP",
            ExternalGroupId = "Developers",
            Role = "Editor",
        });
        await DbContext.SaveChangesAsync();

        var exists = await _service.MappingExistsAsync(workspace2.Id, "LDAP", "Developers");

        Assert.IsFalse(exists);
    }

    [TestMethod]
    public async Task MappingExistsShouldTrimExternalGroupId()
    {
        DbContext.WorkspaceGroupMappings.Add(new WorkspaceGroupMapping
        {
            WorkspaceId = null,
            IdentityProvider = "LDAP",
            ExternalGroupId = "Admins",
            Role = "Admin",
        });
        await DbContext.SaveChangesAsync();

        var exists = await _service.MappingExistsAsync(null, "LDAP", "  Admins  ");

        Assert.IsTrue(exists);
    }

    [TestMethod]
    public async Task MappingExistsShouldReturnFalseForNonexistentMapping()
    {
        var exists = await _service.MappingExistsAsync(null, "LDAP", "NonexistentGroup");

        Assert.IsFalse(exists);
    }
}
