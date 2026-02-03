<div align="center">
  <img src="sama-logo.svg" alt="SAMA" width="300">
</div>

# SAMA - Service Availability Monitoring and Alerting

A modern, containerizable .NET 10 uptime monitoring and alerting system built with simplicity and reliability in mind.

## Overview

SAMA is a comprehensive service availability monitoring solution that helps you:
- Monitor HTTP/HTTPS endpoints, TCP ports, DNS, TLS certificates, and more
- Receive alerts via Email, Slack, Teams, Discord, and custom scripts
- Track uptime metrics and history
- Visualize response times and service health in real-time

## Features

### Core Monitoring
- **Multiple Check Types**: HTTP/HTTPS, TCP, ICMP Ping, DNS, TLS certificates, Custom Scripts
- **Flexible Scheduling**: Per-check intervals from seconds to hours
- **Traffic Light Status**: Up (healthy), Warn (warning/degraded), Down (failed)
- **Configurable Thresholds**: Require N consecutive failures before alerting

### Alerting
- **Reusable Channels**: Define notification channels once, use across multiple checks
- **Multiple Channels**: Email, Slack, Microsoft Teams, Discord, custom scripts, Azure Event Grid
- **Flexible Alerts**: Trigger on Warn/Down status with consecutive failure thresholds
- **Recovery Notifications**: Optional notifications when services recover
- **Lifecycle Events**: Subscribe to check creation, updates, deletion, and status changes
- **External Integrations**: Send events to Azure Event Grid or custom scripts for workflow automation

### User Interface
- **Real-time Dashboard**: Live status grid with HTMX updates
- **Response Time Charts**: Visualize performance trends with charts
- **Check Management**: Intuitive interface for checks, alerts, channels, and the workspaces that contain them
- **User Management**: Role-based access control at the workspace level
- **Configuration Import/Export**: Export and import workspace configurations with encrypted sensitive data

### Future Enhancements (Phase 2+)
- **Geo-Distributed Agents**: Run checks from multiple regions  
- **Advanced Check Types**: Playwright-based browser automation, Database connectivity checks
- **Enterprise SSO**: OIDC and SAML authentication
- **Audit Logging**: Track all configuration changes

### Security & Compliance
- **Encrypted Configuration**: AES-256 encryption for sensitive data
- **Role-Based Access**: Global Admin role, workspace-scoped Editor/Viewer roles, and Guest access

## Technology Stack

- **.NET 10**: Latest cross-platform ASP.NET Core framework
- **Razor Pages + HTMX**: Simple, server-side rendering with dynamic updates
- **Entity Framework Core**: PostgreSQL database
- **Quartz.NET**: Robust background job scheduling
- **Chart.js**: Beautiful response time visualizations
- **Serilog**: Structured logging

## Quick Start

### Prerequisites

- Docker
- Docker Compose (optional, for easier containerized deployment)

### Docker Deployment

1. **Set the encryption key** (optional)
   
   Create a `.env` file in the project root with a secure encryption key:
   ```powershell
   # Generate a random key (PowerShell)
   $key = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | ForEach-Object {[char]$_})
   "SAMA_ENCRYPTION_KEY=$key" | Out-File -FilePath .env -Encoding utf8
   ```
   
   ```bash
   # Generate a random key (Bash)
   echo "SAMA_ENCRYPTION_KEY=$(openssl rand -base64 32)" > .env
   ```
   
   Or set it manually in the `.env` file:
   ```
   SAMA_ENCRYPTION_KEY=your-secure-key-here
   ```

2. **Run with Docker Compose**

   Use the `docker-compose.yml` file in the repo, which will build SAMA, or make use of GHCR:

