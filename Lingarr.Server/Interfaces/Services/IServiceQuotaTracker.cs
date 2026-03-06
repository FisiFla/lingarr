namespace Lingarr.Server.Interfaces.Services;

public interface IServiceQuotaTracker
{
    Task LoadAsync();
    bool IsOverQuota(string serviceType);
    void RecordUsage(string serviceType, long chars);
    Task FlushAsync();
    Task SetQuota(string serviceType, long? monthlyLimitChars);
    Dictionary<string, (long used, long? limit)> GetAllUsage();
}
