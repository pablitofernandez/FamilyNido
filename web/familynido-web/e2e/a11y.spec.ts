import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/**
 * Accessibility baseline using axe-core. Walks every primary authenticated
 * route plus the anonymous login screen and runs the WCAG 2.1 A + AA rule
 * sets through axe.
 *
 * **Current bar: no `critical` violations.** That's the line a11y reviewers
 * treat as "blocks shipping" — missing labels, broken focus traps, etc.
 * `serious` is logged but not gated yet because the brand palette
 * (terra `#c96442`, ink-3 `#8a7862`) doesn't meet 4.5:1 against the cream
 * backgrounds — fixing every contrast hit means redesigning the palette,
 * which is a separate, larger conversation. The colour-contrast rule is
 * specifically excluded from blocking until that lands.
 */

const ROUTES: { path: string; label: string }[] = [
  { path: '/es/login', label: 'login (anon)' },
  { path: '/home', label: 'dashboard' },
  { path: '/tasks', label: 'tasks' },
  { path: '/calendar', label: 'calendar' },
  { path: '/wall', label: 'wall' },
  { path: '/meals', label: 'meals' },
  { path: '/nido', label: 'nido' },
  { path: '/account', label: 'account' },
  { path: '/health', label: 'health' },
  { path: '/school', label: 'school' },
];

async function audit(page: Page, path: string, label: string): Promise<void> {
  await page.goto(path);
  // Give the SPA a moment to settle. Most components are signal-driven
  // and render synchronously, but a few features (calendar, dashboard
  // widgets) fetch data on init.
  await page.waitForLoadState('networkidle');

  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  const blocking = results.violations.filter((v) => v.impact === 'critical');
  const known = results.violations.filter((v) => v.impact === 'serious');
  const lighter = results.violations.filter(
    (v) => v.impact === 'moderate' || v.impact === 'minor',
  );

  const summary = [
    `[a11y] ${label} (${path})`,
    `  critical (gate): ${blocking.length}`,
    `  serious (known): ${known.length}`,
    `  moderate+minor:  ${lighter.length}`,
  ];
  for (const v of blocking) {
    summary.push(`  ✖ [${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`);
  }
  for (const v of known.slice(0, 5)) {
    summary.push(`  · [${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`);
  }
  // eslint-disable-next-line no-console
  console.log(summary.join('\n'));

  // Compact list (id only) to keep the assertion failure readable when it
  // does fire — the full diff with axe nodes is overwhelming.
  const blockingIds = blocking.map((v) => v.id);
  expect(blockingIds, `Critical a11y violations on ${label}`).toEqual([]);
}

for (const { path, label } of ROUTES) {
  test(`a11y: ${label}`, async ({ page }) => {
    await audit(page, path, label);
  });
}
