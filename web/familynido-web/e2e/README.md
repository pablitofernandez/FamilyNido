# FamilyNido E2E tests (Playwright)

Smoke tests for the four critical user flows: app shell + nav, task
create/complete, wall publish, dashboard widgets. They target a *running*
deployment — the suite does not start the API or the database.

## Prerequisites

1. **A running FamilyNido instance.** Either the dev compose
   (`docker compose -f deploy/docker-compose.dev.yml up -d`) or the prod
   stack on a real domain.
2. **A test user with a local password.** PocketID flows aren't scriptable
   from Playwright, but the local-credentials login (`POST
   /api/auth/local/login`) is. Set up the user once via the regular UI:
   - Log in via PocketID with the user you'd like to use for tests.
   - Go to `/account` → "Establecer contraseña local" and pick a password.
3. **Browsers installed by Playwright.** First time only:
   ```bash
   npx playwright install --with-deps chromium
   ```

## Configuration

Set three env vars (or put them in a `.env.e2e` consumed by your shell):

```bash
export E2E_BASE_URL=http://localhost:5173
export E2E_USER_EMAIL=tu-email@example.com
export E2E_USER_PASSWORD=elPasswordQueAcabasDePoner
```

`E2E_BASE_URL` defaults to `http://localhost:5173` (the dev server proxy)
when unset.

## Running

```bash
# All tests, headless
npm run e2e

# With Playwright's UI for debugging
npm run e2e:ui
```

Each spec is idempotent — tasks and wall posts created during a run are
also cleaned up at the end. Re-running the suite back-to-back should
always pass.

## Layout

```
e2e/
  auth.setup.ts      # Fires once before every project; logs in via API.
  shell.spec.ts      # /home + every primary tab loads.
  tasks.spec.ts      # Create + complete + archive.
  wall.spec.ts       # Publish + delete.
  dashboard.spec.ts  # Widget grid mounts.
  .auth/state.json   # Generated; .gitignored. Don't commit.
```

## Adding new tests

- Stay idempotent: any data the test creates must be removed by its end.
- Prefer role-based locators (`getByRole`, `getByPlaceholder`) over CSS
  classes — they survive Tailwind churn better.
- If a test needs admin-only privileges, document it; the test user must
  be `Admin` for now (we don't model multiple test identities yet).
