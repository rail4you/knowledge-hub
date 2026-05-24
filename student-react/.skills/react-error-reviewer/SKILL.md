# React App Error Reviewer Skill

Use this skill to review React applications for runtime errors and common issues.

## When to Use

- User reports a runtime error or console error
- User wants to proactively check a React app for errors
- After deploying a new feature, verify no errors occur

## How to Use

### Step 1: Run the App Check

Use Playwright to open the app and capture console errors:

```bash
npx playwright test --reporter=line << 'EOF'
import { test, expect } from '@playwright/test';

test('check for runtime errors', async ({ page }) => {
  const errors: string[] = [];
  
  page.on('console', msg => {
    if (msg.type() === 'error') {
      errors.push(msg.text());
    }
  });
  
  page.on('pageerror', err => {
    errors.push(`Page Error: ${err.message}`);
  });
  
  await page.goto('http://localhost:5173');
  await page.waitForTimeout(3000);
  
  console.log('Console Errors:', errors);
  expect(errors.length).toBe(0);
});
EOF
```

### Step 2: Check Common Issues

Run these checks after page load:

1. **React Query Errors**: Check if `QueryClientProvider` is properly set
2. **Missing Context**: Check if required providers wrap the app
3. **API Failures**: Check for 4xx/5xx responses in network tab
4. **Routing Issues**: Check if protected routes redirect correctly
5. **Auth State**: Check if auth tokens are valid

### Step 3: Verify Build

```bash
cd student-react && npm run build
```

Check for:
- TypeScript errors
- Missing imports
- Unused exports

## Output Format

Report findings in this format:

```
## Error Review Results

### Console Errors Found
- [List of actual console.error messages]

### Root Causes
1. [Cause 1 with line number if available]
2. [Cause 2]

### Recommended Fixes
1. [Fix 1]
2. [Fix 2]

### Verification
- [ ] Fix applied
- [ ] Build passes
- [ ] No console errors on reload
```

## Common Patterns

| Error Pattern | Likely Cause | Fix |
|-------------|--------------|-----|
| `No QueryClient set` | Missing `QueryClientProvider` | Wrap app with provider |
| `useAuth must be used inside AuthProvider` | Context not wrapped | Add AuthProvider wrapper |
| `Module not found` | Missing import or dependency | Run `npm install` |
| `Cannot read property 'X' of undefined` | Null check missing | Add conditional rendering |
| `Objects are not valid as a React child` | Passing object to JSX | Use `.map()` or extract values |

## Example Workflow

```bash
# 1. Open browser and check
npx playwright open http://localhost:5173

# 2. Check for errors in console
# Look for red error messages

# 3. If errors found, identify root cause
# Check error stack trace for source file

# 4. Fix and rebuild
cd student-react && npm run build

# 5. Verify fix
npx playwright test tests/error-check.spec.ts
```