```yaml
services:
  db:
    image: postgres:18-trixie
    restart: unless-stopped
    shm_size: 128mb
    environment:
      POSTGRES_DB: sama
      POSTGRES_USER: sama
      POSTGRES_PASSWORD: sama-prod-pw
    volumes:
      - pgroot:/var/lib/postgresql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U sama -d sama"]
      interval: 10s
      timeout: 5s
      retries: 5

  web:
    image: ghcr.io/sep/sama:unstable
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - ConnectionStrings__DefaultConnection=Host=db;Database=sama;Username=sama;Password=sama-prod-pw
      - ASPNETCORE_ENVIRONMENT=Production
      # - Encryption__Key=${SAMA_ENCRYPTION_KEY}  # Optional: Override auto-generated encryption key
    volumes:
      - samakeys:/app/keys
    depends_on:
      db:
        condition: service_healthy

volumes:
  pgroot:
  samakeys:
```

   Then launch it:

   ```bash
   docker-compose up -d
   ```

3. **Configure secure access** (optional, but strongly recommended)

  It is recommended to use a reverse proxy, such as nginx, to expose SAMA to any network.

  However, if desired, SAMA can be configured to use TLS certificates and serve HTTPS traffic directly. The Docker Compose file may be modified to add the following to its existing `environment` and `volumes`:

```yaml
    environment:
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/tls/ssl.pem
      - ASPNETCORE_Kestrel__Certificates__Default__KeyPath=/tls/ssl.key
      - ASPNETCORE_Kestrel__EndpointDefaults__Protocols=Http1AndHttp2
      - ASPNETCORE_URLS=https://*:443
    volumes:
      - ./tls:/tls:ro
```

  The `ssl.pem` certificate and `ssl.key` private key files would need to then reside in a `tls` directory, as mounted. Ownership of the directory and its files should be changed to UID 1654, which is the `app` user in the image.

4. **Complete initial setup**
   
   Navigate to `http://localhost:8080` and follow the setup wizard to create your administrator account.


### Docker Image Variants

SAMA provides two image variants:

| Variant | Tag Examples | Description |
|---------|--------------|-------------|
| Standard | `latest`, `2.0.0`, `unstable` | Default image, runs as non-root user |
| Sudo | `latest-sudo`, `2.0.0-sudo`, `unstable-sudo` | Includes sudo capability for script checks requiring elevated privileges |

#### Using the Sudo Variant

The `-sudo` images have sudo installed but require explicit opt-in at runtime for security:

```yaml
services:
  web:
    image: ghcr.io/sep/sama:latest-sudo
    environment:
      - ENABLE_SUDO=true  # Required to enable sudo at runtime
      # ... other environment variables
```

**Security notes:**
- Without `ENABLE_SUDO=true`, sudo access is not granted at container startup (sudo remains installed but is not configured for use)
- Only use the sudo variant if your script checks require elevated privileges

#### Building Custom Images

To build your own sudo-enabled image:

```bash
docker build -f Dockerfile.sudo -t sama-sudo .
```

## Configuration

### Environment Variables

- **`Encryption__Key`** (or **`SAMA_ENCRYPTION_KEY`** for Docker)

  AES-256 key for encrypting sensitive data. If not provided, SAMA uses ASP.NET Data Protection with an auto-generated key.

  *Optional - auto-generated if not specified*

- **`ConnectionStrings__DefaultConnection`**

  PostgreSQL connection string

  *Default: depends on environment*

- **`ASPNETCORE_ENVIRONMENT`**

  Environment name

  *Default: `Production`*

### Application Settings

