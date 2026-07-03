using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Web.Constants;
using SAMA.Web.Services.Commands;

namespace SAMA.Web.Pages.Dev;

[Authorize(Roles = AuthConstants.AdminRole)]
public class LoadTestModel(
    SamaDbContext dbContext,
    CheckCommandService checkCommandService,
    IWebHostEnvironment env,
    ILogger<LoadTestModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IActionResult OnGet()
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var workspaceName = string.IsNullOrWhiteSpace(Input.WorkspaceNamePrefix)
            ? $"LoadTest-{DateTimeOffset.UtcNow:yyyy-MM-dd}-{Guid.CreateVersion7():N}"
            : $"{Input.WorkspaceNamePrefix}-{DateTimeOffset.UtcNow:yyyy-MM-dd}-{Guid.CreateVersion7():N}";

        var workspace = new Workspace
        {
            Name = workspaceName,
            Description = "Load test workspace",
            IsPublic = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Workspaces.Add(workspace);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Created load test workspace {WorkspaceId} ({WorkspaceName})", workspace.Id, workspace.Name);

        var checkTypes = new[] { CheckTypes.Http, CheckTypes.Ping, CheckTypes.Tcp };
        var random = new Random();
        var checks = new List<Check>();
        var totalResults = 0;

        for (var i = 0; i < Input.CheckCount; i++)
        {
            var checkType = checkTypes[random.Next(checkTypes.Length)];
            var schedule = random.Next(2) == 0 ? "60" : "300";
            var enabled = random.Next(10) > 1; // mostly enabled

            var configuration = checkType switch
            {
                CheckTypes.Http => new Dictionary<string, JsonElement>
                {
                    [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("http://localhost:8080/health"),
                    [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
                    [ConfigurationKeys.HttpCheck.AllowInvalidSsl] = JsonSerializer.SerializeToElement(true)
                },
                CheckTypes.Ping => new Dictionary<string, JsonElement>
                {
                    [ConfigurationKeys.PingCheck.Host] = JsonSerializer.SerializeToElement("127.0.0.1"),
                    [ConfigurationKeys.PingCheck.PacketCount] = JsonSerializer.SerializeToElement(2),
                    [ConfigurationKeys.PingCheck.PacketLossThresholdPercent] = JsonSerializer.SerializeToElement(50)
                },
                CheckTypes.Tcp => new Dictionary<string, JsonElement>
                {
                    [ConfigurationKeys.TcpCheck.Host] = JsonSerializer.SerializeToElement("localhost"),
                    [ConfigurationKeys.TcpCheck.Port] = JsonSerializer.SerializeToElement(5432)
                },
                _ => new Dictionary<string, JsonElement>()
            };

            var check = new Check
            {
                WorkspaceId = workspace.Id,
                Name = $"{checkType}-{i + 1}",
                Description = "Load test check",
                CheckType = checkType,
                ConfigurationJson = configuration,
                Schedule = schedule,
                TimeoutSeconds = 30,
                Enabled = enabled,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            checks.Add(check);
        }

        foreach (var check in checks)
        {
            var checkId = await checkCommandService.CreateCheckAsync(
                check.WorkspaceId,
                check.Name,
                check.Description,
                check.CheckType,
                check.Schedule,
                check.TimeoutSeconds,
                check.ConfigurationJson,
                check.Enabled,
                userId);

            check.Id = checkId;
        }

        var now = DateTimeOffset.UtcNow;
        var historyStart = now.AddDays(-Input.DaysOfHistory);

        foreach (var check in checks)
        {
            var intervalSeconds = check.Schedule == "300" ? 300 : 60;
            var current = new DateTimeOffset(historyStart.UtcDateTime, TimeSpan.Zero);
            var lastStatus = CheckStatuses.Up;
            var lastResponseTime = random.Next(20, 500);
            var lastCheckedAt = current;

            while (current <= now)
            {
                var status = lastStatus;
                var responseTime = lastResponseTime;

                if (random.NextDouble() < 0.05)
                {
                    status = CheckStatuses.Down;
                    responseTime = random.Next(1000, 5000);
                }
                else if (random.NextDouble() < 0.1)
                {
                    status = CheckStatuses.Warn;
                    responseTime = random.Next(500, 2000);
                }
                else
                {
                    status = CheckStatuses.Up;
                    responseTime = random.Next(20, 500);
                }

                if (status == CheckStatuses.Down && random.NextDouble() < 0.6)
                {
                    status = CheckStatuses.Warn;
                    responseTime = random.Next(500, 1500);
                }

                var (statusCode, errorMessage) = status switch
                {
                    CheckStatuses.Up => (200, null),
                    CheckStatuses.Warn => (200, null),
                    CheckStatuses.Down => (0, "Connection failed"),
                    _ => (0, null)
                };

                await dbContext.CheckResults.AddAsync(new CheckResult
                {
                    CheckId = check.Id,
                    Status = status,
                    ResponseTimeMs = responseTime,
                    StatusCode = statusCode,
                    ErrorMessage = errorMessage,
                    CheckedAt = current
                });
                totalResults++;

                lastStatus = status;
                lastResponseTime = responseTime;
                lastCheckedAt = current;
                current = current.AddSeconds(intervalSeconds);
            }

            var trackedCheck = await dbContext.Checks.FindAsync(check.Id);
            if (trackedCheck is not null)
            {
                trackedCheck.LatestStatus = lastStatus;
                trackedCheck.LatestCheckedAt = lastCheckedAt;
                trackedCheck.LatestResponseTimeMs = lastResponseTime;
            }

            await dbContext.SaveChangesAsync();
        }

        TempData["SuccessMessage"] = $"Created workspace '{workspace.Name}' with {checks.Count} checks and {totalResults} historical results.";
        return RedirectToPage("/Dashboard/Index", new { workspaceId = workspace.Id });
    }

    public class InputModel
    {
        [Range(1, 1000, ErrorMessage = "Check count must be between 1 and 1000")]
        public int CheckCount { get; set; } = 100;

        [Range(1, 365, ErrorMessage = "Days of history must be between 1 and 365")]
        public int DaysOfHistory { get; set; } = 2;

        [StringLength(MaxPrefixLength, ErrorMessage = "Workspace name prefix must be {1} characters or fewer.")]
        public string? WorkspaceNamePrefix { get; set; } = "LoadTest";

        // Workspace.Name is capped at 100 chars; reserve room for "-yyyy-MM-dd-" (12) + a 32-char GUID suffix.
        public const int MaxPrefixLength = 100 - 12 - 32;
    }
}
