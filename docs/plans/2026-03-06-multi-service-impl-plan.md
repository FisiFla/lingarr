# Multi-Service Translation Fallback Chain — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the single translation service setting with an ordered fallback chain of services, each with optional monthly character quotas.

**Architecture:** New `ServiceQuota` entity + DB table. `ServiceQuotaTracker` singleton caches quotas in memory, flushes to DB periodically. `ServiceChainResolver` walks the chain to find the first service that isn't over-quota. `TranslationJob` wraps translation in a retry loop that falls through the chain on errors. Frontend replaces the service dropdown with a sortable list.

**Tech Stack:** C# / ASP.NET Core / FluentMigrator / Hangfire / xUnit + Moq / Vue 3 + Pinia + TypeScript

---

### Task 1: ServiceQuota Entity and Migration

**Files:**
- Create: `Lingarr.Core/Entities/ServiceQuota.cs`
- Modify: `Lingarr.Core/Data/LingarrDbContext.cs`
- Modify: `Lingarr.Core/Configuration/SettingKeys.cs`
- Create: `Lingarr.Migrations/Migrations/M0008_AddServiceQuotasTable.cs`

**Step 1: Create the ServiceQuota entity**

```csharp
// Lingarr.Core/Entities/ServiceQuota.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lingarr.Core.Entities;

[Table("service_quotas")]
public class ServiceQuota
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty;

    public long? MonthlyLimitChars { get; set; }

    public long CharsUsed { get; set; }

    public int ResetMonth { get; set; }
}
```

**Step 2: Add DbSet to LingarrDbContext**

In `Lingarr.Core/Data/LingarrDbContext.cs`, add after the `Users` DbSet:

```csharp
public DbSet<ServiceQuota> ServiceQuotas { get; set; }
```

**Step 3: Add SettingKeys.Translation.ServiceChain**

In `Lingarr.Core/Configuration/SettingKeys.cs`, add inside the `Translation` class after `ServiceType`:

```csharp
public const string ServiceChain = "service_chain";
```

**Step 4: Create the FluentMigrator migration**

```csharp
// Lingarr.Migrations/Migrations/M0008_AddServiceQuotasTable.cs
using FluentMigrator;

namespace Lingarr.Migrations.Migrations;

[Migration(8)]
public class M0008_AddServiceQuotasTable : Migration
{
    public override void Up()
    {
        Create.Table("service_quotas")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("service_type").AsCustom("VARCHAR(50)").NotNullable().Unique()
            .WithColumn("monthly_limit_chars").AsInt64().Nullable()
            .WithColumn("chars_used").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("reset_month").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Table("service_quotas");
    }
}
```

**Step 5: Commit**

```bash
git add Lingarr.Core/Entities/ServiceQuota.cs Lingarr.Core/Data/LingarrDbContext.cs Lingarr.Core/Configuration/SettingKeys.cs Lingarr.Migrations/Migrations/M0008_AddServiceQuotasTable.cs
git commit -m "feat: add ServiceQuota entity, migration, and ServiceChain setting key"
```

---

### Task 2: ServiceQuotaTracker (Singleton with In-Memory Cache)

**Files:**
- Create: `Lingarr.Server/Interfaces/Services/IServiceQuotaTracker.cs`
- Create: `Lingarr.Server/Services/ServiceQuotaTracker.cs`

**Step 1: Create the interface**

```csharp
// Lingarr.Server/Interfaces/Services/IServiceQuotaTracker.cs
namespace Lingarr.Server.Interfaces.Services;

public interface IServiceQuotaTracker
{
    Task LoadAsync();
    bool IsOverQuota(string serviceType);
    void RecordUsage(string serviceType, long chars);
    Task FlushAsync();
    Task SetQuota(string serviceType, long? monthlyLimitChars);
    Dictionary<string, (long used, long? limit)> GetAllUsage();
}
```

**Step 2: Implement ServiceQuotaTracker**

