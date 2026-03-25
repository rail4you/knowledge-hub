# Tenant User Creation Feature - Implementation Summary

## Goal

Add tenant selection to the user creation dialog at `/identity/users` so that users can be created under a specific tenant instead of the host, and display all users including tenant users in the user list.

## Architecture

The solution creates a dedicated API endpoint for creating tenant users, which properly handles tenant context switching on the backend. It also provides an endpoint to query all users across tenants.

## Backend Changes

### 1. CreateTenantUserDto.cs
**Location**: `src/KnowledgeHub.Application.Contracts/Identity/CreateTenantUserDto.cs`

- DTO for creating a user under a specific tenant
- Contains: TenantId, UserName, EmailAddress, Password, Name, Surname, IsActive, RoleNames

### 2. ITenantUserAppService.cs
**Location**: `src/KnowledgeHub.Application.Contracts/Identity/ITenantUserAppService.cs`

- Interface defining:
  - `CreateUserForTenantAsync(CreateTenantUserDto input)` - Create user in specific tenant
  - `GetAllUsersIncludingTenantsAsync(GetIdentityUsersInput input)` - Get all users across tenants

### 3. TenantUserAppService.cs
**Location**: `src/KnowledgeHub.Application/Identity/TenantUserAppService.cs`

- Uses `ICurrentTenant.Change(tenantId)` to switch tenant context before user creation
- Uses `IdentityUserManager` to create the user within the correct tenant context
- Uses `IDataFilter.Disable<IMultiTenant>()` to query all users across tenants
- Returns `IdentityUserDto` after successful creation

### 4. Password Policy Configuration
**Location**: `src/KnowledgeHub.HttpApi.Host/KnowledgeHubHttpApiHostModule.cs`

- Relaxed password requirements:
  - `RequireDigit = false`
  - `RequireLowercase = false`
  - `RequireNonAlphanumeric = false`
  - `RequireUppercase = false`
  - `RequiredLength = 6`
  - `RequiredUniqueChars = 1`

## Frontend Changes

### 1. identity-form-prop-contributors.ts
**Location**: `angular/src/app/identity-form-prop-contributors.ts`

- Adds `tenantId` as an extra property to the user creation form
- Dropdown shows "Host (Global)" option (null value) and tenant names

### 2. TenantUserService (proxy)
**Location**: `angular/src/app/proxy/application/identity/tenant-user.service.ts`

- Angular service that calls `/api/app/tenant-user/user-for-tenant`

### 3. CustomIdentityUserService
**Location**: `angular/src/app/custom-identity-user.service.ts`

- Replaces the default `IdentityUserService`
- When `tenantId` is present in `extraProperties`, calls `TenantUserService.createUserForTenant()`
- Otherwise, calls the standard identity API
- `getList()` calls `/api/app/tenant-user/users-including-tenants` to show all users including tenant users

### 4. app.config.ts
**Location**: `angular/src/app/app.config.ts`

- Provides `CustomIdentityUserService` in place of `IdentityUserService`

## Flow

### Create Tenant User
1. User navigates to `/identity/users`
2. User clicks "Create new user" button
3. Dialog shows tenant dropdown (first field) plus standard user fields
4. User selects a tenant (e.g., "学校") and fills in user details
5. On submit, form data is sent with `tenantId` in `ExtraProperties`
6. `CustomIdentityUserService.create()` intercepts the request
7. If `tenantId` is present, calls `/api/app/tenant-user/user-for-tenant`
8. `TenantUserAppService.CreateUserForTenantAsync()` switches tenant context
9. User is created under the selected tenant (TenantId column in database)

### View All Users
1. User navigates to `/identity/users`
2. `CustomIdentityUserService.getList()` calls `/api/app/tenant-user/users-including-tenants`
3. `TenantUserAppService.GetAllUsersIncludingTenantsAsync()` disables multi-tenant filter
4. All users (Host and tenant users) are displayed in the table

## Key Files

| File | Purpose |
|------|---------|
| `src/KnowledgeHub.Application.Contracts/Identity/CreateTenantUserDto.cs` | DTO for tenant user creation |
| `src/KnowledgeHub.Application.Contracts/Identity/ITenantUserAppService.cs` | Interface for tenant user service |
| `src/KnowledgeHub.Application/Identity/TenantUserAppService.cs` | Backend service with tenant context switching |
| `src/KnowledgeHub.HttpApi.Host/KnowledgeHubHttpApiHostModule.cs` | Password policy configuration |
| `angular/src/app/identity-form-prop-contributors.ts` | Add tenant dropdown to user creation form |
| `angular/src/app/proxy/application/identity/tenant-user.service.ts` | Angular proxy for TenantUser API |
| `angular/src/app/custom-identity-user.service.ts` | Custom IdentityUserService with tenant support |
| `angular/src/app/app.config.ts` | Provider configuration |

## API Endpoints

- **Create Tenant User**
  - **URL**: `POST /api/app/tenant-user/user-for-tenant`
  - **Body**: `CreateTenantUserDto`
  - **Response**: `IdentityUserDto`

- **Get All Users Including Tenants**
  - **URL**: `GET /api/app/tenant-user/users-including-tenants`
  - **Query**: `maxResultCount`, `skipCount`, `filter`, `sorting`
  - **Response**: `PagedResultDto<IdentityUserDto>`

## Notes

- The `tenantId` is sent as a string (UUID) in `ExtraProperties`
- If "Host (Global)" is selected, `tenantId` is null and user is created via standard identity API
- The role assignment (角色) dropdown remains functional - users can have multiple roles
- Password requirements are relaxed: minimum 6 characters, no special requirements
- All users across tenants are visible in the user list when accessed from Host
