# Screenshots

This folder hosts the images embedded in the main [`README.md`](../../README.md).
Drop the following PNGs in here and the README will render them automatically.

## Recommended capture flow

To avoid screenshotting real family data, use the **demo seed** built into the
API (see the *"Seed a curated demo family"* section in the root README). It
drops a fictitious Smith family with tasks, a wall thread, meal plan, school
schedule and adults' weekly agenda — everything you need to fill the three
captures below without touching personal data. Reset between attempts with
`docker compose -f deploy/docker-compose.dev.yml down -v`.

| File                     | Used in section            | Suggested capture                                                       |
| ------------------------ | -------------------------- | ----------------------------------------------------------------------- |
| `hero-dashboard.png`     | Lead (before Highlights)   | The Home dashboard at desktop width with a few widgets populated.       |
| `tablet-mode.png`        | 📺 Tablet mode             | The full-screen rotating tablet view, ideally landscape 16:9.           |
| `wall.png`               | 💬 Wall                    | A wall post with a reaction or comment so the social bits are visible.  |
| `meals.png`              | 🍽️ Meals                   | The weekly meal-plan grid with at least one full week filled in.        |

## Capture tips

- **Width**: aim for 1600–1920 px wide on desktop captures, 1280 px wide on
  the tablet view. GitHub will downscale gracefully.
- **Format**: PNG with the browser chrome cropped out. JPEG only if the
  capture has photo backgrounds and PNG is heavy.
- **Privacy**: scrub any real family data before committing — use the
  `FamilyNido E2E` seed accounts or invent obviously-fake names.
- **Light/dark**: pick whichever feels more polished; mixing both in the same
  README is fine if you label them.

Files in this folder that are not referenced from the README can be removed
without affecting anything.