Key configuration sections in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=sama;Username=sama;Password=yourpassword"
  }
}
```

The connection string can be overridden using the environment variable described in the previous section.

**Note**: The `Encryption:Key` setting is optional. If not provided, SAMA will auto-generate a persistent encryption key using ASP.NET Data Protection:
- **Docker**: Keys stored in `/app/keys` volume (persisted as `samakeys` volume in docker-compose)
- **Non-Docker**: Keys stored in system data directory (`%ProgramData%\SAMA\keys` on Windows, `/usr/share/SAMA/keys` on Linux)

Additional configuration is managed through the Admin Settings UI.

## Architecture

SAMA follows a simple layered architecture:

```
┌─────────────────────────────────────┐
│    Web Layer (Razor Pages + HTMX)   │
├─────────────────────────────────────┤
│    Service Layer (Business Logic)   │
├─────────────────────────────────────┤
│   Data Layer (EF Core + DbContext)  │
├─────────────────────────────────────┤
│       Database (PostgreSQL)         │
└─────────────────────────────────────┘
```

**Database Migrations**: Automatically applied on application startup. If migrations fail, the application will not start.

**Initial Setup**: On first run, create an administrator account via the setup wizard.

For detailed architecture documentation, see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Project Structure

```
SAMA/
├── SAMA.Web/                      # Main ASP.NET Core application
│   ├── Pages/                     # Razor Pages
│   ├── Services/                  # Business logic services
│   ├── Models/                    # Domain models
│   ├── BackgroundServices/        # Quartz.NET jobs
│   └── Dockerfile
├── SAMA.Data/                     # Data access layer
├── SAMA.Shared/                   # Shared library (checks, alerts)
├── SAMA.Tests.Unit/               # Unit tests
├── SAMA.Tests.Integration/        # Integration tests
├── SAMA.Tests.System/             # System tests
├── docker-compose.yml
└── docs/
    ├── ARCHITECTURE.md            # Architecture documentation
    ├── DATABASE_SCHEMA.md         # Database schema
    └── ROADMAP.md                 # Development roadmap
```

**Note**: Agent-related code (`SAMA.Agent` project, API Controllers) will be added in Phase 2+.

## Development

### Setup

1. **Clone the repository**
   ```powershell
   git clone https://github.com/sep/sama.git
   cd sama
   ```

2. **Start PostgreSQL**
   ```powershell
   docker run -d `
     --name sama-postgres `
     -e POSTGRES_PASSWORD=sama-dev-pw `
     -e POSTGRES_USER=sama `
     -e POSTGRES_DB=samadb `
     -p 5432:5432 `
     postgres:18
   ```

3. **Update connection string** (optional)
   
   Edit `SAMA.Web/appsettings.Development.json` and change the connection string, if needed.

4. **Run the application**
   ```powershell
   cd SAMA.Web
   dotnet run
   ```
   
   **Note**: Database migrations are applied automatically on startup. No manual migration steps required!

5. **Complete initial setup**
   
   Navigate to `http://localhost:5226` and you'll be redirected to the setup page. Create your administrator account:
   - Enter your email address
   - Choose a strong password or passphrase (minimum 14 characters)
   - Confirm your password
   
   **Password Policy**: SAMA encourages passphrases over complex passwords. Use at least 14 characters - consider something memorable like "correct horse battery staple" rather than complex symbols.

   After setup, you can log in and start configuring SAMA.

### Building from Source

```powershell
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test

# Run application (migrations applied automatically)
cd SAMA.Web
dotnet run
```

### Running Tests

```powershell
# Unit tests only
dotnet test SAMA.Tests.Unit/

# Integration tests
dotnet test SAMA.Tests.Integration/

# All tests
dotnet test

# Verbose output (show all test names)
dotnet test --logger "console;verbosity=detailed"
```

**Testing Framework**:
- **MSTest**: Test runner and assertion library
- **NSubstitute**: Mocking framework for creating test doubles with virtual methods
- Tests follow the Arrange-Act-Assert (AAA) pattern
- Unit and some integration tests use mocks to avoid network I/O and external dependencies

### Database Migrations

Migrations are applied automatically on startup, but you can also manage them manually:

```powershell
# Add a new migration
cd SAMA.Web
dotnet ef migrations add MigrationName --project ../SAMA.Data

# View pending migrations
dotnet ef migrations list

# Revert to a specific migration (use with caution)
dotnet ef database update PreviousMigrationName
```

**Production Note**: In production deployments, migrations are applied automatically when the container starts. Ensure your database connection has appropriate permissions to apply schema changes.

## Roadmap

See [docs/ROADMAP.md](docs/ROADMAP.md) for the complete roadmap.

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Coding Standards

- Follow standard C#/.NET conventions
- Write self-documenting code (minimal comments)
- Include appropriate tests for new features
- Update documentation as needed

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

---

[![Powered by SEP logo](https://raw.githubusercontent.com/sep/assets/master/images/powered-by-sep.svg?sanitize=true)](https://www.sep.com)

Built with ❤️ in sunny Indiana
