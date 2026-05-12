import { expect, test as setup } from '@playwright/test';

const STATE_PATH = 'e2e/.auth/state.json';

/**
 * Logs in once via the local-credentials endpoint and persists the resulting
 * cookie as Playwright "storage state". Every other spec project depends on
 * this setup and starts pre-authenticated.
 *
 * Reads E2E_USER_EMAIL and E2E_USER_PASSWORD; bails loudly if either is
 * missing so a misconfigured run fails fast instead of looping through
 * unauthenticated tests.
 */
setup('authenticate via local credentials', async ({ request, baseURL }) => {
  const email = process.env['E2E_USER_EMAIL'];
  const password = process.env['E2E_USER_PASSWORD'];
  expect(email, 'E2E_USER_EMAIL must be set').toBeTruthy();
  expect(password, 'E2E_USER_PASSWORD must be set').toBeTruthy();

  const response = await request.post(`${baseURL}/api/auth/local/login`, {
    data: { email, password },
  });
  expect(response.status(), `local login should return 200, got ${response.status()}`).toBe(200);

  // Persist the cookie + any localStorage; subsequent specs reuse this state.
  await request.storageState({ path: STATE_PATH });
});
