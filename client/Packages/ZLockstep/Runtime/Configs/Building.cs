//
// Auto Generated Code By excel2json
// https://neil3d.gitee.io/coding/excel2json.html
// 1. 每个 Sheet 形成一个 Struct 定义, Sheet 的名称作为 Struct 的名称
// 2. 表格约定：第一行是变量名称，第二行是变量类型

// Generate From Building.xlsx

public class ConfBuilding
{
	public const string JsonFileName = "Building";	public string ID; // 编号
	public int Type; // 建筑类型：1=主基地；2=矿源；3=采矿场；4=电厂；5=兵营；6=坦克工厂；7=防御塔
	public int Manual; // 手动建造
	public string Prefab; // 资源prefab
	public int Size; // 占地尺寸（直径）建议奇数
	public int Hp; // 血量
	public int Atk; // 攻击
	public int Def; // 防御
	public int ConstructionTime; // 建造时间（秒）
	public int CostMoney; // 消耗金钱
	public int CostPower; // 消耗电量
	public string Name; // 名称
	public string Description; // 描述
}

public class ConfBuildingPlace
{
	public const string JsonFileName = "Building";	public string ID; // 编号
	public int CampID; // 阵营
	public int Type; // 建筑类型：1=主基地；2=矿源；3=采矿场；4=电厂；5=兵营；6=坦克工厂；7=防御塔
	public int Count; // 数量
	public string Position; // 位置
	public string Name; // 名称
	public int Enabled; // 是否启用
}


// End of Auto Generated Code
