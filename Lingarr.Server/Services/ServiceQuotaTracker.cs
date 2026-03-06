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
