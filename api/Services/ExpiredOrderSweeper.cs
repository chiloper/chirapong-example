using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain;

namespace Stock.Api.Services;

/// <summary>Periodically expires Pending orders whose reservation window has passed and releases their stock.</summary>
public class ExpiredOrderSweeper(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<ExpiredOrderSweeper> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, config.GetValue("Orders:SweepIntervalSeconds", 60)));
        using var timer = new PeriodicTimer(interval);

        while (await WaitAsync(timer, stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Expired order sweep failed");
            }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        List<long> ids;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            ids = await db.Orders.AsNoTracking()
                .Where(o => o.Status == OrderStatus.Pending && o.ExpiresAt <= now)
                .OrderBy(o => o.ExpiresAt)
                .Select(o => o.Id)
                .Take(100)
                .ToListAsync(ct);
        }

        foreach (var id in ids)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var orders = scope.ServiceProvider.GetRequiredService<OrderService>();
                if (await orders.ReleasePendingAsync(id, OrderStatus.Expired, ct))
                    log.LogInformation("Order {OrderId} expired and its stock was released", id);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to expire order {OrderId}", id);
            }
        }
    }
}
