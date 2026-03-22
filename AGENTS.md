# KnowledgeHub - Project Guidelines

## Project Overview

This is a layered startup solution based on Domain Driven Design (DDD) using [ABP Framework](https://abp.io). The project consists of:

- **Backend**: ASP.NET Core with ABP Framework (.NET)
- **Frontend**: Angular (TypeScript)
- **Architecture**: Layered monolith

---

## Development Rules

For detailed development rules, see: `angular/.claude/CLAUDE.md`

### Quick Reference

**ABP / .NET:**
- Follow ABP's standard folder structure: `*.Application`, `*.Domain`, `*.EntityFrameworkCore`, `*.HttpApi`
- Use C# 10+ features, LINQ, and lambda expressions
- Follow Microsoft C# Coding Conventions
- Use async/await for I/O operations
- Implement pagination with `PagedResultDto`

**Angular:**
- Prefer standalone components (default in Angular 14+)
- Use signals for state management
- Use `input()` and `output()` functions instead of decorators
- Always set `changeDetection: ChangeDetectionStrategy.OnPush`
- Use native control flow (`@if`, `@for`, `@switch`)
- Use the `inject()` function instead of constructor injection

---

## Environments

| Environment | Script | Database | API | Angular |
|-------------|--------|----------|-----|---------|
| Development | `./dev.sh` | localhost:5433 (local PostgreSQL) | https://localhost:44305 | http://localhost:4200 |
| Production | `./etc/docker/run-docker.sh` | postgres:5432 (Docker container) | https://localhost:44354 | http://localhost:4200 |

---

## Development (dev.sh)

**IMPORTANT: Agent should NOT start services automatically.** User manages services via `dev.sh`.

### Commands

```bash
./dev.sh start              # Start API + Angular
./dev.sh start api          # Start only API
./dev.sh start angular      # Start only Angular
./dev.sh stop               # Stop all services
./dev.sh restart            # Restart all services
./dev.sh status             # Show service status
./dev.sh log api            # View API logs (last 100 lines)
./dev.sh log angular        # View Angular logs (last 100 lines)
./dev.sh tail api           # Tail API logs in real-time
./dev.sh tail angular       # Tail Angular logs in real-time
./dev.sh migrate            # Run database migration
```

### Development URLs

- **API**: https://localhost:44305
- **Swagger**: https://localhost:44305/swagger
- **Angular**: http://localhost:4200
- **Database**: localhost:5433 (PostgreSQL)

### Default Credentials

- **Username**: admin
- **Password**: 1q2w3E*

---

## Production (Docker)

### Commands

```bash
cd etc/docker
./run-docker.sh            # Start all Docker services
docker compose down        # Stop all services
docker compose logs -f api # View API logs
docker compose logs -f angular # View Angular logs
```

### Production URLs

- **API**: https://localhost:44354
- **Swagger**: https://localhost:44354/swagger
- **Angular**: http://localhost:4200

---

## Key Commands

- **Install dependencies**: `abp install-libs` (in solution root)
- **Generate API proxy**: `abp generate-proxy -t ng` (in `angular/` folder) - ALWAYS use this to generate frontend API services, NEVER manually edit proxy files
- **Run tests**: `dotnet test` (in solution root)
