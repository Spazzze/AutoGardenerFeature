using AtomicTorch.CBND.GameApi;

namespace CryoFall.Automaton.Features
{
    [NotPersistent]
    public enum GardeningJobType
    {
        WateringWithCan,
        WateringWithItem,
        Fertilizing,
        Undefined,
    }
}