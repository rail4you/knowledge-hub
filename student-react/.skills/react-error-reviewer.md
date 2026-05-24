# React Error Reviewer Skill

A skill for reviewing React applications to catch runtime errors, console warnings, and common issues.

## Quick Use

When user reports a React app error or wants to check for issues:

```
Use the reviewer agent with this task:
"Review the React student portal at http://localhost:5173 for:
1. Console errors (check browser console)
2. Runtime errors in the page
3. Missing provider errors (QueryClient, Auth, etc.)
4. API call failures
5. Build errors if any

Report findings and suggest fixes."
```

## Manual Check Steps

### 1. Check Build First
```bash
cd student-react && npm run build
```

### 2. Check Runtime Errors
Open http://localhost:5173 in browser and check:
- Browser console for red error messages
- Network tab for failed API calls
- React DevTools for component errors

### 3. Check for Common Patterns

| Error Message | Likely Cause | Check |
|--------------|--------------|-------|
| `No QueryClient set` | Missing `QueryClientProvider` | Check `main.tsx` |
| `useAuth must be inside AuthProvider` | Missing `AuthProvider` | Check `App.tsx` |
| `Cannot read property of undefined` | Null data | Check API response handling |
| `Objects are not valid as a React child` | Wrong data type | Check `.map()` usage |

### 4. Key Files to Check

- `src/main.tsx` - Provider setup
- `src/App.tsx` - Route configuration
- `src/lib/auth.tsx` - Auth context
- `src/pages/*.tsx` - Page components

## Output Template

```markdown
## Error Review: [Page Name]

### Errors Found
- Error 1: [message]
- Error 2: [message]

### Severity
- 🔴 Critical: [blocks functionality]
- 🟡 Warning: [affects UX]
- 🟢 Info: [cosmetic]

### Root Cause
[Explain why the error occurs]

### Fix
```[code fix]
```

### Verification
- [ ] Build passes
- [ ] No console errors
- [ ] Page loads correctly
```

## Common Fixes

### Missing QueryClientProvider
```tsx
// main.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const queryClient = new QueryClient();

<QueryClientProvider client={queryClient}>
  <App />
</QueryClientProvider>
```

### Missing AuthProvider
```tsx
// App.tsx
import { AuthProvider } from './lib/auth';

<AuthProvider>
  <BrowserRouter>...</BrowserRouter>
</AuthProvider>
```

### Null Data Handling
```tsx
// Before (error)
{user.name}

// After (safe)
{user?.name}
```