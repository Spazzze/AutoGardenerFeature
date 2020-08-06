using System.Collections.Generic;
using System.Linq;
using AtomicTorch.CBND.CoreMod.Characters.Player;
using AtomicTorch.CBND.CoreMod.Items.Generic;
using AtomicTorch.CBND.CoreMod.Items.Seeds;
using AtomicTorch.CBND.CoreMod.Items.Tools;
using AtomicTorch.CBND.CoreMod.Items.Tools.WateringCans;
using AtomicTorch.CBND.CoreMod.StaticObjects.Loot;
using AtomicTorch.CBND.CoreMod.StaticObjects.Vegetation.Plants;
using AtomicTorch.CBND.CoreMod.Systems;
using AtomicTorch.CBND.CoreMod.Systems.InteractionChecker;
using AtomicTorch.CBND.CoreMod.Systems.Watering;
using AtomicTorch.CBND.CoreMod.Systems.WateringCanRefill;
using AtomicTorch.CBND.GameApi.Data;
using AtomicTorch.CBND.GameApi.Data.Items;
using AtomicTorch.CBND.GameApi.Data.State;
using AtomicTorch.CBND.GameApi.Data.World;
using AtomicTorch.CBND.GameApi.Extensions;
using AtomicTorch.CBND.GameApi.Scripting;
using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;
using AtomicTorch.GameEngine.Common.Primitives;
using CryoFall.Automaton.ClientSettings;
using CryoFall.Automaton.ClientSettings.Options;
using CryoFall.Automaton.Items.Generic.Base;
using Microsoft.VisualBasic.CompilerServices;

namespace CryoFall.Automaton.Features
{
    public class FeatureAutoGardening : ProtoFeature<FeatureAutoGardening>
    {
        public override string Name => "AutoGardening";
        public override string Description => "Water plants with the watering can or granules in your hands. Auto uses fertilizers from your hands.";
        private string UseWateringCansString => "Use watering cans.";
        private string UseWateringGranulesString => "Use hygroscopic granules.";
        private string PlantsForWateringString => "Will water selected plants: ";
        private string UseFertilizersString => "Use fertilizers.";
        private string PlantsForFertilizingString => "Will fertilize selected plants: ";

        private readonly List<Plant> interactionQueue = new List<Plant>();
        private static bool IsCharacterRiding => PlayerCharacter.GetPublicState(Api.Client.Characters.CurrentPlayerCharacter).CurrentVehicle != null;

        public override bool IsEnabled
        {
            get => base.IsEnabled && (useWateringCans || useWateringGranules || useFertilizers);
            set => base.IsEnabled = value;
        }

        private List<IProtoEntity> WateringList { get; set; }
        private List<IProtoEntity> EnabledWateringList { get; set; }
        private List<IProtoEntity> FertilizingList { get; set; }
        private List<IProtoEntity> EnabledFertilizingList { get; set; }

        private bool useWateringCans = true;
        private bool useWateringGranules = true;
        private bool useFertilizers = true;
        private bool readyForInteraction = true;
        private bool isRefillRequested;
        private int delayTickCount;
        private IActionState lastActionState;

        protected override void PrepareFeature(List<IProtoEntity> entityList, List<IProtoEntity> requiredItemList)
        {
            WateringList = new List<IProtoEntity>(Api.FindProtoEntities<IProtoObjectPlant>());
            FertilizingList = new List<IProtoEntity>(Api.FindProtoEntities<IProtoObjectPlant>());
        }

