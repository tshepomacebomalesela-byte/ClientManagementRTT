# Client Management

A small C# client directory with a clear four-project architecture:

- `ClientManagement.Web`: MVC presentation tier. It calls the service over HTTP only.
- `ClientManagement.Service`: authenticated web-service tier for client CRUD and export.
- `ClientManagement.Data`: SQLite ADO.NET repository. No stored procedures, LINQ to SQL, or Entity Framework.
- `ClientManagement.Domain`: shared client, address, contact, and paging models.

## Run locally

Start the service in one terminal:

```powershell
dotnet run --project src/ClientManagement.Service --urls http://localhost:5080
```

Start the web application in another:

```powershell
dotnet run --project src/ClientManagement.Web
```

Open the URL printed by the web project. The default local account is `admin` / `admin123`. Change the credentials in `src/ClientManagement.Service/appsettings.json` before deploying. The SQLite database is created as `client-management.db` in the service working directory.

The directory supports paged search, client details, multiple typed addresses and contacts, deletion, and CSV export. Export includes one row per client/address and excludes contact numbers by design.