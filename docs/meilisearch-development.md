# Meilisearch Document Search - Development Log

## Project Overview
Implement a comprehensive document search system using Meilisearch for the KnowledgeHub platform.

## Requirements Summary
1. **Document Indexing**: Upload resources (DOCX, PDF) → Extract content by page → Store in Meilisearch index
2. **Embedding Support**: Prepare infrastructure for vector embeddings (deferred LLM integration)
3. **Async Indexing**: Index operations return tasks, need status tracking
4. **Search Features**: Keyword search, hybrid search, natural language search (future)
5. **Search Results**: Display by document-page with preview and highlighting
6. **Filtering**: By resource type, category, date, file extension
7. **Statistics**: Track search queries, resource views, exposure counts

---

## Development Log

### Phase 1: Foundation & Database Design

#### 2026-03-18 - Day 1

**Task 1: Initialize Project Structure**
- Created development documentation
- Analyzed existing Resource entity structure

**Task 2: Domain Entities Created**
- `DocumentIndex` - Stores indexed document pages with content and embeddings
- `SearchQuery` - Tracks user search queries
- `ResourceViewLog` - Tracks user resource views
- `SearchStatistics` - Aggregated daily search statistics
- `ResourceExposure` - Tracks resource appearances in search results

**Task 3: Enums Defined**
- `IndexStatus` - Pending, Processing, Completed, Failed
- `SearchType` - Keyword, Hybrid, NaturalLanguage
- `ViewSource` - Search, Direct, Recommended

**NuGet Packages Installed:**
- `Meilisearch` - Meilisearch .NET client
- `NPOI` - DOCX file parsing
- `PdfSharpCore` - PDF file parsing
- `Microsoft.Extensions.Http` - HTTP client factory

**Configuration Added:**
- Meilisearch connection settings in appsettings.json
- Embedding service configuration (placeholder)

---

## Implementation Status

### ✅ Completed (2026-03-18)

1. **NuGet Packages Installed**
   - Meilisearch (0.12.0)
   - NPOI (2.7.0) - DOCX parsing
   - PdfSharpCore (1.3.65) - PDF parsing
   - Microsoft.Extensions.Http

2. **Domain Layer**
   - `DocumentIndex` entity - Indexed document pages
   - `SearchQuery` entity - Search query logging
   - `ResourceViewLog` entity - User behavior tracking
   - `SearchStatistics` entity - Aggregated statistics
   - `ResourceExposure` entity - Resource click tracking
   - Enums: `IndexStatus`, `SearchType`, `ViewSource`
   - Repository interfaces for all entities
   - Navigation properties added to `Resource` entity

3. **Application Layer**
   - `ISearchAppService` - Main search API
   - `IMeiliSearchService` - Meilisearch integration (HTTP-based)
   - `IDocumentExtractionService` - Extracts text from DOCX/PDF files
   - `IEmbeddingService` - Placeholder for embedding API (deferred)
   - `ISearchAnalyticsService` - Statistics and logging
   - All DTOs for search operations

4. **Database Tables Created**
   - `KhDocumentIndices` - Indexed document pages
   - `KhResourceExposures` - Resource click tracking
   - `KhResourceViewLogs` - User view history
   - `KhSearchQueries` - User search history
   - `KhSearchStatistics` - Aggregated statistics

5. **Configuration**
   - `appsettings.json` - Meilisearch and EmbeddingService config added

6. **Angular Frontend**
   - `SearchService` - Angular service for API calls
   - `SearchComponent` - Search UI with filters, results, pagination
   - Route added at `/search`
   - Menu item added in navigation

7. **Permissions (Defined)**
   - `KnowledgeHub.Search` - Search resources (all authenticated users)
   - `KnowledgeHub.Search.ManageIndex` - Manage index (Admin, Teacher)
   - `KnowledgeHub.Search.ViewStatistics` - View statistics (Admin, Teacher)
   - Permissions seeded for all roles in IdentityDataSeederContributor

### 🔜 Next Steps

1. Run database migrations to create new tables (if not done)
2. Configure Meilisearch server at localhost:7700
3. Run DB Migrator to apply changes
4. Run the API host and Angular app
5. Test indexing a resource
6. Test search functionality
7. Configure embedding service REST API endpoint

### 📋 Deferred Features

- LLM Integration for natural language search
- Vector embedding generation
- Semantic search
- Personalized recommendations
- Advanced analytics dashboard
- Index status display UI

---

## Architecture

### Backend Structure
```
KnowledgeHub.Domain/Search/
├── Entities/
│   ├── DocumentIndex.cs
│   ├── SearchQuery.cs
│   ├── ResourceViewLog.cs
│   ├── SearchStatistics.cs
│   └── ResourceExposure.cs
└── Enums/
    ├── IndexStatus.cs
    ├── SearchType.cs
    └── ViewSource.cs

KnowledgeHub.Application/Search/
├── MeiliSearchService.cs
├── DocumentExtractionService.cs
├── EmbeddingService.cs (interface)
├── SearchAnalyticsService.cs
└── RecommendationService.cs (interface)

KnowledgeHub.Application.Contracts/Search/
├── Dtos/
│   ├── SearchQueryDto.cs
│   ├── SearchResultDto.cs
│   └── DocumentSearchResultDto.cs
└── Interfaces/
    ├── IMeiliSearchService.cs
    └── IDocumentExtractionService.cs
```

