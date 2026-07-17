using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Auth.API.Services.Dokploy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Auth.Tests.Unit;

public class DokployAccessRequestServiceTests
{
    [Fact]
    public async Task Submit_then_Approve_merges_projects_and_capabilities()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "req@example.com");
        db.DokployUserLinks.Add(new DokployUserLink
        {
            UserId = user.Id,
            DokployUserId = "dok-1",
            LinkedEmail = "req@example.com",
            IsProvisioned = true,
            CanAccessToAPI = true,
        });
        db.DokployProjectGrants.Add(new DokployProjectGrant
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DokployProjectId = "already",
            ProjectName = "Already",
            GrantedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("assignPermissions"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") };
            if (req.RequestUri.AbsolutePath.Contains("user.all"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"id":"dok-1","email":"req@example.com"}]"""),
                };
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]") };
        });

        var api = CreateApi(handler);
        var acl = new DokployAclSyncService(
            db, api, Options.Create(EnabledOpts()), TimeProvider.System, NullLogger<DokployAclSyncService>.Instance);
        var provision = new DokployProvisionService(
            db, api, acl, Options.Create(EnabledOpts()), TimeProvider.System, NullLogger<DokployProvisionService>.Instance);
        var svc = new DokployAccessRequestService(
            db, provision, acl, Options.Create(EnabledOpts()), TimeProvider.System, NullLogger<DokployAccessRequestService>.Instance);

        var submit = await svc.SubmitAsync(
            user.Id,
            [("new-proj", "New")],
            new DokployCapabilityFlags { CanAccessToDocker = true },
            "please",
            dokployPasswordIfNeeded: null);
        Assert.True(submit.Ok, submit.Error);

        var adminId = Guid.NewGuid();
        var approve = await svc.ApproveAsync(submit.Request!.Id, adminId, "ok");
        Assert.True(approve.Ok, approve.Error);

        var grants = await db.DokployProjectGrants.Where(g => g.UserId == user.Id).Select(g => g.DokployProjectId).ToListAsync();
        Assert.Contains("already", grants);
        Assert.Contains("new-proj", grants);

        var link = await db.DokployUserLinks.SingleAsync(l => l.UserId == user.Id);
        Assert.True(link.CanAccessToAPI);
        Assert.True(link.CanAccessToDocker);

        var stored = await db.DokployAccessRequests.SingleAsync();
        Assert.Equal(DokployAccessRequestStatus.Approved, stored.Status);
    }

    [Fact]
    public async Task Approve_partial_only_merges_selected_projects_and_caps()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "partial@example.com");
        db.DokployUserLinks.Add(new DokployUserLink
        {
            UserId = user.Id,
            DokployUserId = "dok-p",
            LinkedEmail = "partial@example.com",
            IsProvisioned = true,
        });
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("assignPermissions"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") };
            if (req.RequestUri.AbsolutePath.Contains("user.all"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"id":"dok-p","email":"partial@example.com"}]"""),
                };
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]") };
        });

        var api = CreateApi(handler);
        var acl = new DokployAclSyncService(
            db, api, Options.Create(EnabledOpts()), TimeProvider.System, NullLogger<DokployAclSyncService>.Instance);
        var provision = new DokployProvisionService(
            db, api, acl, Options.Create(EnabledOpts()), TimeProvider.System, NullLogger<DokployProvisionService>.Instance);
        var svc = new DokployAccessRequestService(
            db, provision, acl, Options.Create(EnabledOpts()), TimeProvider.System, NullLogger<DokployAccessRequestService>.Instance);

        var submit = await svc.SubmitAsync(
            user.Id,
            [("keep", "Keep"), ("drop", "Drop")],
            new DokployCapabilityFlags { CanAccessToDocker = true, CanAccessToAPI = true },
            null,
            null);
        Assert.True(submit.Ok, submit.Error);

        var approve = await svc.ApproveAsync(
            submit.Request!.Id,
            Guid.NewGuid(),
            "kun Keep + Docker",
            approvedProjectIds: ["keep"],
            approvedCapabilities: new DokployCapabilityFlags { CanAccessToDocker = true, CanAccessToAPI = false });
        Assert.True(approve.Ok, approve.Error);

        var grants = await db.DokployProjectGrants.Where(g => g.UserId == user.Id).Select(g => g.DokployProjectId).ToListAsync();
        Assert.Contains("keep", grants);
        Assert.DoesNotContain("drop", grants);

        var link = await db.DokployUserLinks.SingleAsync(l => l.UserId == user.Id);
        Assert.True(link.CanAccessToDocker);
        Assert.False(link.CanAccessToAPI);

        var stored = await db.DokployAccessRequests
            .Include(r => r.Projects)
            .SingleAsync();
        Assert.Equal(DokployAccessRequestStatus.Approved, stored.Status);
        Assert.Single(stored.Projects);
        Assert.Equal("keep", stored.Projects.Single().DokployProjectId);
        Assert.True(stored.CanAccessToDocker);
        Assert.False(stored.CanAccessToAPI);
        Assert.StartsWith("Delvist godkendt.", stored.ReviewNote);
    }

    [Fact]
    public async Task Submit_rejects_second_pending()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "twice@example.com");
        db.DokployUserLinks.Add(new DokployUserLink
        {
            UserId = user.Id,
            DokployUserId = "dok-2",
            LinkedEmail = "twice@example.com",
            IsProvisioned = true,
        });
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("[]"),
        });
        var api = CreateApi(handler);
        var acl = new DokployAclSyncService(
            db, api, Options.Create(EnabledOpts()), TimeProvider.System, NullLogger<DokployAclSyncService>.Instance);
        var provision = new DokployProvisionService(
            db, api, acl, Options.Create(EnabledOpts()), TimeProvider.System, NullLogger<DokployProvisionService>.Instance);
        var svc = new DokployAccessRequestService(
            db, provision, acl, Options.Create(EnabledOpts()), TimeProvider.System, NullLogger<DokployAccessRequestService>.Instance);

        var first = await svc.SubmitAsync(
            user.Id, [("p1", "P1")], new DokployCapabilityFlags(), null, null);
        Assert.True(first.Ok, first.Error);

        var second = await svc.SubmitAsync(
            user.Id, [("p2", "P2")], new DokployCapabilityFlags(), null, null);
        Assert.False(second.Ok);
        Assert.Contains("afventende", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static DokployOptions EnabledOpts() => new() { Enabled = true, ApiKey = "k", MemberRole = "member" };

    private static DokployApiClient CreateApi(RecordingHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://deploy.example/api/") };
        return new DokployApiClient(http, Options.Create(EnabledOpts()), NullLogger<DokployApiClient>.Instance);
    }

    private static async Task<User> SeedUserAsync(AuthDbContext db, string email)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test",
            Email = email,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static AuthDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AuthDbContext(options);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
