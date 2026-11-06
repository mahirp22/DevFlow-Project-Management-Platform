using DevFlow.Application.Attachments;
namespace DevFlow.Api.Attachments;

public sealed class AttachmentCleanup(IServiceScopeFactory scopes, ILogger<AttachmentCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay startup so migration/startup diagnostics stay independent of S3 availability.
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(2));
                var count = await scope.ServiceProvider.GetRequiredService<IAttachmentService>().Cleanup(timeout.Token);
                if (count > 0) logger.LogInformation("Cleaned {Count} abandoned attachment objects", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogWarning("Attachment cleanup failed with {ExceptionType}", exception.GetType().Name); }
        }
    }
}
