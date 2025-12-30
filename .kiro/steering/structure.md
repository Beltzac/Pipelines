# Project Structure

## Solution Organization

The solution follows a clean architecture pattern with clear separation of concerns:

```
Pipelines.sln
├── TugboatCaptainsPlayground/    # Main desktop application
├── Common/                       # Shared business logic and data access
├── Generation/                   # Code generation utilities
└── Tests/                       # Unit and integration tests
```

## Project Details

### TugboatCaptainsPlayground (Main Application)
- **Type**: Blazor Server desktop app with Photino.NET
- **Purpose**: UI layer and application entry point
- **Key Folders**:
  - `Components/` - Blazor components (Pages, Shared, Layout)
  - `StateServices/` - Application state management
  - `McpServer/` - MCP server tools and integration
  - `wwwroot/` - Static web assets (CSS, JS, images)
  - `Assets/` - Application icons and resources

### Common (Shared Library)
- **Type**: .NET 9.0 class library
- **Purpose**: Business logic, data access, and shared services
- **Key Folders**:
  - `Models/` - Domain models and DTOs
  - `Services/` - Business logic services with interfaces
  - `Repositories/` - Data access layer (TCP and internal)
  - `Jobs/` - Quartz.NET background jobs
  - `Utils/` - Utility classes and extensions
  - `Migrations/` - Entity Framework migrations
  - `AOP/` - Aspect-oriented programming (logging, timing)
  - `ExternalApis/` - External API facades and interfaces

### Tests
- **Type**: TUnit test project
- **Purpose**: Unit and integration testing
- **Structure**: Mirrors the `Common/` project structure
- **Tools**: TUnit, FluentAssertions, Moq, EF Core InMemory

### Generation
- **Type**: .NET Standard 2.0 analyzer project
- **Purpose**: Source code generation utilities

## Architectural Patterns

### Dependency Injection
- Services registered in `ServiceCollectionExtensions.cs`
- Interface-based design with clear abstractions
- Scoped services for database operations

### Repository Pattern
- `IRepositoryDatabase` for internal SQLite operations
- `IOracleRepository` and `IMongoRepository` for external systems
- Separation between TCP and internal repositories

### Service Layer
- Business logic encapsulated in service classes
- All services have corresponding interfaces in `Services/Interfaces/`
- Configuration, logging, and external API integration

### State Management
- Blazor state services in `StateServices/` folder
- Each major feature has its own state service
- Reactive state updates using SignalR where needed

## Naming Conventions

### Files & Folders
- PascalCase for all C# files and folders
- Descriptive names indicating purpose (e.g., `OracleSchemaService.cs`)
- Interface files prefixed with `I` (e.g., `IConfigurationService.cs`)

### Database
- Snake_case naming convention via EFCore.NamingConventions
- Migration files include timestamp and descriptive name
- DbContext suffix for Entity Framework contexts

### Components
- Blazor components use PascalCase
- Page components in `Components/Pages/`
- Shared components in `Components/Shared/`
- Layout components in `Components/Layout/`

## Configuration Management

### Settings
- `appsettings.json` for application configuration
- Environment-specific settings in `appsettings.{Environment}.json`
- Configuration models in `Common/Models/ConfigModel.cs`

### Database
- SQLite for local storage (`DBUtils.MainDBPath`)
- Connection strings managed through configuration service
- Automatic migrations on application startup

## Key Conventions

### Error Handling
- Structured logging with Serilog
- Exception handling at service boundaries
- User-friendly error messages in UI components

### Async/Await
- Async methods suffixed with `Async`
- Proper cancellation token usage
- Task-based asynchronous patterns throughout

### Testing
- Test classes mirror source structure
- Descriptive test method names
- Arrange-Act-Assert pattern
- Mock external dependencies