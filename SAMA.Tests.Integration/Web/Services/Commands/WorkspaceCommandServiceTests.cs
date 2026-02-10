using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Web.Services.Commands;

namespace SAMA.Tests.Integration.Web.Services.Commands;

[TestClass]
public class WorkspaceCommandServiceTests : IntegrationTestBase
{
    private WorkspaceCommandService _service = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _service = new WorkspaceCommandService(DbContext, Substitute.For<ILogger<WorkspaceCommandService>>());
    }

    [TestMethod]
    public async Task CreateWorkspaceAsyncShouldCreateWorkspaceWithBasicProperties()
    {
        var workspaceId = await _service.CreateWorkspaceAsync(
            "Test Workspace",
            "Test Description",
            null,
            false,
            "admin");

        Assert.AreNotEqual(Guid.Empty, workspaceId);

        var workspace = await DbContext.Workspaces.FindAsync(workspaceId);
        Assert.IsNotNull(workspace);
        Assert.AreEqual("Test Workspace", workspace.Name);
        Assert.AreEqual("Test Description", workspace.Description);
        Assert.IsFalse(workspace.IsPublic);
        Assert.AreNotEqual(DateTimeOffset.MinValue, workspace.CreatedAt);
        Assert.AreNotEqual(DateTimeOffset.MinValue, workspace.UpdatedAt);
    }

    [TestMethod]
    public async Task CreateWorkspaceAsyncShouldCreateWorkspaceWithoutDescription()
    {
        var workspaceId = await _service.CreateWorkspaceAsync(
            "Workspace Without Description",
            null,
            null,
            true,
            "admin");

        var workspace = await DbContext.Workspaces.FindAsync(workspaceId);
        Assert.IsNotNull(workspace);
        Assert.AreEqual("Workspace Without Description", workspace.Name);
        Assert.IsNull(workspace.Description);
        Assert.IsTrue(workspace.IsPublic);
    }

    [TestMethod]
    public async Task CreateWorkspaceAsyncShouldCreatePublicWorkspace()
    {
        var workspaceId = await _service.CreateWorkspaceAsync(
            "Public Workspace",
            "Public Description",
            null,
            true,
            "admin");

        var workspace = await DbContext.Workspaces.FindAsync(workspaceId);
        Assert.IsNotNull(workspace);
        Assert.IsTrue(workspace.IsPublic);
    }

    [TestMethod]
    public async Task CreateWorkspaceAsyncShouldCreateWorkspaceWithDashboardMessage()
    {
        var workspaceId = await _service.CreateWorkspaceAsync(
            "Test Workspace",
            "Description",
            "**Welcome** to this workspace!",
            false,
            "admin");

        var workspace = await DbContext.Workspaces.FindAsync(workspaceId);
        Assert.IsNotNull(workspace);
        Assert.AreEqual("**Welcome** to this workspace!", workspace.DashboardMessage);
    }

    [TestMethod]
    public async Task CreateWorkspaceAsyncShouldCreateMultipleWorkspaces()
    {
        await _service.CreateWorkspaceAsync("Workspace 1", null, null, false, "admin");
        await _service.CreateWorkspaceAsync("Workspace 2", null, null, true, "admin");

        var workspaces = await DbContext.Workspaces.OrderBy(w => w.Name).ToListAsync();
        Assert.HasCount(2, workspaces);
        Assert.AreEqual("Workspace 1", workspaces[0].Name);
        Assert.AreEqual("Workspace 2", workspaces[1].Name);
    }

    [TestMethod]
    public async Task UpdateWorkspaceAsyncShouldReturnFalseWhenWorkspaceDoesNotExist()
    {
        var result = await _service.UpdateWorkspaceAsync(
            Guid.NewGuid(),
            "Updated Name",
            "Updated Description",
            null,
            true,
            "admin");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UpdateWorkspaceAsyncShouldUpdateAllProperties()
    {
        var workspace = await CreateWorkspaceAsync("Original Name", "Original Description", false);
        var originalUpdatedAt = workspace.UpdatedAt;

        var result = await _service.UpdateWorkspaceAsync(
            workspace.Id,
            "Updated Name",
            "Updated Description",
            null,
            true,
            "admin");

        Assert.IsTrue(result);

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.Workspaces.FindAsync(workspace.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual("Updated Name", updated.Name);
        Assert.AreEqual("Updated Description", updated.Description);
        Assert.IsTrue(updated.IsPublic);
        Assert.IsTrue(updated.UpdatedAt > originalUpdatedAt);
    }

    [TestMethod]
    public async Task UpdateWorkspaceAsyncShouldClearDescription()
    {
        var workspace = await CreateWorkspaceAsync("Test", "Original Description", false);

        var result = await _service.UpdateWorkspaceAsync(
            workspace.Id,
            "Test",
            null,
            null,
            false,
            "admin");

        Assert.IsTrue(result);

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.Workspaces.FindAsync(workspace.Id);
        Assert.IsNotNull(updated);
        Assert.IsNull(updated.Description);
    }

    [TestMethod]
    public async Task UpdateWorkspaceAsyncShouldToggleIsPublic()
    {
        var workspace = await CreateWorkspaceAsync("Test", null, true);

        var result = await _service.UpdateWorkspaceAsync(
            workspace.Id,
            "Test",
            null,
            null,
            false,
            "admin");

        Assert.IsTrue(result);

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.Workspaces.FindAsync(workspace.Id);
        Assert.IsNotNull(updated);
        Assert.IsFalse(updated.IsPublic);
    }

    [TestMethod]
    public async Task UpdateWorkspaceAsyncShouldSetAndClearDashboardMessage()
    {
        var workspace = await CreateWorkspaceAsync("Test", null, false);

        await _service.UpdateWorkspaceAsync(
            workspace.Id,
            "Test",
            null,
            "# Welcome\nThis is a **test** message.",
            false,
            "admin");

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.Workspaces.FindAsync(workspace.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual("# Welcome\nThis is a **test** message.", updated.DashboardMessage);

        await _service.UpdateWorkspaceAsync(
            workspace.Id,
            "Test",
            null,
            null,
            false,
            "admin");

        DbContext.ChangeTracker.Clear();
        var cleared = await DbContext.Workspaces.FindAsync(workspace.Id);
        Assert.IsNotNull(cleared);
        Assert.IsNull(cleared.DashboardMessage);
    }

    [TestMethod]
    public async Task DeleteWorkspaceAsyncShouldReturnFalseWhenWorkspaceDoesNotExist()
    {
        var result = await _service.DeleteWorkspaceAsync(Guid.NewGuid(), "admin");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task DeleteWorkspaceAsyncShouldDeleteWorkspace()
    {
        var workspace = await CreateWorkspaceAsync("Workspace To Delete", null, false);

        var result = await _service.DeleteWorkspaceAsync(workspace.Id, "admin");

        Assert.IsTrue(result);

        var deleted = await DbContext.Workspaces.FindAsync(workspace.Id);
        Assert.IsNull(deleted);
    }

    [TestMethod]
    public async Task DeleteWorkspaceAsyncShouldDeleteWorkspaceWithRelatedEntities()
    {
        var workspace = await CreateWorkspaceAsync("Test Workspace", null, false);

        var check = new Check
        {
            WorkspaceId = workspace.Id,
            Name = "Test Check",
            CheckType = "Http",
            ConfigurationJson = [],
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Checks.Add(check);

        var channel = new NotificationChannel
        {
            WorkspaceId = workspace.Id,
            Name = "Test Channel",
            ChannelType = "Email",
            ConfigurationJson = [],
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.NotificationChannels.Add(channel);

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await _service.DeleteWorkspaceAsync(workspace.Id, "admin");

        Assert.IsTrue(result);

        var deletedWorkspace = await DbContext.Workspaces.FindAsync(workspace.Id);
        Assert.IsNull(deletedWorkspace);

        var deletedCheck = await DbContext.Checks.FindAsync(check.Id);
        Assert.IsNull(deletedCheck);

        var deletedChannel = await DbContext.NotificationChannels.FindAsync(channel.Id);
        Assert.IsNull(deletedChannel);
    }

    [TestMethod]
    public async Task UpdateWorkspaceAsyncShouldNotAffectOtherWorkspaces()
    {
        var workspace1 = await CreateWorkspaceAsync("Workspace 1", "Description 1", false);
        var workspace2 = await CreateWorkspaceAsync("Workspace 2", "Description 2", true);

        await _service.UpdateWorkspaceAsync(
            workspace1.Id,
            "Updated Workspace 1",
            "Updated Description 1",
            null,
            true,
            "admin");

        DbContext.ChangeTracker.Clear();
        var unchanged = await DbContext.Workspaces.FindAsync(workspace2.Id);
        Assert.IsNotNull(unchanged);
        Assert.AreEqual("Workspace 2", unchanged.Name);
        Assert.AreEqual("Description 2", unchanged.Description);
        Assert.IsTrue(unchanged.IsPublic);
    }

    [TestMethod]
    public async Task DeleteWorkspaceAsyncShouldNotAffectOtherWorkspaces()
    {
        var workspace1 = await CreateWorkspaceAsync("Workspace 1", null, false);
        var workspace2 = await CreateWorkspaceAsync("Workspace 2", null, true);

        await _service.DeleteWorkspaceAsync(workspace1.Id, "admin");

        var remaining = await DbContext.Workspaces.FindAsync(workspace2.Id);
        Assert.IsNotNull(remaining);
        Assert.AreEqual("Workspace 2", remaining.Name);
    }

    [TestMethod]
    public async Task CreateWorkspaceAsyncShouldSetTimestamps()
    {
        var beforeCreate = DateTimeOffset.UtcNow.AddSeconds(-1);

        var workspaceId = await _service.CreateWorkspaceAsync(
            "Test Workspace",
            null,
            null,
            false,
            "admin");

        var workspace = await DbContext.Workspaces.FindAsync(workspaceId);
        Assert.IsNotNull(workspace);
        Assert.IsTrue(workspace.CreatedAt > beforeCreate);
        Assert.IsTrue(workspace.UpdatedAt > beforeCreate);
    }

    [TestMethod]
    public async Task UpdateWorkspaceAsyncShouldUpdateTimestamp()
    {
        var workspace = await CreateWorkspaceAsync("Test", null, false);
        var originalUpdatedAt = workspace.UpdatedAt;

        await Task.Delay(10);

        await _service.UpdateWorkspaceAsync(
            workspace.Id,
            "Updated Test",
            null,
            null,
            false,
            "admin");

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.Workspaces.FindAsync(workspace.Id);
        Assert.IsNotNull(updated);
        Assert.IsTrue(updated.UpdatedAt > originalUpdatedAt);
    }

    private async Task<Workspace> CreateWorkspaceAsync(string name, string? description, bool isPublic)
    {
        var workspace = new Workspace
        {
            Name = name,
            Description = description,
            IsPublic = isPublic,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return workspace;
    }
}
