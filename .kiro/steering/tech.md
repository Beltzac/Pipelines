# Technology Stack

## Framework & Runtime
- **.NET 9.0** - Primary framework (Windows-targeted with `net9.0-windows7.0`)
- **C# 12** with nullable reference types enabled
- **Blazor Server** - Web UI framework with interactive server components
- **Photino.NET** - Desktop application wrapper
- **ASP.NET Core** - Web hosting and services

## Database & ORM
- **Entity Framework Core 9.0** - Primary ORM with SQLite provider
- **SQLite** - Local database storage
- **Oracle.EntityFrameworkCore** - Oracle database integration
- **MongoDB.Driver** - MongoDB connectivity
- **EFCore.NamingConventions** - Snake case naming conventions

## Key Libraries
- **Quartz.NET** - Job scheduling and background tasks
- **Serilog** - Structured logging
- **AutoMapper** - Object mapping
- **LibGit2Sharp** - Git operations
- **Flurl.Http** - HTTP client operations
- **ModelContextProtocol** - MCP server implementation
- **SmartComponents.LocalEmbeddings** - AI/ML embeddings

## UI Components
- **Blazor.Bootstrap** - Bootstrap components for Blazor
- **H.NotifyIcon** - System tray integration
- **GlobalHotKeyCore** - Global keyboard shortcuts
- **Blazor.ContextMenu** - Context menu support

## Build System
- **MSBuild** with SDK-style projects
- **Single-file deployment** with self-contained runtime
- **Package lock files** enabled for reproducible builds
- **Multi-platform support** (AnyCPU, x64, ARM64)

## Common Commands

### Build & Run
```bash
# Build solution
dotnet build

# Run main application (Debug)
dotnet run --project TugboatCaptainsPlayground

# Run tests
dotnet test

# Publish single-file executable
dotnet publish -c Release --self-contained true -p:PublishSingleFile=true
```

### Database Operations
```bash
# Add migration
dotnet ef migrations add <MigrationName> --project Common

# Update database
dotnet ef database update --project Common

# Generate DbContext scaffold
dotnet ef dbcontext scaffold <ConnectionString> <Provider> --project Common
```

### Development
```bash
# Watch for changes (if supported)
dotnet watch run --project TugboatCaptainsPlayground

# Clean solution
dotnet clean

# Restore packages
dotnet restore
```

## Configuration Notes
- Uses `InvariantGlobalization=true` for reduced deployment size
- Embedded resources for static files in single-file deployment
- Debug builds output as console apps, Release builds as Windows apps
- Comprehensive static asset embedding for self-contained deployment