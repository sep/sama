using SAMA.Data.Entities;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Integration.Web.Services.Queries;

[TestClass]
public class WorkspaceQueryServiceTests : IntegrationTestBase
{
    private WorkspaceQueryService _service = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _service = new WorkspaceQueryService(DbContext);
    }

    [TestMethod]
    public async Task GetWorkspaceByIdAsyncShouldReturnWorkspaceWhenExists()
    {
        var workspace = await CreateWorkspaceAsync("Test Workspace", "Test Description", false);

        var result = await _service.GetWorkspaceByIdAsync(workspace.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(workspace.Id, result.Id);
        Assert.AreEqual("Test Workspace", result.Name);
        Assert.AreEqual("Test Description", result.Description);
        Assert.IsFalse(result.IsPublic);
    }

    [TestMethod]
    public async Task GetWorkspaceByIdAsyncShouldReturnNullWhenWorkspaceDoesNotExist()
    {
        var result = await _service.GetWorkspaceByIdAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetWorkspaceByIdAsyncShouldNotTrackEntity()
    {
        var workspace = await CreateWorkspaceAsync("Untracked Workspace", null, false);

        var result = await _service.GetWorkspaceByIdAsync(workspace.Id);

        Assert.IsNotNull(result);

        var entry = DbContext.Entry(result);
        Assert.AreEqual(Microsoft.EntityFrameworkCore.EntityState.Detached, entry.State);
    }

    [TestMethod]
    public async Task GetWorkspaceByIdAsyncShouldReturnCorrectWorkspaceWhenMultipleExist()
    {
        var workspace1 = await CreateWorkspaceAsync("Workspace 1", "Description 1", false);
        var workspace2 = await CreateWorkspaceAsync("Workspace 2", "Description 2", true);
        var workspace3 = await CreateWorkspaceAsync("Workspace 3", "Description 3", false);

        var result = await _service.GetWorkspaceByIdAsync(workspace2.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(workspace2.Id, result.Id);
        Assert.AreEqual("Workspace 2", result.Name);
        Assert.AreEqual("Description 2", result.Description);
        Assert.IsTrue(result.IsPublic);
    }

    [TestMethod]
    public async Task GetWorkspacesAsyncShouldReturnEmptyListWhenNoWorkspaces()
    {
        var result = await _service.GetWorkspacesAsync();

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetWorkspacesAsyncShouldReturnAllWorkspacesOrderedByName()
    {
        await CreateWorkspaceAsync("Zebra Workspace", null, false);
        await CreateWorkspaceAsync("Alpha Workspace", null, true);
        await CreateWorkspaceAsync("Beta Workspace", "Description", false);

        var result = await _service.GetWorkspacesAsync();

        Assert.HasCount(3, result);
        Assert.AreEqual("Alpha Workspace", result[0].Name);
        Assert.AreEqual("Beta Workspace", result[1].Name);
        Assert.AreEqual("Zebra Workspace", result[2].Name);
    }

    [TestMethod]
    public async Task GetWorkspacesAsyncShouldIncludeAllPropertiesAndCounts()
    {
        var workspace = await CreateWorkspaceAsync("Test Workspace", "Test Description", true);
        await CreateCheckAsync(workspace.Id, "Check 1");
        await CreateCheckAsync(workspace.Id, "Check 2");
        await CreateNotificationChannelAsync(workspace.Id, "Channel 1");

        var result = await _service.GetWorkspacesAsync();

        var item = result.Single();
        Assert.AreEqual(workspace.Id, item.Id);
        Assert.AreEqual("Test Workspace", item.Name);
        Assert.AreEqual("Test Description", item.Description);
        Assert.IsTrue(item.IsPublic);
        Assert.AreNotEqual(DateTimeOffset.MinValue, item.CreatedAt);
        Assert.AreNotEqual(DateTimeOffset.MinValue, item.UpdatedAt);
        Assert.AreEqual(2, item.CheckCount);
        Assert.AreEqual(1, item.NotificationChannelCount);
        Assert.AreEqual(0, item.UserCount);
    }

    [TestMethod]
    public async Task GetWorkspaceDetailsAsyncShouldReturnNullWhenWorkspaceDoesNotExist()
    {
        var result = await _service.GetWorkspaceDetailsAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetWorkspaceDetailsAsyncShouldReturnDetailsWithAllProperties()
    {
        var workspace = await CreateWorkspaceAsync("Test Workspace", "Test Description", true);

        var result = await _service.GetWorkspaceDetailsAsync(workspace.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(workspace.Id, result.Id);
        Assert.AreEqual("Test Workspace", result.Name);
        Assert.AreEqual("Test Description", result.Description);
        Assert.IsTrue(result.IsPublic);
        Assert.AreNotEqual(DateTimeOffset.MinValue, result.CreatedAt);
        Assert.AreNotEqual(DateTimeOffset.MinValue, result.UpdatedAt);
        Assert.AreEqual(0, result.CheckCount);
        Assert.AreEqual(0, result.NotificationChannelCount);
        Assert.AreEqual(0, result.UserCount);
    }

    [TestMethod]
    public async Task GetWorkspaceDetailsAsyncShouldCountRelatedEntities()
    {
        var workspace = await CreateWorkspaceAsync("Test Workspace", null, false);
        await CreateCheckAsync(workspace.Id, "Check 1");
        await CreateCheckAsync(workspace.Id, "Check 2");
        await CreateCheckAsync(workspace.Id, "Check 3");
        await CreateNotificationChannelAsync(workspace.Id, "Channel 1");
        await CreateNotificationChannelAsync(workspace.Id, "Channel 2");

        var result = await _service.GetWorkspaceDetailsAsync(workspace.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.CheckCount);
        Assert.AreEqual(2, result.NotificationChannelCount);
        Assert.AreEqual(0, result.UserCount);
    }

    [TestMethod]
    public async Task GetWorkspacesAsyncShouldReturnEmptyListWhenEmptyFilterProvided()
    {
        await CreateWorkspaceAsync("Workspace 1", null, false);
        await CreateWorkspaceAsync("Workspace 2", null, true);

        var result = await _service.GetWorkspacesAsync([]);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetWorkspacesAsyncShouldFilterByWorkspaceIds()
    {
        var workspace1 = await CreateWorkspaceAsync("Workspace 1", null, false);
        var workspace2 = await CreateWorkspaceAsync("Workspace 2", null, true);
        var workspace3 = await CreateWorkspaceAsync("Workspace 3", null, false);

        var result = await _service.GetWorkspacesAsync([workspace1.Id, workspace3.Id]);

        Assert.HasCount(2, result);
        Assert.IsTrue(result.Any(w => w.Id == workspace1.Id));
        Assert.IsTrue(result.Any(w => w.Id == workspace3.Id));
        Assert.IsFalse(result.Any(w => w.Id == workspace2.Id));
    }

    // ...existing helper methods...
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

    private async Task<Check> CreateCheckAsync(Guid workspaceId, string name)
    {
        var check = new Check
        {
            WorkspaceId = workspaceId,
            Name = name,
            CheckType = "Http",
            ConfigurationJson = [],
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.Checks.Add(check);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return check;
    }

    private async Task<NotificationChannel> CreateNotificationChannelAsync(Guid workspaceId, string name)
    {
        var channel = new NotificationChannel
        {
            WorkspaceId = workspaceId,
            Name = name,
            ChannelType = "Email",
            ConfigurationJson = [],
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.NotificationChannels.Add(channel);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return channel;
    }
}
