using AtomicTorch.CBND.GameApi.Data.Items;
using AtomicTorch.GameEngine.Common.Primitives;

namespace CryoFall.Automaton.Items.Generic.Base
{
    public interface IProtoAutoUsableItem
    {
        public void ClientItemUseFinish(IItem item, Vector2Ushort tilePosition);
    }
}