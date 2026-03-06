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
