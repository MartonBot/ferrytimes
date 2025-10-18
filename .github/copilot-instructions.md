# Copilot Instructions for FerryTimes

This document provides guidance for AI coding agents working on the FerryTimes project. It outlines the architecture, workflows, conventions, and integration points to help agents be productive and aligned with the project's goals.

## Project Overview

FerryTimes is a .NET-based application designed to manage ferry schedules and related data. The project is structured into two main components:

1. **FerryTimes.Api**: The API layer that exposes endpoints for interacting with ferry schedules and other data.
2. **FerryTimes.Core**: The core library containing business logic, data models, and services.

### Key Directories
- `src/FerryTimes.Api`: Contains the API project, including `Program.cs` for application startup and configuration.
- `src/FerryTimes.Core`: Contains the core logic, including:
  - `Data/`: Entity Framework `DbContext` and migrations.
  - `Scraping/`: Scrapers for fetching ferry schedules from external sources.
  - `Services/`: Core services for processing and managing data.

## Architecture and Data Flow

1. **Data Scraping**: Scrapers in `Scraping/` fetch data from external ferry services (e.g., Aremiti, Terevau, Vaearai).
   - Scrapers inherit from `BaseFerryScraper` and implement the `ScrapeAsync` method.
   - `FailureNotifier` handles error notifications during scraping.
2. **Data Storage**: Data is stored in SQLite databases (`stats.db`, `timetables.db`) managed via Entity Framework.
3. **API Layer**: The API layer exposes endpoints for accessing and managing ferry schedules.

### Why This Structure?
- Separation of concerns: API and core logic are decoupled for better maintainability.
- Modularity: Scrapers and services are isolated, making it easier to add new ferry providers.

## Developer Workflows

### Building the Project
Run the following task to build the project:
```bash
# From the workspace root
dotnet build
```
Alternatively, use the VS Code task labeled `build`.

### Running the Application
The application can be run directly from the `FerryTimes.Api` project. Ensure the SQLite databases (`stats.db`, `timetables.db`) are present in the `src/FerryTimes.Api` directory.

### Debugging
- Use the `launchSettings.json` file in `src/FerryTimes.Api/Properties/` to configure debugging profiles.
- Logs are stored in `src/FerryTimes.Api/logs/` for troubleshooting.

### Database Migrations
To add or update migrations:
```bash
# Navigate to the Core project
cd src/FerryTimes.Core

# Add a migration
pwsh dotnet ef migrations add <MigrationName>

# Apply migrations
pwsh dotnet ef database update
```

## Project-Specific Conventions

1. **Scraper Design**:
   - All scrapers inherit from `BaseFerryScraper`.
   - Implement the `ScrapeAsync` method to fetch and parse data.
   - Examples: `AremitiScraper`, `TerevauScraper`, `VaearaiScraper`.
   - Use `FailureNotifier` for error handling.

2. **Service Design**:
   - Services are located in `Services/` and follow a single-responsibility principle.
   - Example: `TimetableScraperService` handles the orchestration of scrapers.

3. **Entity Framework**:
   - Use `AppDbContext` for database interactions.
   - Migrations are stored in `Migrations/`.

## Integration Points

- **External Dependencies**:
  - SQLite: Used for local data storage.
  - Entity Framework Core: ORM for database management.
  - Scrapers: Integrate with external ferry service APIs.

- **Cross-Component Communication**:
  - Scrapers feed data into the database via services.
  - The API layer queries the database to serve client requests.

## Examples

### Adding a New Scraper
1. Create a new class in `Scraping/` inheriting from `BaseFerryScraper`.
2. Implement the `ScrapeAsync` method.
3. Register the scraper in `TimetableScraperService`.

### Adding a New API Endpoint
1. Define the endpoint in `Program.cs` using minimal APIs.
2. Implement the logic in `FerryTimes.Core`.
3. Test the endpoint using `FerryTimes.Api.http`.

---

This document is a living guide. Update it as the project evolves to ensure it remains accurate and helpful.