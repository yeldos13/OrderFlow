namespace OrderFlow.WarehouseStub.Chaos;

public sealed class ChaosState(ChaosSettings initial)
{
    private ChaosSettings _current = initial;

    public ChaosSettings Current => Volatile.Read(ref _current);

    public void Update(ChaosSettings settings) => Volatile.Write(ref _current, settings);
}
