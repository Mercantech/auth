using System.Net;
using System.Text;
using System.Text.Json;
using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Auth.API.Services.Dokploy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Auth.Tests.Unit;

public class DokployProvisionAndAclTests
{
    [Fact]
    public async Task TryProvision_skips_when_not_requested()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "a@example.com");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json"),
        });
        var api = CreateApi(handler, enabled: true);
        var svc = new DokployProvisionService(
            db,
            api,
            Options.Create(new DokployOptions { Enabled = true, ApiKey = "k" }),
            TimeProvider.System,
            NullLogger<DokployProvisionService>.Instance);

        await svc.TryProvisionIfRequestedAsync(user, wantDokploy: false, dokployPassword: null);

        Assert.Empty(handler.Requests);
        Assert.Empty(await db.DokployUserLinks.ToListAsync());
    }

    [Fact]
    public async Task TryProvision_links_existing_dokploy_user_by_email()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "match@example.com");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """[{"id":"dok-1","email":"match@example.com"}]""",
                Encoding.UTF8,
                "application/json"),
        });
        var api = CreateApi(handler, enabled: true);
        var svc = new DokployProvisionService(
            db,
            api,
            Options.Create(new DokployOptions { Enabled = true, ApiKey = "k", MemberRole = "member" }),
            TimeProvider.System,
            NullLogger<DokployProvisionService>.Instance);

        await svc.TryProvisionIfRequestedAsync(user, wantDokploy: true, dokployPassword: "password1");

        var link = Assert.Single(await db.DokployUserLinks.ToListAsync());
        Assert.True(link.IsProvisioned);
        Assert.Equal("dok-1", link.DokployUserId);
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Get && r.RequestUri!.AbsolutePath.Contains("user.all"));
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.AbsolutePath.Contains("inviteMember"));
    }

    [Fact]
    public async Task ProvisionAsync_creates_with_credentials_not_invite()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "new@example.com");
        var listCalls = 0;
        var handler = new RecordingHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("user.all"))
            {
                listCalls++;
                var body = listCalls == 1
                    ? "[]"
                    : """[{"id":"dok-new","user":{"id":"dok-new","email":"new@example.com"}}]""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
            }

            if (path.Contains("user.createUserWithCredentials"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var svc = new DokployProvisionService(
            db,
            CreateApi(handler, enabled: true),
            Options.Create(new DokployOptions { Enabled = true, ApiKey = "k", MemberRole = "member" }),
            TimeProvider.System,
            NullLogger<DokployProvisionService>.Instance);

        var result = await svc.ProvisionAsync(user.Id, "secretpass");

        Assert.Equal(DokployProvisionStatus.Created, result.Status);
        Assert.Equal("dok-new", result.DokployUserId);
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post
            && r.RequestUri!.AbsolutePath.Contains("createUserWithCredentials"));
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.AbsolutePath.Contains("inviteMember"));
    }

    [Fact]
    public void ParseUsers_prefers_nested_user_id_over_member_id()
    {
        using var doc = JsonDocument.Parse(
            """[{"id":"m1","role":"member","userId":"u1","user":{"id":"u1","email":"nested@example.com"},"accessedProjects":["p1"],"canAccessToDocker":true}]""");
        var users = DokployApiClient.ParseUsers(doc.RootElement);
        Assert.Single(users);
        Assert.Equal("u1", users[0].Id);
        Assert.Equal("u1", users[0].ResolvedUserId);
        Assert.Equal("m1", users[0].MemberId);
        Assert.Equal("nested@example.com", users[0].Email);
        Assert.Equal(["p1"], users[0].AccessedProjects);
        Assert.True(users[0].CanAccessToDocker);
    }

    [Fact]
    public async Task SaveGrantsAndPush_sets_dirty_then_clears_after_assignPermissions()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "acl@example.com");
        db.DokployUserLinks.Add(new DokployUserLink
        {
            UserId = user.Id,
            DokployUserId = "dok-acl",
            LinkedEmail = "acl@example.com",
            IsProvisioned = true,
            AclDirty = false,
        });
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("user.assignPermissions"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            if (req.RequestUri.AbsolutePath.Contains("user.all"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"id":"dok-acl","email":"acl@example.com"}]"""),
                };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
        });
        var api = CreateApi(handler, enabled: true);
        var sync = new DokployAclSyncService(
            db,
            api,
            Options.Create(new DokployOptions { Enabled = true, ApiKey = "k" }),
            TimeProvider.System,
            NullLogger<DokployAclSyncService>.Instance);

        await sync.SavePermissionsAndPushAsync(
            user.Id,
            [("proj-1", "Demo")],
            new DokployCapabilityFlags { CanAccessToDocker = true, CanCreateProjects = true });

        var link = await db.DokployUserLinks.SingleAsync();
        Assert.False(link.AclDirty);
        Assert.True(link.CanAccessToDocker);
        Assert.True(link.CanCreateProjects);
        Assert.NotNull(link.AclSyncedAtUtc);
        var grant = Assert.Single(await db.DokployProjectGrants.ToListAsync());
        Assert.Equal("proj-1", grant.DokployProjectId);

        var assign = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post
            && r.RequestUri!.AbsolutePath.Contains("user.assignPermissions"));
        var body = await assign.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("dok-acl", doc.RootElement.GetProperty("id").GetString());
        Assert.Equal("proj-1", doc.RootElement.GetProperty("accessedProjects")[0].GetString());
        Assert.True(doc.RootElement.GetProperty("canCreateProjects").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("canAccessToDocker").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("canDeleteProjects").GetBoolean());
    }

    [Fact]
    public async Task Reconcile_pulls_when_not_dirty()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "pull@example.com");
        db.DokployUserLinks.Add(new DokployUserLink
        {
            UserId = user.Id,
            DokployUserId = "dok-pull",
            LinkedEmail = "pull@example.com",
            IsProvisioned = true,
            AclDirty = false,
        });
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("project.allForPermissions"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"projectId":"p-remote","name":"Remote"}]"""),
                };
            if (path.Contains("user.all"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """[{"id":"mem-pull","userId":"dok-pull","email":"pull@example.com","accessedProjects":["p-remote"],"canAccessToAPI":true,"canCreateServices":true}]"""),
                };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
        });

        var sync = new DokployAclSyncService(
            db,
            CreateApi(handler, enabled: true),
            Options.Create(new DokployOptions { Enabled = true, ApiKey = "k" }),
            TimeProvider.System,
            NullLogger<DokployAclSyncService>.Instance);

        var result = await sync.ReconcileAsync();
        Assert.Equal(1, result.Pulled);
        var grant = Assert.Single(await db.DokployProjectGrants.ToListAsync());
        Assert.Equal("p-remote", grant.DokployProjectId);
        Assert.Equal("Remote", grant.ProjectName);
        var link = await db.DokployUserLinks.SingleAsync();
        Assert.True(link.CanAccessToAPI);
        Assert.True(link.CanCreateServices);
        Assert.False(link.CanCreateProjects);
    }

    [Fact]
    public void UnwrapArray_handles_trpc_wrapper()
    {
        using var doc = JsonDocument.Parse("""{"result":{"data":{"json":[{"id":"1","email":"x@y.z"}]}}}""");
        var users = DokployApiClient.UnwrapArray<DokployUserDto>(doc.RootElement);
        Assert.Single(users);
        Assert.Equal("1", users[0].Id);
    }

    [Fact]
    public async Task ProvisionAsync_returns_AlreadyProvisioned_when_linked()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "has@example.com");
        db.DokployUserLinks.Add(new DokployUserLink
        {
            UserId = user.Id,
            DokployUserId = "dok-existing",
            LinkedEmail = "has@example.com",
            IsProvisioned = true,
        });
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json"),
        });
        var svc = new DokployProvisionService(
            db,
            CreateApi(handler, enabled: true),
            Options.Create(new DokployOptions { Enabled = true, ApiKey = "k" }),
            TimeProvider.System,
            NullLogger<DokployProvisionService>.Instance);

        var result = await svc.ProvisionAsync(user.Id, "password1");

        Assert.Equal(DokployProvisionStatus.AlreadyProvisioned, result.Status);
        Assert.Equal("dok-existing", result.DokployUserId);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ProvisionAsync_returns_MissingEmail_without_email()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, email: null);
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json"),
        });
        var svc = new DokployProvisionService(
            db,
            CreateApi(handler, enabled: true),
            Options.Create(new DokployOptions { Enabled = true, ApiKey = "k" }),
            TimeProvider.System,
            NullLogger<DokployProvisionService>.Instance);

        var result = await svc.ProvisionAsync(user.Id, "password1");

        Assert.Equal(DokployProvisionStatus.MissingEmail, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ProvisionAsync_returns_InvalidPassword_when_too_short()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "x@example.com");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json"),
        });
        var svc = new DokployProvisionService(
            db,
            CreateApi(handler, enabled: true),
            Options.Create(new DokployOptions { Enabled = true, ApiKey = "k" }),
            TimeProvider.System,
            NullLogger<DokployProvisionService>.Instance);

        var result = await svc.ProvisionAsync(user.Id, "short");

        Assert.Equal(DokployProvisionStatus.InvalidPassword, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void ParseProjects_reads_name_and_projectId()
    {
        using var doc = JsonDocument.Parse(
            """[{"projectId":"LzhD8J5dpv75HvfnzuGy","name":"AuthZ"},{"id":"x","name":"Infra"}]""");
        var projects = DokployApiClient.ParseProjects(doc.RootElement);
        Assert.Equal(2, projects.Count);
        Assert.Equal("LzhD8J5dpv75HvfnzuGy", projects[0].ResolvedId);
        Assert.Equal("AuthZ", projects[0].Name);
        Assert.Equal("Infra", projects[1].Name);
    }

    private static DokployApiClient CreateApi(RecordingHandler handler, bool enabled)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://deploy.example/api/") };
        return new DokployApiClient(
            http,
            Options.Create(new DokployOptions { Enabled = enabled, ApiKey = "test-key", BaseUrl = "https://deploy.example/api" }),
            NullLogger<DokployApiClient>.Instance);
    }

    private static async Task<User> SeedUserAsync(AuthDbContext db, string? email)
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
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var clone = await CloneAsync(request);
            Requests.Add(clone);
            return respond(request);
        }

        private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            if (request.Content is not null)
            {
                var bytes = await request.Content.ReadAsByteArrayAsync();
                clone.Content = new ByteArrayContent(bytes);
                foreach (var h in request.Content.Headers)
                    clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }

            return clone;
        }
    }
}
