using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "RTS/GameTile")]
public class GameTile : Tile
{
    [Header("Logic Data")]
    public byte terrainType; // 地形类型ID
    public bool isWalkable = true;
    public byte height = 0;
    public byte resourceType = 0;
    
    // 如果是3D模型（比如树），可以利用Tile原本的 gameObject 属性
    // 如果只是贴图，就用 sprite
}