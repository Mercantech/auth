using Auth.API.Models;
using Auth.API.Models.Entities;
using Auth.API.Services;

namespace Auth.Tests.Unit;

public class ClientRequiredLinkEvaluatorTests
{
    private static User UserWith(params (string provider, UserEmailKind? kind)[] links)
    {
        var user = new User { Id = Guid.NewGuid(), DisplayName = "Test" };
        foreach (var (provider, kind) in links)
        {
            user.ExternalLogins.Add(new ExternalLogin
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = provider,
                ProviderUserId = Guid.NewGuid().ToString(),
                LinkedAt = DateTime.UtcNow,
            });

            if (kind is not null)
            {
                user.LinkedEmails.Add(new UserEmail
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    NormalizedEmail = $"{provider}@example.com",
                    Kind = kind.Value,
                    LinkedAt = DateTime.UtcNow,
                });
            }
        }

        return user;
    }

    [Fact]
    public void UserHasLinkedProvider_google_requires_external_login()
    {
        var user = UserWith(("google", UserEmailKind.Personal));
        Assert.True(ClientRequiredLinkEvaluator.UserHasLinkedProvider(user, "google"));
        Assert.False(ClientRequiredLinkEvaluator.UserHasLinkedProvider(UserWith(), "google"));
    }

    [Fact]
    public void UserHasLinkedProvider_microsoft_work_requires_work_email()
    {
        var ok = UserWith(("microsoft", UserEmailKind.Work));
        var onlySchool = UserWith(("microsoft", UserEmailKind.School));

        Assert.True(ClientRequiredLinkEvaluator.UserHasLinkedProvider(ok, "microsoft"));
        Assert.False(ClientRequiredLinkEvaluator.UserHasLinkedProvider(onlySchool, "microsoft"));
    }

    [Fact]
    public void UserHasLinkedProvider_microsoft_edu_accepts_edu_provider_or_school_email()
    {
        var eduProvider = UserWith(("microsoft-edu", null));
        var legacySchool = UserWith(("microsoft", UserEmailKind.School));

        Assert.True(ClientRequiredLinkEvaluator.UserHasLinkedProvider(eduProvider, "microsoft_edu"));
        Assert.True(ClientRequiredLinkEvaluator.UserHasLinkedProvider(legacySchool, "microsoft_edu"));
    }

    [Fact]
    public void GetMissing_lists_unsatisfied_requirements()
    {
        var user = UserWith(("google", UserEmailKind.Personal));
        var missing = ClientRequiredLinkEvaluator.GetMissing(user, ["google", "microsoft"]);
        Assert.Equal(["microsoft"], missing);
    }
}
