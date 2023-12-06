using Bogus;
using Microsoft.Extensions.Options;
using Orleans.BalancedResourcePlacement;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Statistics;
using System.Reflection;

var kalmanFilter = new KalmanFilter();

for (int i = 0; i < 50; i++)
{
    var simulatedStats = new Faker<SiloRuntimeStatistics>()
        .CustomInstantiator(CreateSiloRuntimeStatistics)
        .Generate();

    var filteredStats = kalmanFilter.Update(simulatedStats);

    Console.WriteLine($"Iteration {i + 1}:");
    Console.WriteLine($"Simulated Data: {simulatedStats}");
    Console.WriteLine($"Filtered Data: {filteredStats}");
    Console.WriteLine();

    Thread.Sleep(100); // Simulating a delay between measurements
}

Console.ReadKey();

static SiloRuntimeStatistics CreateSiloRuntimeStatistics(Faker faker)
{
    var constructorInfo = typeof(SiloRuntimeStatistics)
        .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
        .FirstOrDefault();

    if (constructorInfo != null)
    {
        var appStats = new FakeAppEnvironmentStatistics(faker);
        var hostStats = new FakeHostEnvironmentStatistics(faker, appStats.MemoryUsage);

        var parameters = new object[]
        {
            0,
            0,
            appStats ,
            hostStats,
            new OptionsWrapper<LoadSheddingOptions>(new LoadSheddingOptions() { LoadSheddingEnabled = false }),
            DateTime.UtcNow
        };

        var a = (SiloRuntimeStatistics)constructorInfo.Invoke(parameters);
        return a;
    }

    throw new InvalidOperationException("Could not find the internal constructor of SiloRuntimeStatistics");
}

class FakeAppEnvironmentStatistics(Faker faker) : IAppEnvironmentStatistics
{
    public long? MemoryUsage { get; } = faker.Random.Long(0, Constants.PhysicalMemory);
}

class FakeHostEnvironmentStatistics(Faker faker, long? memUsage) : IHostEnvironmentStatistics
{
    public long? TotalPhysicalMemory { get; } = Constants.PhysicalMemory;
    public float? CpuUsage { get; } = faker.Random.Float(1, 99);
    public long? AvailableMemory { get; } = Constants.PhysicalMemory - memUsage;
}

class Constants
{
    public const long PhysicalMemory = (long)16 * 1024 * 1024 * 1024;
}