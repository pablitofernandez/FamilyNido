import { expect, test as setup } from '@playwright/test';

const STATE_PATH = 'e2e/.auth/state-b.json';

/**
 * Logs in tester B (the second seeded family member) and persists the cookie
 * to a separate storage-state file. Multi-user specs combine this state with
 * the primary one in `e2e/.auth/state.json` to drive two distinct browser
 * contexts in the same test.
 *
 * Reads `E2E_USER_B_EMAIL` and `E2E_USER_B_PASSWORD`; bails loudly if either
 * is missing so a misconfigured run fails fast.
 */
setup('authenticate user B via local credentials', async ({ request, baseURL }) => {
  const email = process.env['E2E_USER_B_EMAIL'];
  const password = process.env['E2E_USER_B_PASSWORD'];
  expect(email, 'E2E_USER_B_EMAIL must be set').toBeTruthy();
  expect(password, 'E2E_USER_B_PASSWORD must be set').toBeTruthy();

  const response = await request.post(`${baseURL}/api/auth/local/login`, {
    data: { email, password },
  });
  expect(response.status(), `local login should return 200, got ${response.status()}`).toBe(200);

  await request.storageState({ path: STATE_PATH });
});