```csharp
// Lingarr.Server/Services/ServiceQuotaTracker.cs
using System.Collections.Concurrent;
using Lingarr.Core.Data;
using Lingarr.Core.Entities;
using Lingarr.Server.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lingarr.Server.Services;

public class ServiceQuotaTracker : IServiceQuotaTracker, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceQuotaTracker> _logger;
    private readonly ConcurrentDictionary<string, QuotaEntry> _quotas = new();
    private readonly Timer _flushTimer;
    private int _jobsSinceFlush;
    private const int FlushEveryNJobs = 50;

    private class QuotaEntry
    {
        public long MonthlyLimitChars { get; set; } = -1; // -1 = unlimited
        public long CharsUsed;
        public int ResetMonth;
    }

    public ServiceQuotaTracker(
        IServiceScopeFactory scopeFactory,
        ILogger<ServiceQuotaTracker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _flushTimer = new Timer(_ => _ = FlushAsync(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LingarrDbContext>();
        var quotas = await db.ServiceQuotas.ToListAsync();

        var currentMonth = DateTime.UtcNow.Month;
        foreach (var q in quotas)
        {
            var entry = new QuotaEntry
            {
                MonthlyLimitChars = q.MonthlyLimitChars ?? -1,
                CharsUsed = q.CharsUsed,
                ResetMonth = q.ResetMonth
            };

            if (entry.ResetMonth != currentMonth)
            {
                entry.CharsUsed = 0;
                entry.ResetMonth = currentMonth;
            }

            _quotas[q.ServiceType] = entry;
        }

        _logger.LogInformation("Loaded {Count} service quotas from database", quotas.Count);
    }

    public bool IsOverQuota(string serviceType)
    {
        if (!_quotas.TryGetValue(serviceType, out var entry))
            return false; // no quota configured = unlimited

        if (entry.MonthlyLimitChars < 0)
            return false; // unlimited

        return Interlocked.Read(ref entry.CharsUsed) >= entry.MonthlyLimitChars;
    }

    public void RecordUsage(string serviceType, long chars)
    {
        var entry = _quotas.GetOrAdd(serviceType, _ => new QuotaEntry
        {
            ResetMonth = DateTime.UtcNow.Month
        });

        Interlocked.Add(ref entry.CharsUsed, chars);

        if (Interlocked.Increment(ref _jobsSinceFlush) >= FlushEveryNJobs)
        {
            Interlocked.Exchange(ref _jobsSinceFlush, 0);
            _ = FlushAsync();
        }
    }

    public async Task FlushAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LingarrDbContext>();

            foreach (var (serviceType, entry) in _quotas)
            {
                var existing = await db.ServiceQuotas
                    .FirstOrDefaultAsync(q => q.ServiceType == serviceType);

                if (existing != null)
                {
                    existing.CharsUsed = Interlocked.Read(ref entry.CharsUsed);
                    existing.ResetMonth = entry.ResetMonth;
                    existing.MonthlyLimitChars = entry.MonthlyLimitChars < 0 ? null : entry.MonthlyLimitChars;
                }
                else
                {
                    db.ServiceQuotas.Add(new ServiceQuota
                    {
                        ServiceType = serviceType,
                        CharsUsed = Interlocked.Read(ref entry.CharsUsed),
                        ResetMonth = entry.ResetMonth,
                        MonthlyLimitChars = entry.MonthlyLimitChars < 0 ? null : entry.MonthlyLimitChars
                    });
                }
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush service quotas to database");
        }
    }

    public async Task SetQuota(string serviceType, long? monthlyLimitChars)
    {
        var entry = _quotas.GetOrAdd(serviceType, _ => new QuotaEntry
        {
            ResetMonth = DateTime.UtcNow.Month
        });
        entry.MonthlyLimitChars = monthlyLimitChars ?? -1;
        await FlushAsync();
    }

    public Dictionary<string, (long used, long? limit)> GetAllUsage()
    {
        var result = new Dictionary<string, (long used, long? limit)>();
        foreach (var (serviceType, entry) in _quotas)
        {
            result[serviceType] = (
                Interlocked.Read(ref entry.CharsUsed),
                entry.MonthlyLimitChars < 0 ? null : entry.MonthlyLimitChars
            );
        }
        return result;
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        FlushAsync().GetAwaiter().GetResult();
    }
}
```

**Step 3: Commit**

