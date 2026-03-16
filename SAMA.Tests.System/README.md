# System Tests

End-to-end tests using **Playwright** to verify complete user workflows through a real browser.

## Skipped by Default

System tests are **skipped by default** to avoid running expensive E2E tests during regular test runs. They will run when:

1. **Debugging**: Attach a debugger (e.g., debug a test in Visual Studio Test Explorer)
2. **Environment variable**: Set `RUN_SYSTEM_TESTS=true`
3. **Run scripts**: Use `./run.sh` or `run.bat` (they set the env var automatically)

## Prerequisites

1. PostgreSQL running (same as development)
2. Support services running (from `SAMA.Web/`): `docker compose up -d` (provides SMTP, OIDC mock provider, LDAP mock)
3. Build the solution first: `dotnet build`
3. Install Playwright browsers (one-time):
   ```powershell
   # Windows
   powershell -ExecutionPolicy Bypass -File bin/Debug/net10.0/playwright.ps1 install chromium

   # Cross-platform
   pwsh bin/Debug/net10.0/playwright.ps1 install chromium
   ```

## Running Tests

```bash
# In the system tests directory
./run.sh

# Or to get verbose output (show all test names)
dotnet test --logger "console;verbosity=detailed"
```

The tests automatically:
- Create an ephemeral database schema per test class for isolation
- Start a separate SAMA.Web instance per test class on random ports
- Configure apps entirely via environment variables
- Clean up all schemas and processes after tests complete

## Parallelization

Tests are designed for parallel execution at the class level:
- Each `[TestClass]` gets its own app instance and database schema
- Tests within a class share the same app (run sequentially within the class)
- Multiple test classes can run in parallel without conflicts

This means test classes are fully isolated from each other, enabling safe parallel execution.

## Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `POSTGRES_HOST` | `localhost` | Database host |
| `POSTGRES_PORT` | `5432` | Database port |
| `POSTGRES_DB` | `samadb` | Database name |
| `POSTGRES_USER` | `sama` | Database user |
| `POSTGRES_PASSWORD` | `sama-dev-pw` | Database password |

## Writing Tests

Inherit from `SystemTestBase`, add `[SystemTestCondition]` to your test class, and use the `Page` property (Playwright IPage):

```csharp
[TestClass]
[SystemTestCondition]
public class MyTests : SystemTestBase
{
    [TestMethod]
    public async Task ShouldDoSomething()
    {
        await Page.GotoAsync($"{BaseUrl}/some-page");
        
        await Page.ClickAsync("button#submit");
        
        Assert.IsTrue(Page.Url.Contains("/success", StringComparison.Ordinal));
    }
}
```
