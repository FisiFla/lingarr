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
