# SmartWorkFlowX — Backend API

A production-grade .NET 10 Web API implementing a multi-role enterprise workflow management system. The API handles JWT and Google OAuth authentication, dynamic multi-step approval workflows, automated task routing, real-time SignalR notifications, AI-assisted description generation, and a full audit trail.

**Live API health check:** https://smartworkflowx-backend-dhbdgxeec2fpd6fc.centralindia-01.azurewebsites.net/health  
**Frontend:** https://smart-work-flow-x-frontend.vercel.app

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 (ASP.NET Core Web API) |
| Architecture | Clean Architecture — Domain / Application / Infrastructure / API |
| Database | Azure SQL Database via Entity Framework Core 10 |
| Messaging | Azure Service Bus (task and notification events) |
| Real-time | Azure SignalR Service |
| Authentication | JWT Bearer + Google OAuth 2.0 |
| CAPTCHA | Cloudflare Turnstile (forgot-password endpoint) |
| AI | Groq API — llama-3.1-8b-instant |
| Email | SMTP via MailKit |
| Testing | xUnit + Moq |
| CI/CD | GitHub Actions — build, test, deploy to Azure App Service |

---

## Architecture

```
SmartWorkFlowX.Domain          — entities, repository interfaces, no dependencies
SmartWorkFlowX.Application     — services, DTOs, business logic
SmartWorkFlowX.Infrastructure  — EF Core, Azure Service Bus, Groq, SMTP, BCrypt
SmartWorkFlowX.Api             — controllers, middleware, DI composition root, workers
SmartWorkFlowX.Tests           — xUnit unit tests (87 test cases)
```

EF Core global query filters enforce soft-delete (`IsDeleted`) across all entities automatically. Azure Service Bus workers run as background `IHostedService` instances, processing workflow events and bulk notification broadcasts asynchronously.

---

## Core Features

**Authentication**
- Login with email and BCrypt-hashed password; returns a signed JWT
- Google OAuth 2.0 — registered users receive a JWT, unregistered users are redirected with an error
- Forgot password — sends a time-limited reset link via email (Turnstile-protected)
- Password reset — validates token expiry and updates BCrypt hash

**Role-based Access Control**

Four roles enforced via `[Authorize(Roles = "...")]` on every controller:

| Role | Permissions |
|---|---|
| Admin | Full access — user management, all reports, workflow and task management |
| Manager | Workflow management, task assignment, reports |
| Employee | View and act on own assigned tasks only |
| Auditor | Read-only access to reports and audit logs |

**User Management (Admin only)**
- Paginated user list with search; CSV export
- Create user with role assignment and auto-generated welcome email
- Soft-delete (deactivate) and restore users
- Duplicate email and invalid input validation

**Workflow Management (Manager/Admin)**
- Create multi-step approval workflows; each step has a role, name, on-reject action (GoBack or Cancel), and optional escalation hours
- Activate, deactivate, and clone workflows
- Steps stored as ordered `WorkflowStep` entities; EF Core handles cascade

**Task Management**
- Assign tasks to active workflows with priority, category, and due date
- Approval chain: each step routes the task to the correct role; approve advances, reject triggers GoBack or Cancel
- Full `TaskStepHistory` written on every action with actor, timestamp, and optional comment
- Employees see only their own tasks; Managers see all tasks with filters

**Reports and Audit**
- System analytics: total users, workflow counts, task status distribution, average completion time
- Paginated audit log (Admin/Auditor) — every significant action is logged
- Overdue task report
- All report data served from a dedicated `IReportRepository`

**Real-time Notifications**
- Per-user notifications via Azure SignalR on task assignment and approval events
- Unread count badge updated in real time
- Bulk broadcast to all users or a specific role (queued via Service Bus)

---

## AI Integration — Groq (llama-3.1-8b-instant)

**Endpoint:** `POST /api/Task/formalize-description`  
**Authorization:** Manager or Admin JWT required

