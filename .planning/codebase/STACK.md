# Technology Stack

**Analysis Date:** 2026-08-19

## Languages

**Primary:**
- C# (.NET 10) - Backend API and application layer
- TypeScript 5.9.3 - Frontend and component layer
- Vue 3 - Frontend framework (SPA)

**Secondary:**
- JavaScript (Node.js 20+) - Frontend tooling and build
- SQL - PostgreSQL database

## Runtime

**Environment:**
- .NET 10 SDK (required)
- Node.js 20+ (required for frontend development)
- Docker Desktop (required for PostgreSQL container)

**Package Manager:**
- NuGet - .NET package management (implicit via `dotnet` CLI)
- npm - Frontend dependencies
- Lockfile: `package-lock.json` present in `src/Okozukai.Frontend/`

## Frameworks

**Core:**
- ASP.NET Core 10 (Web API) - Backend web framework (`src/Okozukai.Api`)
- Vue 3 3.5.25 - Frontend SPA framework (`src/Okozukai.Frontend`)
- .NET Aspire 13.1.0 - Service orchestration and local development (`src/Okozukai.AppHost`)

**ORM/Data:**
- Entity Framework Core 10.0.3 - Object-relational mapping (`src/Okozukai.Infrastructure`)
- Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0 - EF Core PostgreSQL provider

**Testing:**
- xUnit 2.9.3 - .NET unit testing framework (`tests/Okozukai.UnitTests`)
- Vitest 4.0.18 - Frontend unit testing (`src/Okozukai.Frontend`)
- Playwright 1.58.2 - Frontend E2E testing (`src/Okozukai.Frontend`)
- Microsoft.AspNetCore.Mvc.Testing - ASP.NET Core integration testing (`tests/Okozukai.IntegrationTests`)

**Build/Dev:**
- Vite 7.3.1 - Frontend build tool and dev server (`src/Okozukai.Frontend`)
- Tailwind CSS 4.2.0 - Utility-first CSS framework (`src/Okozukai.Frontend`)
- PostCSS 8.5.6 - CSS processing (`src/Okozukai.Frontend`)
- Autoprefixer 10.4.24 - CSS vendor prefixes (`src/Okozukai.Frontend`)

## Key Dependencies

**Critical:**
- axios 1.13.5 - HTTP client for frontend API calls (`src/Okozukai.Frontend`)
- vue-router 4.6.4 - Client-side routing for Vue SPA
- chart.js 4.5.1 - Chart library for data visualization
- vue-chartjs 5.3.3 - Vue wrapper for Chart.js

**Infrastructure:**
- Microsoft.Extensions.DependencyInjection.Abstractions 10.0.3 - Dependency injection
- Microsoft.Extensions.Logging.Abstractions 10.0.3 - Logging abstractions
- Microsoft.Extensions.Http.Resilience 10.1.0 - HTTP resilience policies
- Microsoft.Extensions.ServiceDiscovery 10.1.0 - Service discovery for Aspire
- vite-plugin-pwa 1.2.0 - PWA support for Vue frontend
- jsdom 28.1.0 - DOM implementation for frontend tests

**Observability:**
- OpenTelemetry.Exporter.OpenTelemetryProtocol 1.14.0 - OTLP telemetry export
- OpenTelemetry.Extensions.Hosting 1.14.0 - OpenTelemetry integration
- OpenTelemetry.Instrumentation.AspNetCore 1.14.0 - ASP.NET Core instrumentation
- OpenTelemetry.Instrumentation.Http 1.14.0 - HTTP client instrumentation
- OpenTelemetry.Instrumentation.Runtime 1.14.0 - .NET runtime instrumentation

**Testing:**
- Moq 4.20.72 - Mocking framework for unit tests
- Microsoft.NET.Test.Sdk 17.14.1 - Test platform SDK
- @vue/test-utils 2.4.6 - Vue component testing utilities
- coverlet.collector 6.0.4 - Code coverage collection

**Frontend:**
- @tailwindcss/postcss 4.2.0 - Tailwind CSS PostCSS plugin
- @vitejs/plugin-vue 6.0.4 - Vite plugin for Vue 3 SFC
- @types/node 24.10.1 - TypeScript types for Node.js
- @vue/tsconfig 0.8.1 - Recommended TypeScript config for Vue
- vue-tsc 3.1.5 - TypeScript compiler for Vue single-file components

## Configuration

**Environment:**
- `.env*` files - Not present in repository; configuration via environment variables and `appsettings.json`
- User secrets - PostgreSQL connection string stored in .NET user secrets during development
  - Location: `appsettings.json` in `src/Okozukai.Api` and `src/Okozukai.AppHost`
- Environment variables:
  - `ASPNETCORE_ENVIRONMENT` - Runtime environment (Development/Production)
  - `VITE_API_URL` - Frontend API base URL (default: `http://localhost:5005`)
  - `TAILNET_IP` - Tailscale IP for remote access
  - `TAILNET_API_PORT` - API port on Tailnet (default: 5005)
  - `TAILNET_FRONTEND_PORT` - Frontend port on Tailnet (default: 5173)
  - `OTEL_EXPORTER_OTLP_ENDPOINT` - OpenTelemetry OTLP export endpoint

**Build:**
- `tsconfig.json` - TypeScript configuration (`src/Okozukai.Frontend`)
- `tsconfig.app.json` - Application TypeScript config
- `tsconfig.node.json` - Node.js tooling TypeScript config
- `vite.config.ts` - Vite build configuration with Vue and PWA plugins
- `playwright.config.ts` - Playwright E2E test configuration
- `tailwind.config.js` - Tailwind CSS theme configuration
- `postcss.config.js` - PostCSS processing configuration
- `.csproj` files - Project file structure defines .NET dependencies and targets (.NET 10.0)
  - `src/Okozukai.Api/Okozukai.Api.csproj` - Web API project
  - `src/Okozukai.Application/Okozukai.Application.csproj` - Application services
  - `src/Okozukai.Infrastructure/Okozukai.Infrastructure.csproj` - Data persistence
  - `src/Okozukai.Domain/Okozukai.Domain.csproj` - Domain entities (no external dependencies)
  - `src/Okozukai.AppHost/Okozukai.AppHost.csproj` - Aspire orchestration
  - `src/Okozukai.ServiceDefaults/Okozukai.ServiceDefaults.csproj` - Shared observability/health checks
  - `tests/Okozukai.UnitTests/Okozukai.UnitTests.csproj` - Unit tests
  - `tests/Okozukai.IntegrationTests/Okozukai.IntegrationTests.csproj` - Integration tests
- `Okozukai.slnx` - Solution file (Visual Studio 2022+)

## Platform Requirements

**Development:**
- .NET 10 SDK - Full .NET development environment
- Node.js 20+ - JavaScript runtime and npm package manager
- Docker Desktop - PostgreSQL container runtime
- Git - Version control
- Visual Studio 2022+ or VS Code - Recommended IDE

**Production/Deployment:**
- .NET 10 Runtime - Server-side execution environment
- PostgreSQL 13+ - Database server
- Node.js 20+ - For building frontend (if building from source)
- Docker (optional) - Container orchestration for deployment

---

*Stack analysis: 2026-08-19*
