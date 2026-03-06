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
