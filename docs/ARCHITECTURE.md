# SAMA Architecture Design

## Overview
SAMA is a service availability monitoring and alerting system built as a containerizable .NET 10 application following KISS principles with a simple layered architecture.

## System Architecture

### Component Overview
```
┌─────────────────────────────────────────────────────────────┐
│                     SAMA Main Application                    │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Web Layer (Razor Pages + HTMX)                        │ │
│  │  - Dashboard, Check Management, Alert Configuration     │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Service Layer                                          │ │
│  │  - Business Logic, Orchestration                        │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Background Services (Quartz.NET)                       │ │
│  │  - Check Scheduler, Built-in Check Executor            │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Agent API (REST)                                       │ │
│  │  - Agent Registration, Check Assignment, Result Collection│
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Data Access Layer (EF Core)                           │ │
│  │  - PostgreSQL Support                                   │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            │
                            ├─── Database (PostgreSQL)
                            │
        ┌───────────────────┴───────────────────┐
        │                                       │
┌───────▼────────┐                    ┌────────▼────────┐
│  SAMA Agent 1  │                    │  SAMA Agent N   │
│  (Region: US)  │                    │  (Region: EU)   │
│                │                    │                 │
│  - Check Exec  │                    │  - Check Exec   │
│  - Advanced    │                    │  - Advanced     │
│    Checks      │                    │    Checks       │
└────────────────┘                    └─────────────────┘
```

## Project Structure

```
SAMA/
├── SAMA.Web/                        # Main ASP.NET Core application
│   ├── Pages/                       # Razor Pages
│   ├── Services/                    # Business logic services
│   ├── Models/                      # Domain models
│   ├── BackgroundServices/          # Quartz.NET jobs
│   ├── Middleware/                  # Custom middleware
│   ├── Program.cs
│   └── Dockerfile                   # Web app Dockerfile
│
├── SAMA.Data/                       # Data access layer
│   ├── Entities/                    # EF Core entities
│   ├── Migrations/                  # Database migrations
│   └── SamaDbContext.cs
│
├── SAMA.Shared/                     # Shared library (referenced by agents)
│   ├── Checks/                      # Check implementations
│   │   ├── HttpCheck.cs
│   │   ├── TcpCheck.cs
│   │   ├── PingCheck.cs
│   │   ├── DnsCheck.cs
│   │   ├── TlsCertificateCheck.cs
│   │   └── ScriptCheck.cs
│   ├── Alerts/                      # Notification channel implementations
│   │   ├── EmailAlert.cs
│   │   ├── SlackAlert.cs
│   │   ├── TeamsAlert.cs
│   │   ├── DiscordAlert.cs
│   │   ├── ScriptAlert.cs
│   │   └── EventGridAlert.cs
│   ├── Models/                      # Shared DTOs and models
│   └── Utilities/                   # Common utilities
│
├── SAMA.Agent/                      # Distributed agent application
│   ├── Services/                    # Agent-specific services
│   ├── Checks/                      # Advanced checks
│   │   ├── PlaywrightCheck.cs
│   │   └── DatabaseCheck.cs
│   ├── Program.cs
│   └── Dockerfile                   # Agent Dockerfile
│
├── SAMA.Tests.Unit/                 # Unit tests (MSTest)
├── SAMA.Tests.Integration/          # Integration tests
├── SAMA.Tests.System/               # System/E2E tests
│
├── docker-compose.yml               # Compose for deployment
├── SAMA.slnx                        # Solution file
│
└── docs/
    ├── ARCHITECTURE.md
    ├── DATABASE_SCHEMA.md
    └── ROADMAP.md
```

## Technology Stack

### Core Technologies
- **.NET 10**: Latest LTS version
- **ASP.NET Core**: Web framework with Razor Pages
- **Entity Framework Core**: ORM with PostgreSQL support
- **Quartz.NET**: Job scheduling
- **Serilog**: Structured logging
- **MSTest**: Unit testing framework
- **NSubstitute**: Mocking framework for test doubles

### Frontend
- **Razor Pages**: Server-side rendering
- **HTMX**: Dynamic UI updates via polling
- **Chart.js**: Response time visualization
- **Bootstrap 5**: Responsive UI framework

### Database
- **PostgreSQL** (development and production)

### Authentication & Authorization
- **ASP.NET Core Identity**: User management with passkey support
- **LDAP/Active Directory**: On-premises directory integration
- **OIDC**: Integration with Azure AD/Entra, Okta, Auth0, etc.

## Key Design Decisions

### 1. Layered Architecture
Simple architecture with clear separation:
- **Presentation & Business Logic** (SAMA.Web): UI, API endpoints, services, and domain logic
- **Data Access** (SAMA.Data): EF Core entities and DbContext
- **Shared** (SAMA.Shared): Cross-cutting concerns and check implementations