```bash
git add Lingarr.Server/Interfaces/Services/IServiceQuotaTracker.cs Lingarr.Server/Services/ServiceQuotaTracker.cs
git commit -m "feat: add ServiceQuotaTracker with in-memory cache and periodic DB flush"
```

---

### Task 3: ServiceChainResolver

**Files:**
- Create: `Lingarr.Server/Interfaces/Services/IServiceChainResolver.cs`
- Create: `Lingarr.Server/Services/ServiceChainResolver.cs`

**Step 1: Create the interface**

```csharp
// Lingarr.Server/Interfaces/Services/IServiceChainResolver.cs
using Lingarr.Server.Interfaces.Services.Translation;

namespace Lingarr.Server.Interfaces.Services;

public interface IServiceChainResolver
{
    (ITranslationService service, string serviceType)? ResolveNext(
        List<string> chain,
        HashSet<string>? skipServices = null);
}
```

**Step 2: Implement ServiceChainResolver**

```csharp
// Lingarr.Server/Services/ServiceChainResolver.cs
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Interfaces.Services.Translation;
using Microsoft.Extensions.Logging;

namespace Lingarr.Server.Services;

public class ServiceChainResolver : IServiceChainResolver
{
    private readonly ITranslationServiceFactory _factory;
    private readonly IServiceQuotaTracker _quotaTracker;
    private readonly ILogger<ServiceChainResolver> _logger;

    public ServiceChainResolver(
        ITranslationServiceFactory factory,
        IServiceQuotaTracker quotaTracker,
        ILogger<ServiceChainResolver> logger)
    {
        _factory = factory;
        _quotaTracker = quotaTracker;
        _logger = logger;
    }

    public (ITranslationService service, string serviceType)? ResolveNext(
        List<string> chain,
        HashSet<string>? skipServices = null)
    {
        foreach (var serviceType in chain)
        {
            if (skipServices != null && skipServices.Contains(serviceType))
                continue;

            if (_quotaTracker.IsOverQuota(serviceType))
            {
                _logger.LogInformation("Service {ServiceType} is over quota, skipping", serviceType);
                continue;
            }

            try
            {
                var service = _factory.CreateTranslationService(serviceType);
                return (service, serviceType);
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Unknown service type {ServiceType} in chain, skipping", serviceType);
            }
        }

        return null;
    }
}
```

**Step 3: Commit**

```bash
git add Lingarr.Server/Interfaces/Services/IServiceChainResolver.cs Lingarr.Server/Services/ServiceChainResolver.cs
git commit -m "feat: add ServiceChainResolver for fallback chain traversal"
```

---

### Task 4: Register Services and Load Quotas on Startup

**Files:**
- Modify: `Lingarr.Server/Extensions/ServiceCollectionExtensions.cs`
- Modify: `Lingarr.Server/Services/StartupService.cs`

**Step 1: Register new services in DI**

In `Lingarr.Server/Extensions/ServiceCollectionExtensions.cs`, inside `ConfigureServices()`, add after the `ITranslationServiceFactory` registration:

```csharp
builder.Services.AddSingleton<IServiceQuotaTracker, ServiceQuotaTracker>();
builder.Services.AddScoped<IServiceChainResolver, ServiceChainResolver>();
```

**Step 2: Load quotas on startup and migrate service_type → service_chain**

In `Lingarr.Server/Services/StartupService.cs`, find where it initializes settings. Add after the existing setting migration logic:

```csharp
// Load quota cache
var quotaTracker = scope.ServiceProvider.GetRequiredService<IServiceQuotaTracker>();
await quotaTracker.LoadAsync();

// Migrate service_type to service_chain if needed
var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();
var serviceChain = await settingService.GetSetting(SettingKeys.Translation.ServiceChain);
if (string.IsNullOrEmpty(serviceChain))
{
    var legacyServiceType = await settingService.GetSetting(SettingKeys.Translation.ServiceType);
    if (!string.IsNullOrEmpty(legacyServiceType))
    {
        var chain = System.Text.Json.JsonSerializer.Serialize(new[] { legacyServiceType });
        await settingService.SetSetting(SettingKeys.Translation.ServiceChain, chain);
        _logger.LogInformation("Migrated service_type '{ServiceType}' to service_chain", legacyServiceType);
    }
}
```

