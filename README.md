# SmartWorkFlowX - Backend API

The robust .NET 10 Web API powering the SmartWorkFlowX enterprise system. It handles role-based authorization, dynamic workflow state machines, automated task routing, and real-time SignalR notifications.

## Live API

The backend is fully deployed and accessible in the cloud at:
**[https://smartworkflowx-backend-dhbdgxeec2fpd6fc.centralindia-01.azurewebsites.net/health](https://smartworkflowx-backend-dhbdgxeec2fpd6fc.centralindia-01.azurewebsites.net/health)**

The frontend client connecting to this API can be found at: [https://smart-work-flow-x-frontend.vercel.app](https://smart-work-flow-x-frontend.vercel.app)

---

## Hosting Architecture

This API is deployed using a completely free, automated cloud infrastructure stack:
- **Backend Hosting:** Microsoft Azure App Service (F1 Free Tier)
- **Database:** Azure SQL Database (Cloud-hosted relational database)
- **CI/CD Pipeline:** Automated via **GitHub Actions**. Every push to the `main` branch automatically triggers a workflow that builds, publishes, and deploys the .NET code to Azure.
- **Database Migrations:** Entity Framework Core migrations are automatically applied on server startup via `dbContext.Database.Migrate()`.
- **Real-time WebSockets:** Azure SignalR handles live dashboard notifications.

---

## Local Development

**Prerequisites:**
- .NET 10 SDK
- Microsoft SQL Server (LocalDB or Docker)

```bash
# 1. Clone the repository
git clone https://github.com/Archit-Chauhan/SmartWorkFlowX_Backend.git

# 2. Enter the directory
cd SmartWorkFlowX_New

# 3. Update the Database Connection String
# Ensure the `DefaultConnection` in `appsettings.Development.json` points to your LocalDB.

# 4. Apply Database Migrations
dotnet ef database update --project SmartWorkFlowX.Infrastructure --startup-project SmartWorkFlowX.Api

# 5. Run the API
dotnet run --project SmartWorkFlowX.Api
```

The API will run locally at `https://localhost:52082`.
