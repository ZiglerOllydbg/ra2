using System.Collections.Generic;
using System.Linq;
using zUnity;

namespace ZLockstep.Flow
{
    /// <summary>
    /// 流场信息（用于调试）
    /// </summary>
    public struct FlowFieldInfo
    {
        public int fieldId;
        public int targetGridX;
        public int targetGridY;
        public int referenceCount;
        public bool isDirty;
        public int lastUpdateFrame;
    }

    /// <summary>
    /// 流场管理器
    /// 负责创建、缓存、更新流场
    /// </summary>
    public class FlowFieldManager
    {
        private IFlowFieldMap map;
        private Dictionary<long, FlowField> fieldCache;  // key: targetGridX | (targetGridY << 32)
        private Dictionary<string, FlowField> multiFieldCache; // key: "x_y|x_y|..." 排序后
        private Dictionary<int, FlowField> fieldById;
        private Queue<FlowField> dirtyFields;
        private HashSet<int> dirtySet;
        private int nextFieldId;
        private int maxCachedFields;
        private int currentFrame;
        private int maxUpdatesPerFrame;

        public FlowFieldManager()
        {
            fieldCache = new Dictionary<long, FlowField>();
            multiFieldCache = new Dictionary<string, FlowField>();
            fieldById = new Dictionary<int, FlowField>();
            dirtyFields = new Queue<FlowField>();
            dirtySet = new HashSet<int>();
            nextFieldId = 0;
            maxCachedFields = 20;
            maxUpdatesPerFrame = 2;
            currentFrame = 0;
        }

        /// <summary>
        /// 初始化流场管理器
        /// </summary>
        public void Initialize(IFlowFieldMap mapInterface, int maxFields = 20)
        {
            map = mapInterface;
            maxCachedFields = maxFields;
        }

        /// <summary>
        /// 请求到指定世界坐标的流场
        /// </summary>
        public int RequestFlowField(zVector2 targetWorldPos)
        {
            map.WorldToGrid(targetWorldPos, out int gridX, out int gridY);
            return RequestFlowFieldToGrid(gridX, gridY);
        }

        /// <summary>
        /// 请求多源（多个世界坐标目标）流场
        /// </summary>
        public int RequestFlowFieldMultiWorld(List<zVector2> targetWorldPositions)
        {
            List<(int x, int y)> cells = new List<(int x, int y)>();
            HashSet<long> seen = new HashSet<long>();
            foreach (var wp in targetWorldPositions)
            {
                map.WorldToGrid(wp, out int gx, out int gy);
                long k = ((long)gx) | (((long)gy) << 32);
                if (seen.Add(k))
                {
                    cells.Add((gx, gy));
                }
            }
            return RequestFlowFieldMulti(cells);
        }

        /// <summary>
        /// 请求多源（多个格子目标）流场
        /// </summary>
        public int RequestFlowFieldMulti(List<(int x, int y)> targetCells)
        {
            if (targetCells == null || targetCells.Count == 0)
                return -1;

            // 去重并排序 (y,x) 稳定 key
            var unique = targetCells.Distinct().OrderBy(t => t.y).ThenBy(t => t.x).ToList();
            string key = string.Join("|", unique.Select(t => $"{t.x}_{t.y}"));

            if (multiFieldCache.TryGetValue(key, out FlowField fieldHit))
            {
                fieldHit.referenceCount++;
                return fieldHit.fieldId;
            }

            // 检查缓存是否已满
            if (fieldCache.Count + multiFieldCache.Count >= maxCachedFields)
            {
                CleanupUnusedFields();
            }

            // 创建并计算
            FlowField field = new FlowField(nextFieldId++, map.GetWidth(), map.GetHeight());
            field.referenceCount = 1;
            field.isDirty = true;
            field.isMulti = true;

            FlowFieldCalculator.CalculateMulti(field, map, unique);
            field.lastUpdateFrame = currentFrame;

            multiFieldCache[key] = field;
            fieldById[field.fieldId] = field;
            return field.fieldId;
        }