### Frontend Structure
```
angular/src/app/search/
├── search.component.ts
├── search.component.html
├── search.component.css
├── search.service.ts
├── models.ts
└── search.routes.ts
```

---

## API Endpoints (To Be Implemented)

### Search API
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/search` | Search documents |
| GET | `/api/app/search/hybrid` | Hybrid search (keyword + semantic) |
| GET | `/api/app/search/facets` | Get facet counts |
| GET | `/api/app/search/history` | Get user's search history |
| GET | `/api/app/search/statistics` | Get search statistics (admin) |

### Indexing API
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/app/search/index/{resourceId}` | Index a resource |
| GET | `/api/app/search/index/status/{taskId}` | Get indexing task status |
| DELETE | `/api/app/search/index/{resourceId}` | Remove resource from index |
| GET | `/api/app/search/index/status` | Get all indexing tasks |

### Analytics API
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/app/search/analytics/view` | Log resource view |
| GET | `/api/app/search/analytics/popular` | Get popular searches |
| GET | `/api/app/search/analytics/top-resources` | Get top exposed resources |

---

## Meilisearch Index Schema

### Index: `documents`

**Searchable Attributes:**
- `pageContent`
- `pageTitle`
- `resourceName`
- `keywords`
- `description`

**Filterable Attributes:**
- `resourceId`
- `resourceType`
- `categoryId`
- `fileExtension`
- `uploadDate`
- `tenantId`
- `status`

**Sortable Attributes:**
- `uploadDate`
- `relevanceScore`
- `pageNumber`

**Typo Tolerance:** Enabled

**Ranking Rules:**
1. Words
2. Typo
3. Proximity
4. Attribute
5. Sort
6. Exactness

---

## Database Tables

### KhDocumentIndex
| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| ResourceId | GUID | Reference to Resource |
| PageNumber | INT | Page number in document |
| PageContent | TEXT | Extracted text content |
| PageTitle | NVARCHAR(512) | Page title (if extracted) |
| EmbeddingVector | bytea | Vector embedding (JSON) |
| IndexingTaskId | GUID | Meilisearch task ID |
| IndexStatus | INT | Status enum |
| TenantId | GUID? | Multi-tenancy |

### KhSearchQuery
| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| UserId | GUID | User who searched |
| QueryText | NVARCHAR(500) | Search query |
| SearchType | INT | Type of search |
| ResultCount | INT | Number of results |
| Filters | NVARCHAR(1000) | Applied filters |

### KhResourceViewLog
| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| ResourceId | GUID | Resource viewed |
| UserId | GUID | User who viewed |
| PageNumber | INT? | Page viewed |
| ViewDurationSeconds | INT | Time spent viewing |
| ViewSource | INT | How user found resource |

### KhSearchStatistics
| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| OrganizationId | GUID | Organization |
| Date | DATE | Statistics date |
| TotalSearches | INT | Total searches |
| UniqueUsers | INT | Unique searchers |
| AvgResultCount | DECIMAL | Average results |
| TopSearchTerm | NVARCHAR(500) | Most common query |

### KhResourceExposure
| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| ResourceId | GUID | Resource |
| TimesInResults | INT | Times shown |
| TimesClicked | INT | Times clicked |
| LastSeen | DATETIME | Last appearance |

---

## Frontend Features

### Search Component
- Search input with debounce (300ms)
- Search type toggle (Keyword / Hybrid)
- Filter panel (type, category, date, extension)
- Results grouped by document-page
- Highlighting of matched text
- Preview of surrounding context
- Pagination (20 items per page)
- Sorting (relevance, date, popularity)

### Index Status Component
- List of indexing tasks
- Status indicators (pending/processing/completed/failed)
- Task details (resource, pages, progress)
- Retry failed tasks

---

## Deferred Features
1. LLM Integration for natural language search
2. Vector embedding generation
3. Semantic search
4. Personalized recommendations
5. Advanced analytics dashboard

---

## Configuration

### appsettings.json
```json
{
  "Meilisearch": {
    "Host": "http://localhost:7700",
    "ApiKey": "",
    "IndexName": "documents",
    "EmbeddingDimension": 768
  },
  "EmbeddingService": {
    "BaseUrl": "http://localhost:8000/api/embeddings",
    "ApiKey": ""
  }
}
```

---

## Notes
- Using ABP Framework's conventional controllers
- Standalone components in Angular 21
- Signals for state management in frontend
- OnPush change detection strategy
- Multi-tenancy support enabled
