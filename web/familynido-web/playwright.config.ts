import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright config for the FamilyNido E2E suite.
 *
 * The tests target a *running* deployment of the app — they do not start
 * the API or the database themselves. The simplest way to use them is:
 *
 *   1. Have docker compose dev (or prod) running locally.
 *   2. Have a `FamilyMember` linked to a user whose `Local` credential
 *      has a known password set via /account -> "Establecer contraseña".
 *   3. Export the four env vars below before running `npm run e2e`.
 *
 *     E2E_BASE_URL        — e.g. http://localhost:5173 or http://familynido.local
 *     E2E_USER_EMAIL      — email of the primary test user (tester A)
 *     E2E_USER_PASSWORD   — its local password
 *     E2E_USER_B_EMAIL    — email of the secondary test user (tester B), only
 *                           needed for *.b.spec.ts multi-user specs
 *     E2E_USER_B_PASSWORD — its local password
 *
 * See e2e/README.md for the full setup notes.
 */
const baseURL = process.env['E2E_BASE_URL'] ?? 'http://localhost:5173';

export default defineConfig({
  testDir: './e2e',
  // Storage state produced by auth.setup.ts so each spec starts authenticated.
  fullyParallel: false,
  forbidOnly: !!process.env['CI'],
  retries: 0,
  workers: 1,
  reporter: process.env['CI'] ? 'github' : [['list']],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  // The "setup" project authenticates once per user and writes a storage-state
  // file; every other project depends on it and reuses the cookie so tests
  // don't re-login on every spec. Multi-user specs (`*.b.spec.ts`) drive two
  // contexts in the same test via the `pageB` fixture in `e2e/fixtures.ts`.
  projects: [
    {
      name: 'setup',
      testMatch: /.*\.setup\.ts$/,
    },
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        storageState: 'e2e/.auth/state.json',
      },
      dependencies: ['setup'],
    },
  ],
});
