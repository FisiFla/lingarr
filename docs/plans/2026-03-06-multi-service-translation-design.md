# Multi-Service Translation with Fallback Chain

## Summary

Replace the single global translation service setting with an ordered chain of services. Jobs walk the chain until one succeeds. Each service can have an optional monthly character quota. Fallback triggers on both quota exhaustion and runtime errors.

## Data Model

### New DB table: `service_quotas`

| Column | Type | Description |
|---|---|---|
| `id` | int PK | Auto-increment |
| `service_type` | string (unique) | e.g., "deepl", "google" |
| `monthly_limit_chars` | long? | null = unlimited |
| `chars_used` | long | Current month usage |
| `reset_month` | int | Month number for auto-reset |

### Setting change

`service_type` (single string) is replaced by `service_chain` (JSON array of ordered service type strings).

Example: `["deepl", "google", "bing"]`

## Backend

### ServiceChainResolver (new)

Interface: `IServiceChainResolver`
- `Task<(ITranslationService service, string serviceType)?> ResolveNext(List<string> chain, string? skipServiceType = null)`
- Iterates the chain in order, checks quota via `ServiceQuotaTracker`, returns the first eligible service
- Returns null if all services are exhausted

### ServiceQuotaTracker (new, singleton)

- Loads `service_quotas` from DB on startup into a `ConcurrentDictionary`
- `bool IsOverQuota(string serviceType)` — checks in-memory count against limit
- `void RecordUsage(string serviceType, long chars)` — increments in-memory counter
- Flushes to DB every 50 jobs or 5 minutes (whichever first) via a background timer
- Auto-resets counters when the calendar month changes (compares `reset_month` to `DateTime.UtcNow.Month`)

### TranslationJob changes

`TranslationJob.Execute()` modified flow:
1. Read `service_chain` setting (JSON array) instead of `service_type`
2. Call `ServiceChainResolver.ResolveNext(chain)` to get first eligible service
3. Attempt translation
4. On success: record char usage via `ServiceQuotaTracker`, proceed as normal
5. On `TranslationException`: log warning, call `ResolveNext(chain, skipServiceType: failedService)` to try next
6. If chain exhausted: job fails as today

### Migration

New FluentMigrator migration creates `service_quotas` table. On first startup, `StartupService` reads existing `service_type` setting, converts to `service_chain` JSON array with that single service. No quota row created (unlimited by default).

## Frontend

### Settings > Translation Service

Replace the current single dropdown with:
- **Sortable list** of enabled services (drag to reorder = priority)
- Each item shows: service name, optional "Monthly limit" input (characters), usage bar (current/limit)
- Toggle to enable/disable each service
- Disabled/available services shown below the active list, can be added with a click
- Reordering and toggling saves the `service_chain` setting
- Quota input saves/updates the `service_quotas` table via a new API endpoint

### New API endpoints

- `GET /api/service-chain` — returns ordered chain with quota info and current usage
- `PUT /api/service-chain` — updates chain order and quota limits
- `GET /api/service-chain/usage` — returns current month usage per service (for usage bars)

## What stays the same

- Per-service configuration (API keys, models, endpoints) in their existing settings sections
- `TranslationFactory` — still creates services by type string, unchanged
- `MAX_CONCURRENT_JOBS` — workers use the chain independently
- Statistics tracking — already records which service was used per job
- `AutomatedTranslationJob` — unchanged, it queues `TranslationJob` which handles the chain

## Risks and mitigations

- **In-memory quota drift on crash**: mitigated by periodic DB flush (5 min). Worst case: over-count by ~5 min of translations after a crash. Acceptable for monthly quotas.
- **Concurrent workers reading stale quota**: `ConcurrentDictionary` with atomic increments. Slightly over-quota is acceptable (few extra translations before the next worker sees the update).
