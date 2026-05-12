# Integrations — FamilyNido public API

FamilyNido exposes a small set of versioned endpoints under `/api/v1/**`
so any external integration (an automation runner, n8n, IFTTT, a cron
job on another machine…) can create tasks in the family's dashboard.

Authentication is by **API key**, minted from the FamilyNido admin UI
(*My account → API keys for integrations*, admins only). The key is
sent with every request:

```
X-Api-Key: bxn_xxxxxxxxxxxxxxxxxxxxxxxx
```

or, alternatively, as `Authorization: Bearer bxn_…`.

## Available endpoints

### `POST /api/v1/tasks` — create a task

Body (JSON):

| Field | Type | Required | Default | Notes |
| - | - | - | - | - |
| `title` | string | yes | — | Up to 200 characters. |
| `category` | string | no | `"General"` | Up to 50 characters. |
| `points` | int | no | `5` | Range 0–100. |
| `dueDate` | string `YYYY-MM-DD` | no | today | Ignored when `isFloating=true`. |
| `isFloating` | bool | no | `false` | Floating task — has no fixed date and stays pending every day until someone marks it done. |
| `responsibleMemberId` | guid | no | — | Must belong to the family the token was issued for. |
| `deduplicate` | bool | no | `false` | When `true` and a pending task (not archived, no completions) with the same `title` already exists, returns `200 OK` with `created=false` instead of inserting a new row. |

Response `201 Created` (or `200 OK` when `deduplicate` matches an
existing pending task):

```json
{
  "created": true,
  "reason": null,
  "taskId": "01938efc-...",
  "title": "Empty the dishwasher"
}
```

`reason` is `"already-pending"` on a dedup hit.

Common errors:

- `400 Bad Request`: validation failed (empty title, `points` out of
  range, `responsibleMemberId` outside the family).
- `401 Unauthorized`: header missing, malformed or revoked.
- `429 Too Many Requests`: rate limit exceeded (60 req/min/IP).

### Example: `curl`

```bash
curl -X POST https://your-host/api/v1/tasks \
  -H "X-Api-Key: bxn_xxxxxxxx…" \
  -H "Content-Type: application/json" \
  -d '{"title":"Buy bread","category":"Shopping","points":1}'
```

## Concrete guides

If you wire up an integration worth sharing, open a PR against this
folder with a short worked example — that kind of doc pays for itself.
