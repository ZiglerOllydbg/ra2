//
// Auto Generated Code By excel2json
// https://neil3d.gitee.io/coding/excel2json.html
// 1. 每个 Sheet 形成一个 Struct 定义, Sheet 的名称作为 Struct 的名称
// 2. 表格约定：第一行是变量名称，第二行是变量类型

// Generate From Unit.xlsx
using UnityEngine.Scripting;

[Preserve]
public class ConfUnit
{
	public ConfUnit() {}

	[Preserve]
	public const string JsonFileName = "Unit";
	public int ID; // 编号
	public int Type; // 建筑类型：1=大兵；2=獾式坦克；3=灰熊坦克；
	public string Prefab; // 资源prefab
	public int Hp; // 血量
	public int Atk; // 攻击
	public int Def; // 防御
	public int AtkInterval; // 攻击频率
	public int AtkCount; // 攻击目标数量
	public int Speed; // 移动速度
	public int CostMoney; // 消耗金钱
	public string Name; // 名称
	public string Description; // 描述
}


// End of Auto Generated Code
