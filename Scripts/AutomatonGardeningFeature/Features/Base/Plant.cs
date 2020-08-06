using System;
using AtomicTorch.CBND.CoreMod.StaticObjects.Vegetation.Plants;
using AtomicTorch.CBND.GameApi.Data.World;

namespace CryoFall.Automaton.Features
{
    public class Plant
    {
        public readonly IStaticWorldObject PlantWorldObject;
        public readonly ProtoObjectPlant ProtoPlant;
        public bool IsWatered => PlantWorldObject.GetPublicState<PlantPublicState>().IsWatered;
        public bool IsSpoiled => PlantWorldObject.GetPublicState<PlantPublicState>().IsSpoiled;
        public bool HasHarvest => PlantWorldObject.GetPublicState<PlantPublicState>().HasHarvest;
        public bool IsFertilized => PlantWorldObject.GetPublicState<PlantPublicState>().IsFertilized;

        private Plant()
        {
        }

        public Plant(IStaticWorldObject plant)
        {
            if (!(plant.ProtoGameObject is ProtoObjectPlant protoObjectPlant)) throw new ArgumentException("Not valid plant object");
            PlantWorldObject = plant;
            ProtoPlant = protoObjectPlant;
        }

        public override string ToString() => $"{nameof(PlantWorldObject)}: {PlantWorldObject}, " +
                                             $"{nameof(IsWatered)}: {IsWatered}, " +
                                             $"{nameof(IsSpoiled)}: {IsSpoiled}, " +
                                             $"{nameof(HasHarvest)}: {HasHarvest}, " +
                                             $"{nameof(IsFertilized)}: {IsFertilized}";

        private bool Equals(Plant other) => Equals(PlantWorldObject.Id, other.PlantWorldObject.Id);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((Plant) obj);
        }

        public override int GetHashCode() => PlantWorldObject != null ? PlantWorldObject.GetHashCode() : 0;
    }
}