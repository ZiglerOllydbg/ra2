using Game.RA2.Client;
using ZFrame;
using ZLockstep.Simulation.ECS;

public class SelectBuildEvent : ModuleEvent
{
    public BuildingType BuildingType { get; private set; }

    public SelectBuildEvent(BuildingType buildingType) : base()
    {
        BuildingType = buildingType;
    }
}