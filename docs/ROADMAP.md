# SAMA Development Roadmap

## MVP Phases

### Phase 1: Core Monolith (MVP)

**Goal**: Functional monitoring system with local authentication and built-in checks only.

#### Deliverables
- [x] Project structure and solution setup
- [x] Database schema implementation (EF Core)
  - [x] Entity models (12 entities)
  - [x] PostgreSQL migrations
  - [x] Encryption infrastructure
- [x] Authentication & Authorization
  - [x] ASP.NET Core Identity setup
  - [x] Role-based access control
  - [x] User management UI
- [x] Core Domain Logic
  - [x] Workspace management
  - [x] Check configuration models
  - [x] Alert configuration models
  - [x] Check execution engine
  - [x] Alert processing engine
  - [x] Service layer implementations
- [x] Built-in Check Implementations (SAMA.Shared)
  - [x] HTTP/HTTPS check
  - [x] TCP port check
  - [x] ICMP ping check
  - [x] DNS resolution check
  - [x] SSL certificate check
  - [x] Script execution check
- [x] Notification Channel Implementations (SAMA.Shared)
  - [x] Email (SMTP)
  - [x] Slack webhook
  - [x] Microsoft Teams webhook
  - [x] Discord webhook
  - [x] Script invocation
  - [x] Azure Event Grid (bonus)
- [x] Background Services
  - [x] Quartz.NET setup
  - [x] Check scheduler job
  - [x] Check executor job
  - [x] Alert processor job
  - [x] Data retention cleanup job
- [x] Web UI (Razor Pages + HTMX)
  - [x] Dashboard (status grid, active alerts)
  - [x] Workspace management pages
  - [x] Notification channel management pages
  - [x] Check CRUD pages
  - [x] Alert CRUD pages
  - [x] Historical data views
  - [x] Response time charts (Chart.js)
  - [x] User management pages
  - [x] Event subscription pages (bonus)
- [x] Observability
  - [x] Serilog structured logging
  - [x] Health check endpoint
- [x] Configuration
  - [x] appsettings.json structure
  - [x] Environment variable support
  - [x] Data encryption setup
- [x] Testing
  - [x] Unit tests (MSTest) - core logic
  - [x] Integration tests - database operations
- [x] Documentation
  - [x] README with setup instructions

---

### Phase 2: Distributed Agents & Enterprise Authentication

**Goal**: Add agent support for geo-distributed monitoring, advanced checks, and enterprise SSO.

Note, these are all subject to change.

#### Deliverables
- [ ] Agent Infrastructure
  - [ ] Agent registration in database
  - [ ] API key generation and management
  - [ ] Agent management UI
  - [ ] Agent health monitoring
  - [ ] Heartbeat mechanism
- [ ] Agent API (REST)
  - [ ] Check assignment endpoint
  - [ ] Result submission endpoint
  - [ ] Bulk result submission
  - [ ] Heartbeat endpoint
  - [ ] Configuration endpoint
  - [ ] API key authentication middleware
- [ ] SAMA.Agent Application
  - [ ] Agent console application
  - [ ] Check polling service
  - [ ] Result reporter service
  - [ ] API key-based authentication
  - [ ] Configuration management
- [ ] Advanced Check Types
  - [ ] Playwright-based web navigation
  - [ ] Database connectivity checks
- [ ] Check Routing Logic
  - [ ] Assign checks to specific agents/regions
  - [ ] Fallback to local execution
  - [ ] Multi-region support
- [ ] Agent Monitoring
  - [ ] Agent health dashboard
  - [ ] LastSeenAt tracking
  - [ ] Alert on agent disconnection
- [ ] LDAP/Active Directory Integration
  - [ ] LDAP authentication handler
  - [ ] LDAP server configuration UI
  - [ ] Group membership queries
  - [ ] Testing with Active Directory and OpenLDAP
- [ ] OIDC Integration
  - [ ] OIDC authentication handler
  - [ ] OIDC provider configuration UI
  - [ ] User claim mapping
  - [ ] Testing with common providers (Azure AD/Entra, Okta, Auth0)
- [ ] Enhanced Authorization
  - [ ] WorkspaceGroupMappings table and UI
  - [ ] External group-to-workspace role mapping
  - [ ] Just-in-time user provisioning on login
  - [ ] Auto-sync workspace access based on IdP groups
- [ ] Account Management
  - [ ] Local vs external account handling
  - [ ] Account linking
  - [ ] Profile management
- [ ] Docker Support
  - [ ] Dockerfile for main app
  - [ ] Dockerfile for agent
  - [ ] Docker Compose configuration
  - [ ] GHCR publishing workflow
- [ ] Testing
  - [ ] System tests - end-to-end scenarios (SAMA.Tests.System)
  - [ ] Agent API integration tests
  - [ ] Agent application tests
  - [ ] Multi-region scenario tests
  - [ ] LDAP/AD authentication tests
  - [ ] OIDC flow tests
  - [ ] Multi-provider tests
- [ ] Documentation
  - [ ] Agent deployment guide
  - [ ] Agent API documentation
  - [ ] API key setup guide
  - [ ] LDAP/AD setup guide
  - [ ] OIDC setup guide
  - [ ] Common IdP configurations

---

## Future Enhancements (Post-MVP)

Note, these are all subject to change.

### High Priority
- [ ] **Maintenance Windows**: Pause checks during scheduled maintenance
- [ ] **Alert Suppression**: Throttle repeated alerts for flapping services
- [ ] **REST API**: Full REST API for external integrations
- [ ] **Mobile PWA**: Progressive Web App for mobile devices

### Medium Priority
- [ ] **Advanced Reporting**: Scheduled reports via email
- [ ] **Export Capabilities**: CSV/JSON export of historical data
- [ ] **Check Dependencies**: Don't alert if dependent check is down
- [ ] **Custom Dashboards**: User-configurable dashboard views
- [ ] **Grafana Integration**: Export metrics to Grafana
- [ ] **Incident Management**: Enhanced incident tracking and notes
- [ ] **DNS-over-HTTPS/TLS**: Add support for custom servers, including DoH and DoT in DNS checks, maybe using the `DnsClientX` package

### Low Priority
- [ ] **Alert Acknowledgment**: Workflow for acknowledging and resolving alerts
- [ ] **Multi-Tenancy**: Support multiple isolated tenants
- [ ] **API Versioning**: Versioned REST API
- [ ] **Webhook Receivers**: Trigger checks from external events
- [x] **Configuration Import/Export**: Export/import workspaces with checks, channels, and alerts
- [ ] **Advanced Analytics**: ML-based anomaly detection
- [ ] **Mobile Apps**: Native iOS/Android apps
- [ ] **Internationalization**: Multi-language UI support
