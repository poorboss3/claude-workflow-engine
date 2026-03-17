# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A BPM (Business Process Management) workflow engine with a .NET 8 C# backend and Vue 3 frontend. It supports complex approval workflows including sequential/parallel signing, dynamic approver resolution, proxy/delegation, and countersigning.

## Commands

### Backend (.NET 8)

```bash
# Run API (dev mode, uses in-memory DB — no external services needed)
cd WorkflowEngine/src/WorkflowEngine.API && dotnet run
# API: http://localhost:5000  |  Swagger: http://localhost:5000/swagger

# Build solution
cd WorkflowEngine && dotnet build WorkflowEngine.sln

# Run tests
cd WorkflowEngine && dotnet test tests/WorkflowEngine.Tests/

# Run a single test class
cd WorkflowEngine && dotnet test tests/WorkflowEngine.Tests/ --filter "ClassName=YourTestClass"
```

### Frontend (Vue 3 + Vite)

```bash
cd workflow-ui
npm install
npm run dev      # Dev server: http://localhost:3000 (proxies /api → http://localhost:5000)
npm run build    # Production build
npm run preview  # Preview production build
```

## Architecture

### Backend: Clean Architecture (DDD + CQRS)

```
WorkflowEngine/src/
├── WorkflowEngine.Domain/        # Entities, enums, repository interfaces — no dependencies
├── WorkflowEngine.Application/   # CQRS commands/queries via MediatR, DTOs, FluentValidation
├── WorkflowEngine.Infrastructure/# EF Core, PostgreSQL, Redis, RabbitMQ/MassTransit implementations
└── WorkflowEngine.API/           # ASP.NET Core controllers, middleware, DI composition root
```

Dependency direction: `API → Application → Domain` (Infrastructure implements Domain interfaces).

**Key domain entities:** `ProcessDefinition`, `ProcessInstance`, `ApprovalStep`, `WorkflowTask`, `ApprovalRule`, `ProxyConfig`, `DelegationConfig`.

**Key enums:** `ProcessStatus` (Running/Completed/Rejected/Withdrawn), `TaskStatus`, `StepType`, `JointSignPolicy` (Sequential/Parallel/Both), `TaskAction` (Approve/Reject/Return/Countersign/Delegate).

**Dev vs. Production:** `appsettings.json` has `"UseInMemoryDb": true` for development — no PostgreSQL, Redis, or RabbitMQ needed. Switch to `false` and configure connection strings for production.

**Authentication:** In dev, current user is read from the `X-User-Id` request header. In production, JWT claims are used.

**Extension Points:** External HTTP callbacks allow business systems to dynamically resolve approvers based on form data. Configured with timeout/retry via Polly.

### Frontend: Vue 3 SPA

```
workflow-ui/src/
├── views/       # ProcessListView, ProcessCreateView, FlowView, TaskInboxView
├── components/  # WorkflowDiagram.vue (Vue Flow visualization)
├── api/         # Axios HTTP clients
├── stores/      # Pinia: flowStore (process instance data), userStore (current user)
└── router/      # Vue Router config
```

**Routes:**
- `/processes` → ProcessListView (workflow definitions)
- `/processes/create` → ProcessCreateView
- `/flow/:instanceId` → FlowView (instance detail + approval actions)
- `/tasks` → TaskInboxView (pending/completed task inbox; default route)

**UI:** Element Plus component library (Chinese locale configured). Workflow diagrams rendered with `@vue-flow/core`.

### Key API Endpoints

```
POST /api/v1/process-instances/prepare    # Resolve default approver list via extension point
POST /api/v1/process-instances            # Submit a new process instance
POST /api/v1/tasks/{id}/approve
POST /api/v1/tasks/{id}/reject
POST /api/v1/tasks/{id}/return
POST /api/v1/tasks/{id}/countersign
GET  /api/v1/tasks/pending                # Current user's pending tasks
GET  /api/v1/tasks/completed              # Current user's completed tasks
```

## Tech Stack

| Layer | Technology |
|---|---|
| Backend runtime | .NET 8, ASP.NET Core 8 |
| ORM | Entity Framework Core 8 + Npgsql |
| CQRS/Mediator | MediatR 12.x |
| Validation | FluentValidation.AspNetCore 11.x |
| Cache | StackExchange.Redis |
| Messaging | MassTransit + RabbitMQ |
| HTTP resilience | Polly 8.x |
| Mapping | Mapster 7.x |
| Logging | Serilog |
| Frontend | Vue 3.4, Vite 5.2, Pinia, Vue Router 4 |
| UI components | Element Plus 2.6 |
| Flow visualization | @vue-flow/core |

## Reference Documents

- `workflow-engine-design.md` — 12-chapter system design (data models, workflows, extension points, notifications)
- `workflow-engine-implementation.md` — Detailed implementation guide (DB schema, API specs, concurrency control)