Note: Read the full `StartupService.cs` to find the exact insertion point. It should be near the end of the `ExecuteAsync` or `StartAsync` method, after settings are validated.

**Step 3: Commit**

```bash
git add Lingarr.Server/Extensions/ServiceCollectionExtensions.cs Lingarr.Server/Services/StartupService.cs
git commit -m "feat: register service chain DI, load quotas and migrate settings on startup"
```

---

### Task 5: Modify TranslationJob for Chain + Fallback

**Files:**
- Modify: `Lingarr.Server/Jobs/TranslationJob.cs`

**Step 1: Add IServiceChainResolver and IServiceQuotaTracker to constructor**

Add to constructor parameters:

```csharp
private readonly IServiceChainResolver _serviceChainResolver;
private readonly IServiceQuotaTracker _quotaTracker;
```

Inject via constructor (add parameters and assignments).

**Step 2: Replace the single-service translation block in Execute()**

Find these lines in `Execute()` (around line 97-165):

```csharp
var serviceType = settings[SettingKeys.Translation.ServiceType];
// ... through to the translatedSubtitles assignment
```

Replace with the chain-aware version:

```csharp
// Read service chain (fall back to legacy single service)
var chainJson = await _settings.GetSetting(SettingKeys.Translation.ServiceChain);
List<string> chain;
if (!string.IsNullOrEmpty(chainJson))
{
    chain = System.Text.Json.JsonSerializer.Deserialize<List<string>>(chainJson) ?? new();
}
else
{
    var legacyType = settings[SettingKeys.Translation.ServiceType] ?? "google";
    chain = new List<string> { legacyType };
}

var failedServices = new HashSet<string>();
ITranslationService? translationService = null;
string serviceType = "";
List<SubtitleItem>? translatedSubtitles = null;

while (translatedSubtitles == null)
{
    var resolved = _serviceChainResolver.ResolveNext(chain, failedServices);
    if (resolved == null)
    {
        throw new TranslationException(
            $"All services in the chain exhausted. Tried: {string.Join(", ", failedServices)}");
    }

    (translationService, serviceType) = resolved.Value;

    try
    {
        var translator = new SubtitleTranslationService(translationService, _logger, _progressService);
        var subtitles = await _subtitleService.ReadSubtitles(request.SubtitleToTranslate);

        if (settings[SettingKeys.Translation.UseBatchTranslation] == "true"
            && translationService is IBatchTranslationService _)
        {
            var maxSize = int.TryParse(settings[SettingKeys.Translation.MaxBatchSize],
                out var batchSize) ? batchSize : 10000;

            _logger.LogInformation(
                "Using batch translation with {ServiceType}, max batch size: {maxBatchSize}",
                serviceType, maxSize);

            translatedSubtitles = await translator.TranslateSubtitlesBatch(
                subtitles, translationRequest, stripSubtitleFormatting,
                maxSize, cancellationToken);
        }
        else
        {
            if (contextPromptEnabled)
            {
                _logger.LogInformation(
                    "Using {ServiceType} with context (before: {contextBefore}, after: {contextAfter})",
                    serviceType, contextBefore, contextAfter);
            }

            translatedSubtitles = await translator.TranslateSubtitles(
                subtitles, request, stripSubtitleFormatting,
                contextBefore, contextAfter, cancellationToken);
        }

        // Record character usage for quota tracking
        var totalChars = translatedSubtitles.Sum(s => (long)(s.OriginalText?.Length ?? 0));
        _quotaTracker.RecordUsage(serviceType, totalChars);
    }
    catch (Exception ex) when (ex is not TaskCanceledException and not OperationCanceledException)
    {
        failedServices.Add(serviceType);
        _logger.LogWarning(ex, "Translation failed with {ServiceType}, trying next in chain", serviceType);
    }
}
```

Note: The `subtitles` variable (`ReadSubtitles`) should be read once before the loop if it doesn't need to change per service. Move it above the while loop.

**Step 3: Commit**

```bash
git add Lingarr.Server/Jobs/TranslationJob.cs
git commit -m "feat: TranslationJob walks service chain with fallback on error/quota"
```

---

### Task 6: ServiceChain API Controller