        /// <summary>
        /// 请求到指定格子的流场
        /// </summary>
        public int RequestFlowFieldToGrid(int targetGridX, int targetGridY)
        {
            // 生成缓存key
            long key = MakeKey(targetGridX, targetGridY);

            // 检查是否已存在
            if (fieldCache.TryGetValue(key, out FlowField field))
            {
                field.referenceCount++;
                return field.fieldId;
            }

            // 检查缓存是否已满
            if (fieldCache.Count >= maxCachedFields)
            {
                CleanupUnusedFields();
            }

            // 创建新流场
            field = new FlowField(nextFieldId++, map.GetWidth(), map.GetHeight());
            field.referenceCount = 1;
            field.isDirty = true;

            // 计算流场
            FlowFieldCalculator.Calculate(field, map, targetGridX, targetGridY);
            field.lastUpdateFrame = currentFrame;

            // 缓存
            fieldCache[key] = field;
            fieldById[field.fieldId] = field;

            return field.fieldId;
        }

        /// <summary>
        /// 释放流场
        /// </summary>
        public void ReleaseFlowField(int fieldId)
        {
            if (fieldById.TryGetValue(fieldId, out FlowField field))
            {
                field.referenceCount--;
                if (field.referenceCount < 0)
                {
                    field.referenceCount = 0;
                }
            }
        }

        /// <summary>
        /// 获取指定世界位置的移动方向
        /// </summary>
        public zVector2 SampleDirection(int fieldId, zVector2 worldPos)
        {
            if (!fieldById.TryGetValue(fieldId, out FlowField field))
            {
                return zVector2.zero;
            }

            map.WorldToGrid(worldPos, out int gridX, out int gridY);

            if (!field.IsValid(gridX, gridY))
            {
                return zVector2.zero;
            }

            int index = field.GetIndex(gridX, gridY);
            return field.directions[index];
        }

        /// <summary>
        /// 获取到目标的估算代价
        /// </summary>
        public zfloat SampleCost(int fieldId, zVector2 worldPos)
        {
            if (!fieldById.TryGetValue(fieldId, out FlowField field))
            {
                return zfloat.Infinity;
            }

            map.WorldToGrid(worldPos, out int gridX, out int gridY);

            if (!field.IsValid(gridX, gridY))
            {
                return zfloat.Infinity;
            }

            int index = field.GetIndex(gridX, gridY);
            return field.costs[index];
        }

        /// <summary>
        /// 判断是否已到达目标区域
        /// </summary>
        public bool IsAtTarget(int fieldId, zVector2 worldPos, zfloat threshold)
        {
            zfloat cost = SampleCost(fieldId, worldPos);
            return cost < threshold;
        }

