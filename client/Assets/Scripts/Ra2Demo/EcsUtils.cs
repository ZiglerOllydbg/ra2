using ZLockstep.Simulation.ECS.Components;

public static class EcsUtils
{
    /// <summary>
    /// 获取本地玩家的经济信息（资金和电力）
    /// </summary>
    /// <param name="game">游戏实例</param>
    /// <param name="money">输出资金值</param>
    /// <param name="power">输出电力值</param>
    /// <returns>是否成功获取经济信息</returns>
    public static bool GetLocalPlayerEconomy(ZLockstep.Sync.Game game, out int money, out int power)
    {
        money = 0;
        power = 0;

        if (game == null || game.World == null)
            return false;

        // 检查是否存在全局信息组件
        if (!game.World.ComponentManager.HasGlobalComponent<GlobalInfoComponent>())
            return false;

        // 获取全局信息组件以确定本地玩家阵营ID
        var globalInfoComponent = game.World.ComponentManager.GetGlobalComponent<GlobalInfoComponent>();

        // 查找属于本地玩家的经济组件
        var (economyComponent, _) = game.World.ComponentManager.GetComponentWithCondition<EconomyComponent>(
            e => game.World.ComponentManager.HasComponent<CampComponent>(e) &&
            game.World.ComponentManager.GetComponent<CampComponent>(e).CampId == globalInfoComponent.LocalPlayerCampId);

        if (economyComponent.Equals(default(EconomyComponent)))
        {
            UnityEngine.Debug.LogWarning($"[Utils] 未找到本地玩家阵营 {globalInfoComponent.LocalPlayerCampId} 的经济组件");
            return false;
        }

        money = economyComponent.Money;
        power = economyComponent.Power;
        return true;
    }
}