# Document Indexing & Search System - Development Plan

## Overview

Integrate liteparse for document parsing with Meilisearch indexing, featuring:
- Async background job processing with retry logic
- Real-time job status tracking
- PDF.js-based search preview with highlighting

## Design Decisions

| Decision | Choice |
|----------|--------|
| Parser | Replace with liteparse only |
| Preview | PDF.js rendering with text overlay |
| Retry | Auto retry with exponential backoff (max 3 attempts) |

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Frontend (Angular)                              │
├─────────────────────────────────────────────────────────────────────────────┤
│  Resource Upload  │  Indexing Job List  │  Search with Preview              │
└─────────┬─────────┴──────────┬──────────┴──────────┬────────────────────────┘
          │                    │                     │
          ▼                    ▼                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Backend API                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│  ChunkUploadCtrl   │  IndexingJobAppService  │  SearchAppService            │
└─────────┬─────────┴──────────┬───────────────┴──────────┬───────────────────┘
          │                    │                          │
          ▼                    ▼                          ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Background Job Queue                                 │
│                    (ABP IBackgroundJobManager)                               │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                     DocumentIndexingBackgroundJob                            │
│  ┌──────────────┐    ┌──────────────────┐    ┌─────────────────────┐        │
│  │  1. Parse    │ -> │ 2. Transform     │ -> │ 3. Index to         │        │
│  │  (liteparse) │    │  to PageContent  │    │    Meilisearch      │        │
│  └──────────────┘    └──────────────────┘    └─────────────────────┘        │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Implementation Status

### ✅ Completed Backend Implementation

| File | Description |
|------|-------------|
| `src/KnowledgeHub.Domain/Search/DocumentIndexingJob.cs` | Indexing job entity with status tracking |
| `src/KnowledgeHub.Domain/Search/PageContent.cs` | Parsed page content with text items |
| `src/KnowledgeHub.Application/Search/LiteparseService.cs` | Liteparse CLI integration |
| `src/KnowledgeHub.Application/Search/DocumentIndexingBackgroundJob.cs` | Background job with retry logic |
| `src/KnowledgeHub.Application/Search/IndexingJobAppService.cs` | CRUD API for indexing jobs |
| `src/KnowledgeHub.Application.Contracts/Search/IndexingJobAppService.cs` | Interface and DTOs |
| `src/KnowledgeHub.Application.Contracts/Search/DocumentIndexingJobArgs.cs` | Background job arguments |

### ✅ Completed Frontend Implementation

| File | Description |
|------|-------------|
| `angular/src/app/admin/indexing-jobs/indexing-jobs.component.ts` | Admin UI for job monitoring |
| `angular/src/app/search/search.service.ts` | Updated with indexing job methods |
| `angular/src/app/app.routes.ts` | Added route for indexing jobs |

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/indexing-job` | List indexing jobs (paged) |
| GET | `/api/app/indexing-job/{id}` | Get job by ID |
| GET | `/api/app/indexing-job/by-resource/{resourceId}` | Get job by resource |
| POST | `/api/app/indexing-job` | Create new indexing job |
| POST | `/api/app/indexing-job/{id}/retry` | Retry failed job |
| POST | `/api/app/indexing-job/{id}/cancel` | Cancel job |
| POST | `/api/app/indexing-job/retry-all-failed` | Retry all failed jobs |

## Usage

### 1. Automatic Indexing (on resource upload)
When a resource is created via `ResourceAppService.CreateAsync`, an indexing job is automatically queued.

### 2. Manual Indexing
```typescript
// From Angular
this.searchService.createIndexingJob({
  resourceId: 'resource-id-here'
}).subscribe(job => {
  console.log('Indexing job created:', job.id);
});
```

### 3. Monitor Indexing Jobs
Navigate to `/admin/indexing-jobs` to see job status, progress, and retry failed jobs.

## Database Migration

Run the following command to apply the migration:
```bash
./dev.sh migrate
```

## Estimated Effort

| Task | Status |
|------|--------|
| Domain entities + EF migration | ✅ Completed |
| LiteparseService | ✅ Completed |
| Background job with retry | ✅ Completed |
| API endpoints | ✅ Completed |
| Update MeiliSearchService | ✅ Completed |
| Frontend: Indexing job list | ✅ Completed |
| Frontend: Search preview | 📝 Basic implementation |
| Frontend: PDF viewer with highlights | 📝 Basic implementation |

## Next Steps

1. Install liteparse CLI tool on the server
2. Run database migration
3. Test the indexing workflow end-to-end
4. Enhance PDF viewer with actual PDF.js rendering