**Files:**
- Create: `Lingarr.Server/Controllers/ServiceChainController.cs`

**Step 1: Create the controller**

```csharp
// Lingarr.Server/Controllers/ServiceChainController.cs
using System.Text.Json;
using Lingarr.Core.Configuration;
using Lingarr.Server.Attributes;
using Lingarr.Server.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lingarr.Server.Controllers;

[ApiController]
[LingarrAuthorize]
[Route("api/service-chain")]
public class ServiceChainController : ControllerBase
{
    private readonly ISettingService _settingService;
    private readonly IServiceQuotaTracker _quotaTracker;

    public ServiceChainController(
        ISettingService settingService,
        IServiceQuotaTracker quotaTracker)
    {
        _settingService = settingService;
        _quotaTracker = quotaTracker;
    }

    public record ServiceChainItem(string ServiceType, long? MonthlyLimitChars, long CharsUsed);

    [HttpGet]
    public async Task<ActionResult<List<ServiceChainItem>>> Get()
    {
        var chainJson = await _settingService.GetSetting(SettingKeys.Translation.ServiceChain);
        var chain = string.IsNullOrEmpty(chainJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(chainJson) ?? new();

        var usage = _quotaTracker.GetAllUsage();

        var result = chain.Select(serviceType =>
        {
            var (used, limit) = usage.GetValueOrDefault(serviceType, (0, null));
            return new ServiceChainItem(serviceType, limit, used);
        }).ToList();

        return Ok(result);
    }

    public record UpdateServiceChainRequest(List<ServiceChainEntry> Services);
    public record ServiceChainEntry(string ServiceType, long? MonthlyLimitChars);

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateServiceChainRequest request)
    {
        var chain = request.Services.Select(s => s.ServiceType).ToList();
        await _settingService.SetSetting(
            SettingKeys.Translation.ServiceChain,
            JsonSerializer.Serialize(chain));

        // Also update legacy service_type to the first in chain for backwards compat
        if (chain.Count > 0)
        {
            await _settingService.SetSetting(SettingKeys.Translation.ServiceType, chain[0]);
        }

        foreach (var entry in request.Services)
        {
            await _quotaTracker.SetQuota(entry.ServiceType, entry.MonthlyLimitChars);
        }

        return Ok(new { message = "Service chain updated" });
    }

    [HttpGet("usage")]
    public ActionResult<Dictionary<string, object>> GetUsage()
    {
        var usage = _quotaTracker.GetAllUsage();
        var result = usage.ToDictionary(
            kv => kv.Key,
            kv => (object)new { used = kv.Value.used, limit = kv.Value.limit });
        return Ok(result);
    }
}
```

**Step 2: Commit**

```bash
git add Lingarr.Server/Controllers/ServiceChainController.cs
git commit -m "feat: add ServiceChain API controller for chain CRUD and usage"
```

---

### Task 7: Frontend — Service Chain Settings Component

**Files:**
- Modify: `Lingarr.Client/src/ts/setting.ts` — add `SERVICE_CHAIN` to SETTINGS
- Create: `Lingarr.Client/src/services/serviceChainService.ts`
- Create: `Lingarr.Client/src/components/features/settings/ServiceChainSettings.vue`
- Modify: `Lingarr.Client/src/components/features/settings/ServicesSettings.vue`

**Step 1: Add setting constant**

In `Lingarr.Client/src/ts/setting.ts`, add to the `SETTINGS` object:

```typescript
SERVICE_CHAIN: 'service_chain',
```

Add to `ISettings`:

```typescript
service_chain: string
```

**Step 2: Create the API service**

```typescript
// Lingarr.Client/src/services/serviceChainService.ts
import axios from 'axios'

export interface ServiceChainItem {
    serviceType: string
    monthlyLimitChars: number | null
    charsUsed: number
}

export interface ServiceChainEntry {
    serviceType: string
    monthlyLimitChars: number | null
}

export const serviceChainService = {
    async getChain(): Promise<ServiceChainItem[]> {
        const response = await axios.get<ServiceChainItem[]>('/api/service-chain')
        return response.data
    },

    async updateChain(services: ServiceChainEntry[]): Promise<void> {
        await axios.put('/api/service-chain', { services })
    },

    async getUsage(): Promise<Record<string, { used: number; limit: number | null }>> {
        const response = await axios.get('/api/service-chain/usage')
        return response.data
    }
}
```

