using System.IO;

public class MapManager
{
    public MapCellData[,] grid;
    public int width;
    public int height;

    public void LoadMap(byte[] mapBytes)
    {
        using (MemoryStream ms = new MemoryStream(mapBytes))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            string magic = reader.ReadString();
            int version = reader.ReadInt32();
            width = reader.ReadInt32();
            height = reader.ReadInt32();

            grid = new MapCellData[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    grid[x, y] = new MapCellData
                    {
                        TerrainType = reader.ReadByte(),
                        CollisionFlags = reader.ReadByte(),
                        ResourceType = reader.ReadByte()
                    };
                }
            }
        }
    }

    // 核心寻路用的 API (A*)
    public bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        // 检查阻挡位
        return (grid[x, y].CollisionFlags & 1) == 0; 
    }
}