The `GroqService` sends a structured prompt to the Groq API using the `llama-3.1-8b-instant` model. The prompt includes a `context` field (`"task"` or `"workflow"`) so the model produces output appropriate to each entity type. The model is instructed to return a concise, professional 2–3 sentence description.

**Used in the frontend for:**
- Auto-generating task descriptions in the Task Assignment form
- Auto-generating workflow descriptions in the Workflow Builder

The raw text the user types is sent to this endpoint; the response replaces the description field with a formalized version.

**Configuration** (`appsettings.json`):
```json
"Groq": {
  "ApiKey": "<your-groq-api-key>",
  "Model": "llama-3.1-8b-instant"
}
```

---

## Local Development

**Prerequisites**
- .NET 10 SDK
- SQL Server (LocalDB, Docker, or a full instance)
- Optional: Azure Service Bus namespace (notifications degrade gracefully without it)

```bash
# 1. Clone
git clone https://github.com/Archit-Chauhan/SmartWorkFlowX_Backend.git
cd SmartWorkFlowX_Backend

# 2. Set connection string
# Edit SmartWorkFlowX.Api/appsettings.Development.json
# Set "DefaultConnection" to your SQL Server instance

# 3. Apply EF Core migrations
dotnet ef database update --project SmartWorkFlowX.Infrastructure --startup-project SmartWorkFlowX.Api

# 4. Run
dotnet run --project SmartWorkFlowX.Api
```

The API starts at `https://localhost:52082`. Swagger UI is available at `/swagger`.

---

## Running Tests

The test project covers 87 test cases across 7 modules (Authentication, User Management, Workflow, Task, Reports, Notifications, Security). Tests use xUnit and Moq against mocked repositories — no database connection required.

```bash
# Run all tests with standard output
dotnet test SmartWorkFlowX.Tests/SmartWorkFlowX.Tests.csproj --verbosity normal

# Run all tests and generate a TRX results file (used by GitHub Actions)
dotnet test SmartWorkFlowX.Tests/SmartWorkFlowX.Tests.csproj \
  --verbosity normal \
  --logger "trx;LogFileName=test-results.trx"

# Run tests for a specific module (filter by TC-ID prefix)
dotnet test SmartWorkFlowX.Tests/SmartWorkFlowX.Tests.csproj \
  --filter "DisplayName~TC-A"   # Authentication
  # TC-U = User, TC-W = Workflow, TC-T = Task
  # TC-R = Reports, TC-N = Notifications, TC-S = Security

# Run a single test by exact display name
dotnet test SmartWorkFlowX.Tests/SmartWorkFlowX.Tests.csproj \
  --filter "DisplayName=TC-A01: Login with valid credentials"
```

Each test case carries its TC-ID in `[Fact(DisplayName = "TC-XXX: ...")]` so test names are visible in the GitHub Actions run log.

---

## CI/CD Pipeline

Every push to `main` triggers the GitHub Actions workflow at `.github/workflows/main_smartworkflowx-backend.yml`:

1. Restore NuGet packages
2. Build in Release configuration
3. Run all 87 tests — pipeline fails if any test fails
4. Publish and deploy to Azure App Service (Central India region)

Test results are uploaded as a TRX artifact on each run.

---

## Environment Variables

| Key | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Jwt__Key` | HS256 signing key (minimum 32 characters) |
| `Jwt__Issuer` | JWT issuer claim |
| `Jwt__Audience` | JWT audience claim |
| `Groq__ApiKey` | Groq API key for AI description generation |
| `Google__ClientId` | Google OAuth client ID |
| `Google__ClientSecret` | Google OAuth client secret |
| `AzureServiceBus__ConnectionString` | Service Bus namespace connection string |
| `AzureSignalR__ConnectionString` | SignalR Service connection string |
| `Smtp__Host` / `Smtp__User` / `Smtp__Pass` | SMTP credentials for email delivery |
| `Cloudflare__TurnstileSecretKey` | Turnstile secret for forgot-password validation |