        public override void PrepareOptions(SettingsFeature settingsFeature)
        {
            AddOptionIsEnabled(settingsFeature);
            Options.Add(new OptionSeparator());
            Options.Add(new OptionCheckBox(
                parentSettings: settingsFeature,
                id: "UseWateringCans",
                label: UseWateringCansString,
                defaultValue: true,
                valueChangedCallback: value => { useWateringCans = value; }));
            Options.Add(new OptionCheckBox(
                parentSettings: settingsFeature,
                id: "UseWateringGranules",
                label: UseWateringGranulesString,
                defaultValue: true,
                valueChangedCallback: value => { useWateringGranules = value; }));
            Options.Add(new OptionInformationText(PlantsForWateringString, 12));
            Options.Add(new OptionEntityList(
                parentSettings: settingsFeature,
                id: "EnabledWateringList",
                entityList: WateringList.OrderBy(entity => entity.Id),
                defaultEnabledList: new List<string>(),
                onEnabledListChanged: enabledList => EnabledWateringList = enabledList));
            Options.Add(new OptionSeparator());
            Options.Add(new OptionCheckBox(
                parentSettings: settingsFeature,
                id: "UseFertilizers",
                label: UseFertilizersString,
                defaultValue: true,
                valueChangedCallback: value => { useFertilizers = value; }));
            Options.Add(new OptionInformationText(PlantsForFertilizingString, 12));
            Options.Add(new OptionEntityList(
                parentSettings: settingsFeature,
                id: "EnabledTreeList",
                entityList: FertilizingList.OrderBy(entity => entity.Id),
                defaultEnabledList: new List<string>(),
                onEnabledListChanged: enabledList => EnabledFertilizingList = enabledList));
        }

        public override void Update(double deltaTime)
        {
            if (!IsEnabled || IsCharacterRiding) return;
            if (delayTickCount > 0) delayTickCount--;
            else if (CheckPrecondition()) CheckInteractionQueue();
        }

        public override void Execute()
        {
            if (IsEnabled && !IsCharacterRiding && CheckPrecondition()) FillQueue();
        }

        private void ChangeRefillRequestState(bool newState)
        {
            if (isRefillRequested == newState) return;
            delayTickCount = !newState ? 0 : 10;
            isRefillRequested = newState;
        }

        protected override bool CheckPrecondition()
        {
            var item = SelectedItem?.ProtoItem;
            return item != null && (item is ProtoItemFertilizer || item is ProtoItemPlantWatering || item is IProtoItemToolWateringCan);
        }

        private bool CheckPrecondition(Plant plant, out GardeningJobType jobType)
        {
            jobType = GardeningJobType.Undefined;
            var item = SelectedItem?.ProtoItem;
            if (item == null) return false;
            if (useFertilizers && item is ProtoItemFertilizer
                               && EnabledFertilizingList.Contains(plant.ProtoPlant)
                               && !plant.IsFertilized)
            {
                jobType = GardeningJobType.Fertilizing;
                delayTickCount = 40;
                return true;
            }

            if (!useWateringCans && !useWateringGranules || !EnabledWateringList.Contains(plant.ProtoPlant) || plant.IsWatered) return false;

            if (useWateringGranules && item is ProtoItemPlantWatering)
            {
                jobType = GardeningJobType.WateringWithItem;
                delayTickCount = 40;
                return true;
            }

            if (!useWateringCans || !(item is IProtoItemToolWateringCan wateringCan)) return false;
            jobType = GardeningJobType.WateringWithCan;
            if (wateringCan.SharedCanWater(SelectedItem) || lastActionState is WateringCanRefillActionState)
            {
                ChangeRefillRequestState(false);
                return true;
            }

            if (isRefillRequested) return false;
            WateringCanRefillSystem.Instance.ClientTryStartAction();
            ChangeRefillRequestState(true);
            return false;
        }

        private void CheckInteractionQueue()
        {
            if (!readyForInteraction || interactionQueue.Count == 0) return;
            var testList = new List<Plant>();
            testList.AddRange(interactionQueue);
            var jobType = GardeningJobType.Undefined;
            var plant = testList
                .Select(GetValidPlant)
                .FirstOrDefault(p => p != null && CheckPrecondition(p, out jobType));
            if (plant == null || jobType == GardeningJobType.Undefined) return;

            if (jobType == GardeningJobType.WateringWithCan)
            {
                WateringSystem.Instance.SharedStartAction(new ItemWorldActionRequest(CurrentCharacter, plant.PlantWorldObject, SelectedItem));
                return;
            }

            if (SelectedItem?.ProtoItem is IProtoAutoUsableItem item)
                item.ClientItemUseFinish(SelectedItem, TileCenter(plant.PlantWorldObject.OccupiedTile).ToVector2Ushort());
        }

