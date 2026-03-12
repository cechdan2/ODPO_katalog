using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhotoApp.Data;
using PhotoApp.Models;
using PhotoApp.Services;

namespace PhotoApp.Tests.Services;

public class EfUserServiceTests
{
    [Fact]
    public void ValidateCredentials_ReturnsTrueAndUserForMatchingPassword()
    {
        using var context = CreateContext();
        var service = new EfUserService(context);

        var isValid = service.ValidateCredentials("TestUser", "ValidPass123!", out var user);

        Assert.True(isValid);
        Assert.NotNull(user);
        Assert.Equal("TestUser", user.UserName);
    }

    [Fact]
    public void ValidateCredentials_ReturnsFalseForWrongPassword()
    {
        using var context = CreateContext();
        var service = new EfUserService(context);

        var isValid = service.ValidateCredentials("TestUser", "WrongPass123!", out _);

        Assert.False(isValid);
    }

    [Fact]
    public void FindByName_IsCaseInsensitive()
    {
        using var context = CreateContext();
        var service = new EfUserService(context);

        var user = service.FindByName("testuser");

        Assert.NotNull(user);
        Assert.Equal("TestUser", user.UserName);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var user = new CustomUser
        {
            UserName = "TestUser"
        };

        user.PasswordHash = new PasswordHasher<CustomUser>()
            .HashPassword(user, "ValidPass123!");

        context.Users.Add(user);
        context.SaveChanges();

        return context;
    }
}
