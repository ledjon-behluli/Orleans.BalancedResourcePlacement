using Orleans.Runtime;
using Orleans.Statistics;
using System.Diagnostics;
using System.Management;

namespace Orleans.BalancedResourcePlacement;

internal sealed class WindowsEnvironmentStatistics : IHostEnvironmentStatistics, ILifecycleObserver, IDisposable
{
    private readonly TimeSpan monitorPeriod;

    private readonly PerformanceCounter memoryCounter;
    private readonly PerformanceCounter cpuCounter;

    public long? TotalPhysicalMemory { get; private set; }
    public float? CpuUsage { get; private set; }
    public long? AvailableMemory { get; private set; }

    private CancellationTokenSource? cts;
    private Task? monitorTask;

    public WindowsEnvironmentStatistics(BalancedResourcePlacementOptions options)
    {
        monitorPeriod = options.ResourceStatisticsCollectionPeriod;
        memoryCounter = new("Memory", "Available Bytes");
        cpuCounter = new("Processor", "% Processor Time", "_Total");
    }

    public void Dispose()
    {
        if (cts != null && !cts.IsCancellationRequested)
        {
            cts.Cancel();
        }
    }

    public async Task OnStart(CancellationToken ct)
    {
        cts = new CancellationTokenSource();
        ct.Register(() => cts.Cancel());

        monitorTask = await Task.Factory.StartNew(
            () => Monitor(cts.Token),
            cts.Token,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.RunContinuationsAsynchronously,
            TaskScheduler.Default
        );
    }

    public async Task OnStop(CancellationToken ct)
    {
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
            try
            {
                if (monitorTask is null)
                {
                    return;
                }

                await monitorTask;
            }
            catch (TaskCanceledException) { }
        }
        catch (Exception) { }
    }

    private async Task Monitor(CancellationToken cancellationToken)
    {
        int i = 0;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException("Monitor task canceled");
            }

            try
            {
                CpuUsage = cpuCounter.NextValue();
                AvailableMemory = (long)memoryCounter.NextValue();

                if (i == 0)
                {
                    using ManagementObjectSearcher searcher = new("Select * From Win32_ComputerSystem");
                    foreach (ManagementObject mObject in searcher.Get().Cast<ManagementObject>())
                    {
                        double bytes = Convert.ToDouble(mObject["TotalPhysicalMemory"]);
                        TotalPhysicalMemory = (long)bytes;

                        break;
                    }
                }

                await Task.Delay(monitorPeriod, cancellationToken);
            }
            catch (Exception ex) when (ex is not TaskCanceledException)
            {
                await Task.Delay(3 * monitorPeriod, cancellationToken);
            }
         
            if (i < 2) i++;
        }
    }
}

internal sealed class WindowsEnvironmentStatisticsLifecycleAdapter :
    ILifecycleParticipant<ISiloLifecycle>, ILifecycleObserver
{
    private readonly WindowsEnvironmentStatistics statistics;

    public WindowsEnvironmentStatisticsLifecycleAdapter(WindowsEnvironmentStatistics statistics) 
        => this.statistics = statistics;

    public Task OnStart(CancellationToken ct) => statistics.OnStart(ct);
    public Task OnStop(CancellationToken ct) => statistics.OnStop(ct);

    public void Participate(ISiloLifecycle lifecycle) =>
        lifecycle.Subscribe(nameof(WindowsEnvironmentStatistics), ServiceLifecycleStage.RuntimeInitialize, this);
}