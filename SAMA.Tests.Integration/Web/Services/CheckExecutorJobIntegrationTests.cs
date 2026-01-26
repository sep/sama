using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using SAMA.Data.Entities;
using SAMA.Shared.Checks;
using SAMA.Shared.Constants;
using SAMA.Shared.Models;
using SAMA.Web.Services;

namespace SAMA.Tests.Integration.Web.Services;

[TestClass]
public class CheckExecutorJobIntegrationTests : IntegrationTestBase
{
    private static ICheckExecutor _sharedMockExecutor = null!;

    private IScheduler _mockScheduler = null!;
    private IJobExecutionContext _mockContext = null!;
    private CheckExecutorJob _job = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Create shared mock executor before services are built
        _sharedMockExecutor = Substitute.For<ICheckExecutor>();
        services.AddKeyedScoped<ICheckExecutor>(CheckTypes.Http, (_, _) => _sharedMockExecutor);

        // Register mock alert handler
        var mockAlertHandler = Substitute.For<AlertHandlerService>(null, null, null, null);
        services.AddScoped(_ => mockAlertHandler);
    }

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _mockScheduler = Substitute.For<IScheduler>();
        _mockContext = Substitute.For<IJobExecutionContext>();
        _mockContext.CancellationToken.Returns(CancellationToken.None);
        _mockContext.Scheduler.Returns(_mockScheduler);

        _job = new CheckExecutorJob(ServiceProvider, Substitute.For<ILogger<CheckExecutorJob>>());
    }

    [TestMethod]
    public async Task ExecuteShouldRemoveJobWhenCheckNotFound()
    {
        var nonExistentCheckId = Guid.NewGuid();
        var jobKey = new JobKey($"check-{nonExistentCheckId:N}", "checks");
        var jobDetail = CreateJobDetail(nonExistentCheckId, jobKey);

        _mockContext.JobDetail.Returns(jobDetail);

        await _job.Execute(_mockContext);

        await _mockScheduler.Received(1).DeleteJob(jobKey, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteShouldRemoveJobWhenCheckIsDisabled()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id, enabled: false);

        var jobKey = new JobKey($"check-{check.Id:N}", "checks");
        var jobDetail = CreateJobDetail(check.Id, jobKey);

        _mockContext.JobDetail.Returns(jobDetail);

        await _job.Execute(_mockContext);

        await _mockScheduler.Received(1).DeleteJob(jobKey, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteShouldNotRemoveJobWhenCheckExistsAndEnabled()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id, enabled: true);

        var jobKey = new JobKey($"check-{check.Id:N}", "checks");
        var jobDetail = CreateJobDetail(check.Id, jobKey);

        _mockContext.JobDetail.Returns(jobDetail);
        _sharedMockExecutor.ExecuteAsync(Arg.Any<Dictionary<string, JsonElement>>(), Arg.Any<CancellationToken>())
            .Returns(new CheckExecutionResult
            {
                Status = CheckStatuses.Up,
                ResponseTimeMs = 100,
                CheckedAt = DateTimeOffset.UtcNow
            });

        await _job.Execute(_mockContext);

        await _mockScheduler.DidNotReceive().DeleteJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteShouldSaveCheckResultWhenCheckExecutesSuccessfully()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id, enabled: true);

        var jobKey = new JobKey($"check-{check.Id:N}", "checks");
        var jobDetail = CreateJobDetail(check.Id, jobKey);

        _mockContext.JobDetail.Returns(jobDetail);
        _sharedMockExecutor.ExecuteAsync(Arg.Any<Dictionary<string, JsonElement>>(), Arg.Any<CancellationToken>())
            .Returns(new CheckExecutionResult
            {
                Status = CheckStatuses.Up,
                ResponseTimeMs = 150,
                CheckedAt = DateTimeOffset.UtcNow
            });

        await _job.Execute(_mockContext);

        var results = DbContext.CheckResults.Where(r => r.CheckId == check.Id).ToList();
        Assert.HasCount(1, results);
        Assert.AreEqual(CheckStatuses.Up, results[0].Status);
        Assert.AreEqual(150, results[0].ResponseTimeMs);
    }

    private static IJobDetail CreateJobDetail(Guid checkId, JobKey jobKey)
    {
        var jobDetail = Substitute.For<IJobDetail>();
        var jobDataMap = new JobDataMap { { "CheckId", checkId } };
        jobDetail.Key.Returns(jobKey);
        jobDetail.JobDataMap.Returns(jobDataMap);
        return jobDetail;
    }

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var workspace = new Workspace
        {
            Name = $"Test Workspace {Guid.NewGuid()}"
        };

        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return workspace;
    }

    private async Task<Check> CreateCheckAsync(Guid workspaceId, bool enabled)
    {
        var check = new Check
        {
            WorkspaceId = workspaceId,
            Name = $"Test Check {Guid.NewGuid()}",
            CheckType = CheckTypes.Http,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                ["Url"] = JsonSerializer.SerializeToElement("https://example.com")
            },
            IntervalSeconds = 60,
            TimeoutSeconds = 30,
            Enabled = enabled
        };

        DbContext.Checks.Add(check);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return check;
    }
}
