using System.Net;
using System.Text.RegularExpressions;
using Auth.API.Data;
using Auth.API.Models;
using Auth.API.Models.Entities;
using Auth.API.Security;
using Auth.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(nameof(AuthIntegrationCollection))]
public class EmailAccountTests(AuthIntegrationFixture fixture)
{
    [Fact]
    public async Task Confirm_email_token_sets_EmailConfirmed()
    {
        var userId = Guid.NewGuid();
        string plainToken;

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<IUserActionTokenService>();
            var now = DateTime.UtcNow;

            var user = new User
            {
                Id = userId,
                DisplayName = "Confirm Test",
                Email = "confirm@example.test",
                EmailConfirmed = false,
                CreatedAt = now,
                LastLoginAt = now,
                LastLoginMethod = MercantecAuthClaims.LoginMethodValues.Password,
            };
            db.Users.Add(user);
            db.LocalLogins.Add(new LocalLogin
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Email = user.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                CreatedAt = now,
            });
            await db.SaveChangesAsync();

            plainToken = await tokenService.IssueAsync(
                userId,
                UserActionTokenPurpose.EmailConfirmation,
                TimeSpan.FromHours(1));
        }

        var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var res = await client.GetAsync($"/account/confirm-email?token={Uri.EscapeDataString(plainToken)}");

        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("confirm_ok", res.Headers.Location?.ToString(), StringComparison.Ordinal);

        await using var verifyScope = fixture.Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var confirmed = await verifyDb.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.EmailConfirmed)
            .SingleAsync();
        Assert.True(confirmed);
    }

    [Fact]
    public async Task Password_reset_token_updates_hash()
    {
        var userId = Guid.NewGuid();
        const string oldPassword = "password123";
        const string newPassword = "newpassword99";
        string plainToken;

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<IUserActionTokenService>();
            var now = DateTime.UtcNow;

            var user = new User
            {
                Id = userId,
                DisplayName = "Reset Test",
                Email = "reset@example.test",
                EmailConfirmed = true,
                CreatedAt = now,
                LastLoginAt = now,
                LastLoginMethod = MercantecAuthClaims.LoginMethodValues.Password,
            };
            db.Users.Add(user);
            db.LocalLogins.Add(new LocalLogin
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Email = user.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(oldPassword),
                CreatedAt = now,
            });
            await db.SaveChangesAsync();

            plainToken = await tokenService.IssueAsync(
                userId,
                UserActionTokenPurpose.PasswordReset,
                TimeSpan.FromMinutes(30));
        }

        var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var registerPage = await client.GetStringAsync("/Account/ResetPassword?token=" + Uri.EscapeDataString(plainToken));
        var tokenMatch = Regex.Match(registerPage, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(tokenMatch.Success, "Antiforgery-token mangler på ResetPassword-siden.");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenMatch.Groups[1].Value,
            ["token"] = plainToken,
            ["password"] = newPassword,
            ["confirmPassword"] = newPassword,
        });
        var res = await client.PostAsync("/account/password/reset", form);

        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("password_reset_ok", res.Headers.Location?.ToString(), StringComparison.Ordinal);

        await using var verifyScope = fixture.Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var hash = await verifyDb.LocalLogins.AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => l.PasswordHash)
            .SingleAsync();
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, hash));
        Assert.False(BCrypt.Net.BCrypt.Verify(oldPassword, hash));
    }
}