### 2. Check Execution Models

#### Built-in Checks (In-Process)
- Executed by main application via Quartz.NET
- Check types: HTTP/HTTPS, TCP, Ping, DNS, TLS Certificate, Script
- Scheduled based on configured intervals
- Runs in background worker threads

#### Agent-Based Checks (Distributed)
- Executed by remote agents
- Check types: All built-in + Playwright, Database connectivity
- Enables geo-distributed monitoring
- Agents poll for assigned checks via REST API

### 3. Agent Communication Protocol

#### Agent Registration
1. Administrator creates agent in SAMA UI
2. System generates random API key
3. Administrator provides API key to agent via configuration
4. Agent authenticates using API key in Authorization header
5. Main app validates API key against registered agents

#### Check Assignment
- Agents poll `/api/agents/{agentId}/checks/assigned` endpoint
- Authorization header: `Bearer {apiKey}`
- Returns list of checks to execute with configuration
- Polling interval: configurable (default 30 seconds)

#### Result Reporting
- Agents POST results to `/api/agents/{agentId}/checks/{checkId}/results`
- Authorization header: `Bearer {apiKey}`
- Payload includes status, response time, error details
- Main app processes results and triggers alerts if needed

### 4. Data Encryption
- All check configuration data encrypted at rest using AES-256
- Symmetric key provided via environment variable: `SAMA_ENCRYPTION_KEY`
- Encryption handled transparently in data layer
- Sensitive fields: passwords, API keys, webhook URLs, connection strings

### 5. Alert Processing

SAMA provides two complementary notification systems:

#### Status Alerts (Alerts Table)
- Per-check alerts with threshold-based triggering
- Notification channels defined per workspace (reusable across checks)
- **Many-to-many relationship**: One alert can notify multiple channels simultaneously
- All channels receive the same alert with identical threshold and trigger conditions
- Alerts trigger based on: status (Warn/Down), consecutive failure count
- Recovery notifications sent when check returns to Up state
- Simple boolean flags and integer thresholds (no complex JSON conditions)
- **Default behavior**: If no channels selected, sends to **all enabled workspace channels**
  - Safe because alerts are already filtered by thresholds (prevents spam)
  - Designed for human notification with fail-safe defaults

**Example**: Create one alert that notifies Email, Slack, and Teams when a check fails 3 consecutive times. All three channels receive notifications with the same conditions.

#### Event Subscriptions (EventSubscriptions Table)
- Workspace-level event routing for operational automation
- Subscribe to lifecycle events: CheckCreated, CheckUpdated, CheckDeleted
- Subscribe to status change events: CheckStatusChanged (Up→Warn, Up→Down, Warn→Down, Down→Warn, Warn→Up, Down→Up)
- Route events to any notification channel (Email, Slack, Teams, Discord, Webhook, Script, EventGrid)
- Enable external workflow integrations via Azure Event Grid or custom webhooks
- Use cases: Change tracking, compliance auditing, CI/CD integration, CMDB synchronization, real-time status streaming, external incident management integration
- **Default behavior**: If no channels selected, events are **not sent anywhere**
  - Intentional to prevent accidental spam (especially for high-volume CheckStatusChanged events)
  - Designed for integration/automation with explicit opt-in

**Unlike Alerts** (which have thresholds and filtering), EventSubscriptions send raw events immediately with no filtering.

**⚠️ CheckStatusChanged Warning**: This event fires on **every status transition** for all checks in a workspace. Can generate hundreds of notifications per hour. Only subscribe integration channels (EventGrid, webhooks, scripts) to this event. For human notifications with thresholds, use Alerts instead.

### 6. Real-time Updates
- Dashboard uses HTMX polling for updates (default: 5 seconds)
- Polls `/api/dashboard/status` for current state
- Partial page updates via HTMX swaps
- Future: SSE support for true push updates

### 7. Database Configuration
- PostgreSQL as the only supported database
- EF Core migrations for PostgreSQL
- **Automatic migration on startup**: Migrations are applied automatically when the application starts
- Application will fail to start if migrations cannot be applied
- Default: PostgreSQL container for development

## Security Architecture

### Authentication
**Local Accounts**:
- ASP.NET Core Identity
- Passphrase-friendly password policy: minimum 14 characters, no complexity requirements
- Account lockout protection
- Future enhancement: Passkey support

**LDAP/Active Directory**:
- Direct bind and search+bind authentication modes
- SSL/TLS and StartTLS support with custom Root CA certificates
- Admin UI for LDAP server configuration and test login
- JIT user provisioning on successful LDAP login
- Account linking: existing local users matched by email are linked to LDAP; local password login is then disabled
- Group membership queries for role/workspace auto-provisioning