        private Plant GetValidPlant(Plant plant)
        {
            if (!plant.PlantWorldObject.IsDestroyed
                && (!plant.IsFertilized || !plant.IsWatered) && !plant.IsSpoiled && !plant.HasHarvest
                && plant.ProtoPlant.SharedCanInteract(CurrentCharacter, plant.PlantWorldObject, false)
                && (lastActionState == null || lastActionState.TargetWorldObject != plant.PlantWorldObject
                                            || !lastActionState.IsCompleted
                                            || lastActionState.IsCancelled
                                            || lastActionState.IsCancelledByServer)) return plant;
            interactionQueue.RemoveAll(o => o.PlantWorldObject.Id == plant.PlantWorldObject.Id);
            return null;
        }

        private void FillQueue()
        {
            using var objectsInCharacterInteractionArea = InteractionCheckerSystem
                .SharedGetTempObjectsInCharacterInteractionArea(CurrentCharacter);
            if (objectsInCharacterInteractionArea == null) return;

            var objectsOfInterest = objectsInCharacterInteractionArea.AsList()
                .Select(test => test.PhysicsBody.AssociatedWorldObject)
                .Where(test => test != null && !test.IsDestroyed && test is IStaticWorldObject && test.ProtoGameObject is IProtoObjectPlant)
                .Where(test => useFertilizers && EnabledFertilizingList.Contains(test.ProtoGameObject)
                               || (useWateringCans || useWateringGranules) && EnabledWateringList.Contains(test.ProtoGameObject))
                .Select(o => GetValidPlant(new Plant(o as IStaticWorldObject)))
                .Where(o => o != null && !interactionQueue.Contains(o))
                .OrderBy(p => p.PlantWorldObject.TilePosition.TileDistanceTo(CurrentCharacter.TilePosition))
                .ToList();

            if (objectsOfInterest.Count > 0) interactionQueue.AddRange(objectsOfInterest);
        }

        private void Reset()
        {
            ChangeRefillRequestState(false);
            readyForInteraction = true;
            lastActionState = null;
        }

        public override void Stop()
        {
            if (interactionQueue?.Count > 0)
            {
                interactionQueue.Clear();
                InteractionCheckerSystem.CancelCurrentInteraction(CurrentCharacter);
            }

            Reset();
        }

        private void OnActionStateChanged()
        {
            if (PrivateState.CurrentActionState == null) readyForInteraction = true;
            else
            {
                readyForInteraction = false;
                lastActionState = PrivateState.CurrentActionState;
            }
        }

        public override void Start(ClientComponent parentComponent)
        {
            base.Start(parentComponent);

            // Check if there an action in progress.
            if (PrivateState.CurrentActionState != null)
            {
                readyForInteraction = false;
                lastActionState = PrivateState.CurrentActionState;
            }

            // Check if we opened loot container before enabling component.
            var currentInteractionObject = InteractionCheckerSystem.SharedGetCurrentInteraction(CurrentCharacter);
            if (currentInteractionObject?.ProtoWorldObject is ProtoObjectLootContainer)
            {
                readyForInteraction = false;
            }
        }

        public override void SetupSubscriptions(ClientComponent parentComponent) => PrivateState.ClientSubscribe(
            s => s.CurrentActionState, OnActionStateChanged, parentComponent);

        public static Vector2D TileCenter(Tile tile) => new Vector2D(tile.Position.X + 0.5, tile.Position.Y + 0.5);
    }
}