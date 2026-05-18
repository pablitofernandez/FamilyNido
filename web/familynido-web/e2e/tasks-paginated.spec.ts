import { APIRequestContext, expect, test } from '@playwright/test';

/**
 * Pagination + search coverage for the "Todas" tab.
 *
 * Each test seeds its own tasks via the API (using the authenticated context
 * cookie inherited from auth.setup.ts) with a unique prefix, so search
 * filters insulate us from any other tasks present in the dev database.
 * Cleanup is best-effort in afterEach: failed tests still leave seeded
 * rows behind, but the prefix keeps them clearly identifiable.
 */

type SeededTask = { id: string; title: string };

async function createTask(api: APIRequestContext, title: string): Promise<SeededTask> {
  const today = new Date().toISOString().slice(0, 10);
  const resp = await api.post('/api/household-tasks/', {
    data: {
      title,
      description: null,
      category: 'General',
      recurrence: 'None',
      weeklyDays: null,
      monthlyDay: null,
      timeOfDay: null,
      startDate: today,
      dueDate: null,
      responsibleMemberId: null,
      relatedMemberIds: [],
      isFloating: false,
      points: 1,
    },
  });
  expect(resp.status(), `POST /api/household-tasks should 201 (got ${resp.status()})`).toBe(201);
  const body = (await resp.json()) as { id: string };
  return { id: body.id, title };
}

async function deleteTask(api: APIRequestContext, id: string): Promise<void> {
  // Best-effort: ignore the result so a failing cleanup doesn't mask the
  // real test failure.
  await api.delete(`/api/household-tasks/${id}`);
}

test.describe('tasks "Todas" tab: pagination + search', () => {
  let seeded: SeededTask[] = [];
  let prefix = '';

  test.beforeEach(({}, testInfo) => {
    seeded = [];
    // Prefix is unique per test invocation so concurrent runs (or stale
    // data from prior runs) never collide.
    prefix = `E2E-${testInfo.title.replace(/\W+/g, '-')}-${Date.now()}`;
  });

  test.afterEach(async ({ request }) => {
    for (const t of seeded) {
      await deleteTask(request, t.id);
    }
  });

  test('paginates with pageSize=2 and updates the URL', async ({ page, request }) => {
    // 3 tasks → 2 pages with pageSize=2.
    for (let i = 1; i <= 3; i++) {
      seeded.push(await createTask(request, `${prefix} ${i}`));
    }

    await page.goto(`/tasks?tab=all&q=${encodeURIComponent(prefix)}&pageSize=2`);

    const rows = page.locator('section ul > li').filter({ hasText: prefix });
    await expect(rows).toHaveCount(2);

    // Next button on the pagination nav.
    await page.getByRole('button', { name: /página siguiente/i }).click();

    await expect(page).toHaveURL(/[?&]page=2(?:&|$)/);
    await expect(rows).toHaveCount(1);
  });

  test('search filter narrows the list and reflects q in the URL', async ({ page, request }) => {
    // One "match", one "noise" — both prefixed so the cleanup catches them.
    seeded.push(await createTask(request, `${prefix} match`));
    seeded.push(await createTask(request, `${prefix} other`));

    await page.goto(`/tasks?tab=all&q=${encodeURIComponent(prefix)}`);

    // Type a refinement into the search box and wait past the 300 ms debounce.
    const searchBox = page.getByRole('searchbox', { name: /buscar tareas/i });
    await searchBox.fill(`${prefix} match`);

    await expect(page).toHaveURL(new RegExp(`[?&]q=${encodeURIComponent(`${prefix} match`).replace(/\+/g, '\\+')}`));
    const rows = page.locator('section ul > li').filter({ hasText: prefix });
    await expect(rows).toHaveCount(1);
    await expect(rows.first()).toContainText('match');
  });

  test('refreshing preserves page and search from the URL', async ({ page, request }) => {
    for (let i = 1; i <= 3; i++) {
      seeded.push(await createTask(request, `${prefix} ${i}`));
    }

    await page.goto(`/tasks?tab=all&q=${encodeURIComponent(prefix)}&pageSize=2&page=2`);

    // Initial load: still on page 2.
    const rows = page.locator('section ul > li').filter({ hasText: prefix });
    await expect(rows).toHaveCount(1);

    // F5 — must still be on page 2 of the same search.
    await page.reload();
    await expect(page).toHaveURL(/[?&]page=2(?:&|$)/);
    await expect(rows).toHaveCount(1);
  });

  test('search with no matches shows the empty-results message', async ({ page }) => {
    const impossible = `${prefix}-zzz-not-going-to-exist`;
    await page.goto(`/tasks?tab=all&q=${encodeURIComponent(impossible)}`);

    await expect(page.getByText(/ningún resultado/i)).toBeVisible();
    // Pagination is hidden when totalPages <= 1.
    await expect(page.getByRole('button', { name: /página siguiente/i })).toHaveCount(0);
  });
});