**Step 3: Create the ServiceChainSettings component**

```vue
<!-- Lingarr.Client/src/components/features/settings/ServiceChainSettings.vue -->
<template>
    <div class="flex flex-col space-y-3">
        <span class="font-semibold">Service priority (drag to reorder):</span>

        <!-- Active chain -->
        <div class="flex flex-col space-y-1">
            <div
                v-for="(item, index) in activeServices"
                :key="item.serviceType"
                draggable="true"
                class="flex items-center justify-between rounded border border-accent/30 bg-accent/10 p-2 cursor-move"
                @dragstart="dragStart(index)"
                @dragover.prevent="dragOver(index)"
                @drop="drop(index)">
                <div class="flex items-center space-x-2">
                    <span class="text-sm font-medium">{{ index + 1 }}.</span>
                    <span>{{ getLabel(item.serviceType) }}</span>
                </div>
                <div class="flex items-center space-x-2">
                    <input
                        type="number"
                        :value="item.monthlyLimitChars"
                        :placeholder="'unlimited'"
                        class="w-32 rounded border border-accent/30 bg-secondary px-2 py-1 text-sm"
                        @change="updateLimit(index, ($event.target as HTMLInputElement).value)" />
                    <span class="text-xs opacity-60">chars/mo</span>
                    <div
                        v-if="item.monthlyLimitChars"
                        class="h-1.5 w-20 rounded-full bg-accent/20 overflow-hidden">
                        <div
                            class="h-full rounded-full transition-all"
                            :class="usagePercent(item) > 90 ? 'bg-red-500' : 'bg-green-500'"
                            :style="{ width: Math.min(usagePercent(item), 100) + '%' }">
                        </div>
                    </div>
                    <span v-if="item.monthlyLimitChars" class="text-xs opacity-60">
                        {{ formatChars(item.charsUsed) }}/{{ formatChars(item.monthlyLimitChars) }}
                    </span>
                    <button
                        class="ml-2 text-red-400 hover:text-red-300 text-sm"
                        @click="removeService(index)">
                        Remove
                    </button>
                </div>
            </div>
        </div>

        <!-- Available services -->
        <div v-if="availableServices.length > 0" class="flex flex-col space-y-1">
            <span class="text-sm opacity-60">Available services:</span>
            <div class="flex flex-wrap gap-2">
                <button
                    v-for="service in availableServices"
                    :key="service"
                    class="rounded border border-accent/30 px-3 py-1 text-sm hover:bg-accent/20"
                    @click="addService(service)">
                    + {{ getLabel(service) }}
                </button>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { SERVICE_TYPE } from '@/ts'
import { serviceChainService, type ServiceChainItem, type ServiceChainEntry } from '@/services/serviceChainService'

const emit = defineEmits<{ save: [] }>()

const activeServices = ref<ServiceChainItem[]>([])
const dragIndex = ref<number | null>(null)

const allServiceTypes = Object.values(SERVICE_TYPE)
const serviceLabels: Record<string, string> = {
    anthropic: 'Anthropic',
    bing: 'Bing',
    deepl: 'DeepL',
    deepseek: 'DeepSeek',
    gemini: 'Gemini',
    google: 'Google',
    libretranslate: 'LibreTranslate',
    localai: 'Local AI',
    microsoft: 'Microsoft',
    openai: 'OpenAI',
    yandex: 'Yandex'
}

const availableServices = computed(() =>
    allServiceTypes.filter(
        (st) => !activeServices.value.some((a) => a.serviceType === st)
    )
)

function getLabel(serviceType: string): string {
    return serviceLabels[serviceType] ?? serviceType
}

function usagePercent(item: ServiceChainItem): number {
    if (!item.monthlyLimitChars || item.monthlyLimitChars === 0) return 0
    return (item.charsUsed / item.monthlyLimitChars) * 100
}

function formatChars(chars: number): string {
    if (chars >= 1_000_000) return (chars / 1_000_000).toFixed(1) + 'M'
    if (chars >= 1_000) return (chars / 1_000).toFixed(0) + 'K'
    return chars.toString()
}

function dragStart(index: number) { dragIndex.value = index }

function dragOver(index: number) {
    if (dragIndex.value === null || dragIndex.value === index) return
    const item = activeServices.value.splice(dragIndex.value, 1)[0]
    activeServices.value.splice(index, 0, item)
    dragIndex.value = index
}

async function drop(_index: number) {
    dragIndex.value = null
    await saveChain()
}

function updateLimit(index: number, value: string) {
    const num = parseInt(value)
    activeServices.value[index].monthlyLimitChars = isNaN(num) || num <= 0 ? null : num
    saveChain()
}

function addService(serviceType: string) {
    activeServices.value.push({ serviceType, monthlyLimitChars: null, charsUsed: 0 })
    saveChain()
}

function removeService(index: number) {
    activeServices.value.splice(index, 1)
    saveChain()
}

async function saveChain() {
    const entries: ServiceChainEntry[] = activeServices.value.map((s) => ({
        serviceType: s.serviceType,
        monthlyLimitChars: s.monthlyLimitChars
    }))
    await serviceChainService.updateChain(entries)
    emit('save')
}

onMounted(async () => {
    activeServices.value = await serviceChainService.getChain()
})
</script>
```

