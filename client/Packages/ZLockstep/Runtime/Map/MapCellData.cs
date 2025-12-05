[System.Serializable]
public struct MapCellData
{
    // 地形类型 (0: 空地, 1: 草地, 2: 泥沼, 3: 山地) -> 决定移动速度
    public byte TerrainType; 
    
    // 阻挡信息 (使用位运算，例如 bit 0: 地面阻挡, bit 1: 空中阻挡)
    // 也可以简单用 bool IsWalkable
    public byte CollisionFlags; 

    // 高度 (用于处理视野或高地优势)
    public byte Height;

    // 资源类型 (0: 无, 1: 黄金, 2: 钻石)
    public byte ResourceType;
    
    // 资源剩余量 (如果是刷上去的矿脉)
    public ushort ResourceAmount;
}