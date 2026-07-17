using Auth.API.Options;
using Microsoft.Extensions.Options;

namespace Auth.API.Services.Dokploy;

public sealed class DokployAclSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<DokployOptions> options,
    ILogger<DokployAclSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = options.Value;
            if (!opts.Enabled)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                continue;
            }

            var interval = Math.Max(1, opts.AclSyncIntervalMinutes);
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IDokployAclSyncService>();
                var result = await sync.ReconcileAsync(stoppingToken);
                logger.LogInformation(
                    "Dokploy ACL-sync: linked={Linked} pushed={Pushed} pulled={Pulled} errors={Errors}",
                    result.LinkedUsers,
                    result.Pushed,
                    result.Pulled,
                    result.Errors);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Dokploy ACL baggrundssync fejlede");
            }

            await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken);
        }
    }
}
