using zUnity;

namespace ZLockstep.Simulation.ECS.Components
{
    /// <summary>
    /// 逻辑层的Transform组件（纯数据，使用确定性数学）
    /// 存储实体的位置、旋转、缩放信息
    /// </summary>
    public struct TransformComponent : IComponent
    {
        public zVector3 Position;
        public zQuaternion Rotation;
        public zVector3 Scale;

        public static TransformComponent Default => new TransformComponent
        {
            Position = zVector3.zero,
            Rotation = zQuaternion.identity,
            Scale = zVector3.one
        };
    }
}

