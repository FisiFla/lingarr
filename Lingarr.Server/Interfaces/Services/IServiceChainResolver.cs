using Lingarr.Server.Interfaces.Services.Translation;

namespace Lingarr.Server.Interfaces.Services;

public interface IServiceChainResolver
{
    (ITranslationService service, string serviceType)? ResolveNext(
        List<string> chain,
        HashSet<string>? skipServices = null);
}
