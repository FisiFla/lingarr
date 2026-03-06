using System;
using Lingarr.Core.Data;
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lingarr.Server.Tests.Services;

public class AuthServiceTests
{
    [Fact]
    public void VerifyPassword_WithCurrentPbkdf2Hash_ReturnsTrueAndNoRehashRequired()
    {
        var service = CreateService();
        var password = "Password123!";
        var hash = service.HashPassword(password);

        var valid = service.VerifyPassword(password, hash);

        Assert.True(valid);
        Assert.False(service.NeedsPasswordRehash(hash));
    }

    [Fact]
    public void VerifyPassword_WithLegacyBcryptHash_ReturnsTrueAndRehashRequired()
    {
        var service = CreateService();
        var password = "Password123!";
        var legacyHash = BCrypt.Net.BCrypt.HashPassword(password);

        var valid = service.VerifyPassword(password, legacyHash);

        Assert.True(valid);
        Assert.True(service.NeedsPasswordRehash(legacyHash));
    }

    [Fact]
    public void VerifyPassword_WithInvalidHashFormat_ReturnsFalse()
    {
        var service = CreateService();

        var valid = service.VerifyPassword("Password123!", "not-a-valid-hash");

        Assert.False(valid);
    }

    private static AuthService CreateService()
    {
        var options = new DbContextOptionsBuilder<LingarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new LingarrDbContext(options);
        var settingService = new Mock<ISettingService>();
        var logger = Mock.Of<ILogger<AuthService>>();

        return new AuthService(context, settingService.Object, logger);
    }
}