**Step 4: Replace dropdown in ServicesSettings.vue**

In `Lingarr.Client/src/components/features/settings/ServicesSettings.vue`:

Replace the `<SelectComponent v-model:selected="serviceType" :options="serviceOptions" />` section with:

```vue
<ServiceChainSettings @save="saveNotification?.show()" />
```

Add the import:

```typescript
import ServiceChainSettings from '@/components/features/settings/ServiceChainSettings.vue'
```

Remove the now-unused `serviceType` computed, `serviceOptions` array, and `serviceConfigComponent` computed. Remove the `SelectComponent` import.

Keep the per-service config components — they should still be shown. Update the `serviceConfigComponent` to show config for all services currently in the chain, or show them always below the chain list. Simplest approach: show all per-service config sections for services in the active chain.

Replace the single `<component :is="serviceConfigComponent" .../>` block with:

```vue
<div v-for="service in chainServiceTypes" :key="service">
    <component
        :is="getServiceConfig(service)"
        v-if="getServiceConfig(service)"
        @save="saveNotification?.show()" />
</div>
```

With script additions:

```typescript
const chainServiceTypes = ref<string[]>([])

// populated from ServiceChainSettings via a shared ref or by watching the setting store
// simplest: read from the API on mount
onMounted(async () => {
    const chain = await serviceChainService.getChain()
    chainServiceTypes.value = chain.map(s => s.serviceType)
})

function getServiceConfig(serviceType: string) {
    switch (serviceType) {
        case SERVICE_TYPE.LIBRETRANSLATE: return LibreTranslateConfig
        case SERVICE_TYPE.OPENAI: return OpenAiConfig
        case SERVICE_TYPE.ANTHROPIC: return AnthropicConfig
        case SERVICE_TYPE.LOCALAI: return LocalAiConfig
        case SERVICE_TYPE.DEEPL: return DeepLConfig
        case SERVICE_TYPE.GEMINI: return GeminiConfig
        case SERVICE_TYPE.DEEPSEEK: return DeepSeekConfig
        default: return null
    }
}
```

**Step 5: Commit**

```bash
git add Lingarr.Client/src/ts/setting.ts Lingarr.Client/src/services/serviceChainService.ts Lingarr.Client/src/components/features/settings/ServiceChainSettings.vue Lingarr.Client/src/components/features/settings/ServicesSettings.vue
git commit -m "feat: replace service dropdown with sortable chain UI and quota display"
```

---

### Task 8: Tests

**Files:**
- Create: `Lingarr.Server.Tests/Services/ServiceChainResolverTests.cs`

**Step 1: Write tests for ServiceChainResolver**

