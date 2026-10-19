namespace OrderFlow.Consumer.IntegrationTests.Infrastructure;

public static class Eventually
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public static async Task<T> GetAsync<T>(Func<Task<T>> probe, Func<T, bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);

        while (true)
        {
            var value = await probe();
            if (condition(value) || DateTime.UtcNow >= deadline)
            {
                return value;
            }

            await Task.Delay(PollInterval);
        }
    }

    public static Task<T> GetAsync<T>(Func<T> probe, Func<T, bool> condition, TimeSpan? timeout = null) =>
        GetAsync(() => Task.FromResult(probe()), condition, timeout);
}
