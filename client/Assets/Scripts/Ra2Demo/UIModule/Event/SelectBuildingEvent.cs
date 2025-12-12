using Game.RA2.Client;
using ZFrame;
using ZLockstep.Simulation.ECS;

public class SelectBuildingEvent : ModuleEvent
{
    public BuildingType BuildingType { get; private set; }

    public SelectBuildingEvent(BuildingType buildingType) : base()
    {
        BuildingType = buildingType;
    }
}