using AtomicTorch.CBND.GameApi.Scripting;
using CryoFall.Automaton.Features;

namespace CryoFall.Automaton
{
    public class GardeningFeatureBootstrapperClient : BaseBootstrapper
    {
        public override void ClientInitialize()
        {
            AutomatonManager.AddFeature(FeatureAutoGardening.Instance);
        }
    }
}