**Future**:
- OIDC integration (Azure AD/Entra, Okta, Auth0, etc.)
- SAML2 SSO support may be added based on demand

### Authorization
- Role-based access control (RBAC)
- **Global Role**: Admin (full system access)
  - Can be assigned manually via ASP.NET Identity
  - Can be auto-provisioned via LDAP/OIDC group mappings
- **Workspace Roles**: Editor, Viewer (scoped per workspace via UserWorkspaces)
- **Guest**: Unauthenticated users can view public workspaces
- **External Group Mapping**: Auto-provision Admin or workspace access via LDAP/OIDC group mappings

### API Security
- Agent API: API key-based authentication (Bearer token)
- Future REST API: JWT tokens or API keys

### Data Protection
- Sensitive data encrypted at rest
- HTTPS enforced
- Secrets via environment variables
- Audit logging for all modifications

## Scalability Considerations

### Current Scale Targets
- 50 monitored endpoints
- 25 concurrent checks
- 10 users
- No sub-second check intervals

### Design for Future Scale
- Async/parallel check execution
- Agent-based horizontal scaling for checks
- Database indexing strategy for performance
- Resource prioritization (UI > built-in checks)

### High Availability
- Agents: Multiple instances per region
- Main app: Single instance (HA out of scope for MVP)
- Database: Standard backup/restore procedures

## Deployment Architecture

### Development
- PostgreSQL in Docker container
- Automatic migration application on startup
- Appsettings.Development.json for config

### Production
- Docker containers via Docker Compose
- Environment variables for configuration
- Volume mounts for persistent data
- Health check endpoints for monitoring
- **Automatic database migrations on container startup**
- Application fails fast if migrations cannot be applied

### Container Strategy
- `sama-web`: Main application + built-in checks
- `sama-agent`: Distributed agent
- Images published to GitHub Container Registry (GHCR)
- Multi-stage builds for optimized images
- Dockerfiles located in respective project directories

### Database Migration Strategy
- **Automatic on startup**: No manual intervention required
- **Fail-fast behavior**: Application will not start if migrations fail
- **Deployment simplicity**: Just start the container, migrations handled automatically
- **Rollback**: Use database backups and restore to previous version if needed
- **Zero-downtime migrations**: Not supported in MVP (requires HA setup)

## Observability

### Logging
- Serilog with structured logging
- Log levels: Debug, Information, Warning, Error, Critical
- Console sink for development
- File/persistent sink for production
- Migration status logged on startup

### Health Checks
- ASP.NET Core health check endpoint at `/health`
- Database connectivity check
- Background service status check
- Migration status included in startup logs

## Testing Strategy

### Unit Tests
- **Framework**: MSTest with NSubstitute for mocking
- **Location**: `SAMA.Tests.Unit/`
- **Naming Convention**: `{ClassName}Tests.cs` (e.g., `TcpCheckExecutorTests.cs`)
- **Test Naming**: `{Method}Should{ExpectedBehavior}When{Condition}` (e.g., `ExecuteAsyncShouldReturnDownWhenHostNotConfigured`)
- **Mocking Strategy**: 
  - Use wrapper classes with virtual methods for external dependencies (e.g., `TcpClientWrapper`, `TcpClientFactory`)
  - Mock wrappers using NSubstitute in tests
  - Avoid real network I/O in unit tests
- **Pattern**: Arrange-Act-Assert (AAA) with shared mock setup in `[TestInitialize]`
- **Coverage Target**: Business logic and check executors

### Integration Tests
- **Location**: `SAMA.Tests.Integration/`
- **Strategy**: Real PostgreSQL database with per-test-class schema isolation
- **Scope**: Database interactions, service workflows, entity relationships
- **Execution**: Parallel across test classes, sequential within a class

### System Tests
- **Location**: `SAMA.Tests.System/`
- **Scope**: End-to-end scenarios, UI workflows
- **Future**: Playwright for browser automation

### Test Guidelines
- Keep unit tests fast (< 100ms each)
- Use mocks to eliminate external dependencies
- Test edge cases and error handling
- Follow KISS principle - simple, readable tests

## Future Considerations

### Phase 1 (MVP)
- Monolith with local authentication
- Built-in checks only
- Basic alerting
- Core dashboard
- Automatic migrations on startup
- LDAP/Active Directory authentication
- Group-based workspace provisioning (LDAP)

### Phase 2
- Agent support with advanced checks (Playwright, Database)
- OIDC authentication
- Enhanced alerting rules

### Phase 3
- Advanced reporting
- Alert acknowledgment workflow
- SSE for real-time updates
- SAML SSO (if demand warrants)

### Beyond MVP
- REST API for external integrations
- Alert escalation
- Maintenance windows
- Public status pages
- Multi-tenancy
- Alert suppression/throttling
