using Auth.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<UserEmail> UserEmails => Set<UserEmail>();
    public DbSet<LocalLogin> LocalLogins => Set<LocalLogin>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ClientApp> ClientApps => Set<ClientApp>();
    public DbSet<ClientAppRedirectUri> ClientAppRedirectUris => Set<ClientAppRedirectUri>();
    public DbSet<AuthorizationCode> AuthorizationCodes => Set<AuthorizationCode>();
    public DbSet<AuthUsageEvent> AuthUsageEvents => Set<AuthUsageEvent>();
    public DbSet<UserClientUsage> UserClientUsages => Set<UserClientUsage>();
    public DbSet<UserTotpMfa> UserTotpMfas => Set<UserTotpMfa>();
    public DbSet<UserMfaRecoveryCode> UserMfaRecoveryCodes => Set<UserMfaRecoveryCode>();
    public DbSet<UserPasskeyCredential> UserPasskeyCredentials => Set<UserPasskeyCredential>();
    public DbSet<UserActionToken> UserActionTokens => Set<UserActionToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.CreatedViaClientId);
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<LocalLogin>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.User).WithOne(u => u.LocalLogin).HasForeignKey<LocalLogin>(x => x.UserId);
        });

        modelBuilder.Entity<ExternalLogin>(e =>
        {
            e.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.ExternalLogins).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<UserEmail>(e =>
        {
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.HasIndex(x => new { x.UserId, x.Kind }).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.LinkedEmails).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash);
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<ClientApp>(e =>
        {
            e.HasIndex(x => x.ClientId).IsUnique();
            e.HasMany(x => x.RedirectUris).WithOne(r => r.ClientApp).HasForeignKey(r => r.ClientAppId);
        });

        modelBuilder.Entity<AuthorizationCode>(e =>
        {
            e.HasIndex(x => x.CodeHash);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<AuthUsageEvent>(e =>
        {
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.ClientId, x.CreatedAtUtc });
            e.HasIndex(x => x.EventType);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserClientUsage>(e =>
        {
            e.HasKey(x => new { x.UserId, x.ClientId });
            e.HasIndex(x => x.ClientId);
            e.HasIndex(x => x.LastSeenAtUtc);
            e.HasOne(x => x.User).WithMany(u => u.ClientUsages).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<UserTotpMfa>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasOne(x => x.User).WithOne(u => u.TotpMfa).HasForeignKey<UserTotpMfa>(x => x.UserId);
        });

        modelBuilder.Entity<UserMfaRecoveryCode>(e =>
        {
            e.HasIndex(x => x.CodeHash);
            e.HasOne(x => x.Totp).WithMany(t => t.RecoveryCodes).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<UserPasskeyCredential>(e =>
        {
            e.HasIndex(x => x.CredentialId).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.PasskeyCredentials).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<UserActionToken>(e =>
        {
            e.HasIndex(x => x.TokenHash);
            e.HasIndex(x => new { x.UserId, x.Purpose, x.ConsumedAtUtc });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });
    }
}
