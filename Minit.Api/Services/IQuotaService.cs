using Minit.Api.Domain;

namespace Minit.Api.Services;

public interface IQuotaService
{
    Task<MonthlyUsage> GetOrCreateMonthlyUsageAsync(Guid userId, int monthYyyyMm, CancellationToken cancellationToken = default);
    Task<int> CheckRemainingSecondsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task ApplyBillingAsync(Guid userId, int billedSeconds, DateTime utcNow, CancellationToken cancellationToken = default);
}
