# Copilot Instructions for SAMA

## Core Principles

**KISS - Keep It Simple, Stupid**: Choose straightforward solutions over clever ones. Minimize abstractions. Write clear code first.

**Readability and Maintainability First**: Self-documenting code with minimal comments. Short methods with clear flow. Early returns over deep nesting.

**Best Practices Always**: Follow modern .NET conventions and existing patterns. Security first. Async/await properly. Log errors with context. For tests and examples, use IANA-approved example domains.

**Testability**: Write unit tests for all business logic, naming them with the "Should" pattern and no underscores. Use dependency injection for easy mocking. **NEVER mock DbContext** - always use integration tests for database operations.

**Follow Existing Patterns**: Always search for and follow existing patterns in the codebase before creating new approaches. Check existing tests, services, and pages to maintain consistency.

**No Extra Docs**: Don't create new documentation files unless explicitly asked. Code is the documentation.

## Project Structure

```
SAMA.Web/                 # Pages, Services, BackgroundServices, Controllers
SAMA.Data/                # Entities, DbContext, Migrations
SAMA.Shared/              # Check executors, Alert handlers, DTOs
SAMA.Tests.Unit/          # MSTest unit tests
SAMA.Tests.Integration/   # MSTest integration tests
```

**Naming**: Entities are singular. Services end with `Service`. Jobs end with `Job`. Pages match entity names.

## Key Patterns

### Architecture
- Layered monolith: Web → Services → Data → Database
- Razor Pages with HTMX (no heavy client-side frameworks)
- Register services in DI

### Database
- Migrations auto-apply on startup
- Always use `async`/`await`
- Use `.AsNoTracking()` for read-only queries
- Use `.Include()` to avoid N+1 queries

### Security
- Encrypt sensitive data with `SensitiveDataMaskingService`
- Use `[Authorize]` with roles for admins; use `[RequireWorkspaceEditAccess]` or `[RequireWorkspaceViewAccess]` for workspace-level access
- Validate input with data annotations
- EF Core handles SQL injection prevention

### Background Jobs
```csharp
[DisallowConcurrentExecution]
public class MyJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MyJob> _logger;
    
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SamaDbContext>();
        // Job logic
    }
}
```

### HTMX Updates
```html
<div id="status" hx-get="/api/status" hx-trigger="every 5s"></div>
```

### Lucide Icons
- **ALWAYS verify icon names exist** in `SAMA.Web/wwwroot/lib/lucide-static/font/lucide.css` before using them - do not guess or assume icon names
- Always add an appropriate size class to Lucide icons (e.g., `icon-xs`, `icon-md`, `icon-lg`)

## What NOT to Do

- ❌ Don't create documentation files unless explicitly asked
- ❌ Don't add new architectural patterns
- ❌ Don't add unnecessary abstractions or interfaces
- ❌ Don't use client-side JS frameworks
- ❌ Don't write verbose comments
- ❌ Don't add dependencies without strong justification
- ❌ Don't deviate from established patterns unless necessary

## Workflow

**Making Changes**: Read existing code first → Follow existing patterns → Run tests

**Adding Features**: Check ROADMAP.md → Study similar code → Entities → Migration → Service → Pages → Tests → Update README only if major

**Running tests**: Run `dotnet test <path>` (e.g., `dotnet test SAMA.Tests.Unit/`) or `dotnet test` to run all tests. Use `--filter "<filter>"` to filter tests. Add `--logger "console;verbosity=detailed"` to see all test names.

## Quick Reference

- **Tests**: MSTest with Arrange-Act-Assert (without extraneous comments)
- **Unit Tests**: For business logic without database dependencies
- **Integration Tests**: For anything involving DbContext - NEVER mock the database, NEVER use InMemory database provider, use existing test base classes and patterns
- **Connection string**: `appsettings.Development.json`
- **Encryption key**: Optional environment variable `Encryption__Key` (development) or `SAMA_ENCRYPTION_KEY` (Docker Compose). If not provided, auto-generated via ASP.NET Data Protection.
- **Ports**: HTTPS 5226 (dev), HTTP 8080 (Docker)
- **Dependencies**: Prefer .NET BCL, use established packages only

## Questions?

See `docs/ARCHITECTURE.md`, `docs/DATABASE_SCHEMA.md`, `docs/ROADMAP.md`, `docs/DESIGN_SYSTEM.md`, or `README.md`.

---

**Simple, readable code beats clever code. Follow established patterns. When in doubt, choose the straightforward solution.**
