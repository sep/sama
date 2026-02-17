using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Web.Services.Commands;

namespace SAMA.Tests.Integration.Web.Services.Commands;

[TestClass]
public class GroupMappingCommandServiceTests : IntegrationTestBase
{
    private GroupMappingCommandService _service = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _service = new GroupMappingCommandService(DbContext, Substitute.For<ILogger<GroupMappingCommandService>>());
    }

    [TestMethod]
    public async Task CreateMappingShouldCreateWorkspaceMapping()
    {
        var workspace = new Workspace { Name = "Test Workspace" };
        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();

        var id = await _service.CreateMappingAsync(
            workspace.Id, "LDAP", "CN=Developers,DC=example,DC=com", "Editor", "admin");

        Assert.AreNotEqual(Guid.Empty, id);

        var mapping = await DbContext.WorkspaceGroupMappings.FindAsync(id);
        Assert.IsNotNull(mapping);
        Assert.AreEqual(workspace.Id, mapping.WorkspaceId);
        Assert.AreEqual("LDAP", mapping.IdentityProvider);
        Assert.AreEqual("CN=Developers,DC=example,DC=com", mapping.ExternalGroupId);
        Assert.AreEqual("Editor", mapping.Role);
    }

    [TestMethod]
    public async Task CreateMappingShouldCreateGlobalAdminMapping()
    {
        var id = await _service.CreateMappingAsync(
            null, "LDAP", "CN=Admins,DC=example,DC=com", "Admin", "admin");

        var mapping = await DbContext.WorkspaceGroupMappings.FindAsync(id);
        Assert.IsNotNull(mapping);
        Assert.IsNull(mapping.WorkspaceId);
        Assert.AreEqual("Admin", mapping.Role);
    }

    [TestMethod]
    public async Task CreateMappingShouldTrimExternalGroupId()
    {
        var id = await _service.CreateMappingAsync(
            null, "LDAP", "  CN=Admins,DC=example,DC=com  ", "Admin", "admin");

        var mapping = await DbContext.WorkspaceGroupMappings.FindAsync(id);
        Assert.IsNotNull(mapping);
        Assert.AreEqual("CN=Admins,DC=example,DC=com", mapping.ExternalGroupId);
    }

    [TestMethod]
    public async Task DeleteMappingShouldRemoveMapping()
    {
        var id = await _service.CreateMappingAsync(
            null, "LDAP", "CN=Admins,DC=example,DC=com", "Admin", "admin");

        var deleted = await _service.DeleteMappingAsync(id, "admin");

        Assert.IsTrue(deleted);
        Assert.IsNull(await DbContext.WorkspaceGroupMappings.FindAsync(id));
    }

    [TestMethod]
    public async Task DeleteMappingShouldReturnFalseForNonexistentMapping()
    {
        var deleted = await _service.DeleteMappingAsync(Guid.NewGuid(), "admin");

        Assert.IsFalse(deleted);
    }

    [TestMethod]
    public async Task DeleteMappingShouldNotAffectOtherMappings()
    {
        var id1 = await _service.CreateMappingAsync(null, "LDAP", "Group1", "Admin", "admin");
        var id2 = await _service.CreateMappingAsync(null, "LDAP", "Group2", "Admin", "admin");

        await _service.DeleteMappingAsync(id1, "admin");

        var remaining = await DbContext.WorkspaceGroupMappings.ToListAsync();
        Assert.HasCount(1, remaining);
        Assert.AreEqual(id2, remaining[0].Id);
    }
}