```csharp
// Lingarr.Server.Tests/Services/ServiceChainResolverTests.cs
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Interfaces.Services.Translation;
using Lingarr.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lingarr.Server.Tests.Services;

public class ServiceChainResolverTests
{
    private readonly Mock<ITranslationServiceFactory> _factoryMock = new();
    private readonly Mock<IServiceQuotaTracker> _quotaMock = new();
    private readonly Mock<ILogger<ServiceChainResolver>> _loggerMock = new();

    private ServiceChainResolver CreateResolver() =>
        new(_factoryMock.Object, _quotaMock.Object, _loggerMock.Object);

    [Fact]
    public void ResolveNext_ReturnsFirstAvailableService()
    {
        var mockService = new Mock<ITranslationService>().Object;
        _factoryMock.Setup(f => f.CreateTranslationService("google")).Returns(mockService);
        _quotaMock.Setup(q => q.IsOverQuota(It.IsAny<string>())).Returns(false);

        var result = CreateResolver().ResolveNext(new List<string> { "google", "deepl" });

        Assert.NotNull(result);
        Assert.Equal("google", result.Value.serviceType);
    }

    [Fact]
    public void ResolveNext_SkipsOverQuotaService()
    {
        var mockService = new Mock<ITranslationService>().Object;
        _quotaMock.Setup(q => q.IsOverQuota("deepl")).Returns(true);
        _quotaMock.Setup(q => q.IsOverQuota("google")).Returns(false);
        _factoryMock.Setup(f => f.CreateTranslationService("google")).Returns(mockService);

        var result = CreateResolver().ResolveNext(new List<string> { "deepl", "google" });

        Assert.NotNull(result);
        Assert.Equal("google", result.Value.serviceType);
    }

    [Fact]
    public void ResolveNext_SkipsExplicitlySkippedServices()
    {
        var mockService = new Mock<ITranslationService>().Object;
        _quotaMock.Setup(q => q.IsOverQuota(It.IsAny<string>())).Returns(false);
        _factoryMock.Setup(f => f.CreateTranslationService("bing")).Returns(mockService);

        var result = CreateResolver().ResolveNext(
            new List<string> { "google", "bing" },
            new HashSet<string> { "google" });

        Assert.NotNull(result);
        Assert.Equal("bing", result.Value.serviceType);
    }

    [Fact]
    public void ResolveNext_ReturnsNullWhenAllExhausted()
    {
        _quotaMock.Setup(q => q.IsOverQuota(It.IsAny<string>())).Returns(true);

        var result = CreateResolver().ResolveNext(new List<string> { "deepl", "google" });

        Assert.Null(result);
    }

    [Fact]
    public void ResolveNext_SkipsUnknownServiceType()
    {
        var mockService = new Mock<ITranslationService>().Object;
        _quotaMock.Setup(q => q.IsOverQuota(It.IsAny<string>())).Returns(false);
        _factoryMock.Setup(f => f.CreateTranslationService("nonexistent"))
            .Throws(new ArgumentException("Unsupported"));
        _factoryMock.Setup(f => f.CreateTranslationService("google")).Returns(mockService);

        var result = CreateResolver().ResolveNext(new List<string> { "nonexistent", "google" });

        Assert.NotNull(result);
        Assert.Equal("google", result.Value.serviceType);
    }
}
```

**Step 2: Run tests**

```bash
cd /tmp/lingarr-fork && dotnet test Lingarr.Server.Tests --filter "ServiceChainResolver" -v normal
```

Expected: All 5 tests pass.

**Step 3: Commit**

```bash
git add Lingarr.Server.Tests/Services/ServiceChainResolverTests.cs
git commit -m "test: add ServiceChainResolver unit tests"
```

---

### Task 9: Push and Build

**Step 1: Push all changes**

```bash
cd /tmp/lingarr-fork && git push origin main
```

**Step 2: Trigger Docker build**

```bash
gh workflow run docker-build.yml --repo FisiFla/lingarr --ref main
```

**Step 3: Wait for build, then pull on NAS**

```bash
# After build completes:
ssh Leenion@192.168.0.216 "cd /volume1/docker/ugreen-docker-setup && sudo docker compose pull lingarr && sudo docker compose up -d lingarr"
```

**Step 4: Commit plan**

```bash
git add docs/plans/2026-03-06-multi-service-impl-plan.md
git commit -m "docs: add multi-service translation implementation plan"
```