        /// <summary>
        /// 标记地图区域为脏（地图变化时调用）
        /// </summary>
        public void MarkRegionDirty(int minX, int minY, int maxX, int maxY)
        {
            // 将逻辑格子坐标转换为流场格子坐标
            int flowMinX = minX / 2;
            int flowMinY = minY / 2;
            int flowMaxX = (maxX + 1) / 2;
            int flowMaxY = (maxY + 1) / 2;

            foreach (var field in fieldById.Values)
            {
                if (field.referenceCount <= 0)
                    continue;

                if (!field.isMulti)
                {
                    if (field.targetGridX >= flowMinX && field.targetGridX <= flowMaxX &&
                        field.targetGridY >= flowMinY && field.targetGridY <= flowMaxY)
                    {
                        field.isDirty = true;
                        if (dirtySet.Add(field.fieldId))
                        {
                            dirtyFields.Enqueue(field);
                        }
                    }
                }
                else
                {
                    bool intersect = !(field.maxTargetX < flowMinX || field.minTargetX > flowMaxX ||
                                       field.maxTargetY < flowMinY || field.minTargetY > flowMaxY);
                    if (intersect)
                    {
                        field.isDirty = true;
                        if (dirtySet.Add(field.fieldId))
                        {
                            dirtyFields.Enqueue(field);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 更新所有脏流场
        /// </summary>
        public void UpdateDirtyFields()
        {
            int updated = 0;
            while (dirtyFields.Count > 0 && updated < maxUpdatesPerFrame)
            {
                FlowField field = dirtyFields.Dequeue();
                
                if (field.isDirty)
                {
                    if (field.isMulti)
                    {
                        List<(int x, int y)> targets = new List<(int x, int y)>();
                        if (field.targetXs != null)
                        {
                            for (int i = 0; i < field.targetXs.Length; i++)
                                targets.Add((field.targetXs[i], field.targetYs[i]));
                        }
                        FlowFieldCalculator.CalculateMulti(field, map, targets);
                    }
                    else
                    {
                        FlowFieldCalculator.Calculate(field, map, field.targetGridX, field.targetGridY);
                    }
                    field.lastUpdateFrame = currentFrame;
                    updated++;
                }
                // 出队后无论是否更新，移除脏标记记录，避免重复
                dirtySet.Remove(field.fieldId);
            }
        }

        /// <summary>
        /// 强制更新指定流场
        /// </summary>
        public void ForceUpdateFlowField(int fieldId)
        {
            if (fieldById.TryGetValue(fieldId, out FlowField field))
            {
                if (field.isMulti)
                {
                    List<(int x, int y)> targets = new List<(int x, int y)>();
                    if (field.targetXs != null)
                    {
                        for (int i = 0; i < field.targetXs.Length; i++)
                            targets.Add((field.targetXs[i], field.targetYs[i]));
                    }
                    FlowFieldCalculator.CalculateMulti(field, map, targets);
                }
                else
                {
                    FlowFieldCalculator.Calculate(field, map, field.targetGridX, field.targetGridY);
                }
                field.lastUpdateFrame = currentFrame;
            }
        }

        /// <summary>
        /// 获取当前活跃的流场数量
        /// </summary>
        public int GetActiveFieldCount()
        {
            return fieldById.Count;
        }

        public Dictionary<int, FlowField> GetActiveFields()
        {
            return fieldById;
        }

        /// <summary>
        /// 获取流场信息
        /// </summary>
        public FlowFieldInfo GetFieldInfo(int fieldId)
        {
            if (fieldById.TryGetValue(fieldId, out FlowField field))
            {
                return new FlowFieldInfo
                {
                    fieldId = field.fieldId,
                    targetGridX = field.targetGridX,
                    targetGridY = field.targetGridY,
                    referenceCount = field.referenceCount,
                    isDirty = field.isDirty,
                    lastUpdateFrame = field.lastUpdateFrame
                };
            }
            return default(FlowFieldInfo);
        }

        /// <summary>
        /// 清理无引用的流场
        /// </summary>
        public void CleanupUnusedFields()
        {
            List<long> keysToRemove = new List<long>();
            List<int> idsToRemove = new List<int>();
            List<string> multiKeysToRemove = new List<string>();

            foreach (var kvp in fieldCache)
            {
                if (kvp.Value.referenceCount <= 0)
                {
                    keysToRemove.Add(kvp.Key);
                    idsToRemove.Add(kvp.Value.fieldId);
                }
            }

            foreach (var kvp in multiFieldCache)
            {
                if (kvp.Value.referenceCount <= 0)
                {
                    multiKeysToRemove.Add(kvp.Key);
                    idsToRemove.Add(kvp.Value.fieldId);
                }
            }

            foreach (long key in keysToRemove)
            {
                fieldCache.Remove(key);
            }

            foreach (int id in idsToRemove)
            {
                fieldById.Remove(id);
            }

            foreach (string key in multiKeysToRemove)
            {
                multiFieldCache.Remove(key);
            }
        }

        /// <summary>
        /// 清除所有流场
        /// </summary>
        public void Clear()
        {
            fieldCache.Clear();
            multiFieldCache.Clear();
            fieldById.Clear();
            dirtyFields.Clear();
            dirtySet.Clear();
        }

        /// <summary>
        /// 每帧更新（用于更新脏流场）
        /// </summary>
        public void Tick()
        {
            currentFrame++;
            UpdateDirtyFields();
        }

        /// <summary>
        /// 设置每帧最多更新的流场数量
        /// </summary>
        public void SetMaxUpdatesPerFrame(int maxUpdates)
        {
            maxUpdatesPerFrame = maxUpdates;
        }

        private long MakeKey(int x, int y)
        {
            return ((long)x) | (((long)y) << 32);
        }
    }
}

