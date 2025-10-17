# ZLockstep - 帧同步（RTS）游戏框架设计

## 一、 核心引擎层 (Core Engine Layer)

### 1. 核心库 (Core Library)
- **确定性数学库 (Deterministic Math Library)**
  - 定点数 (Fixed-Point Numbers)
  - 向量、矩阵、四元数 (Vector, Matrix, Quaternion)
  - 几何与碰撞体 (Geometry & Colliders - Bounds, Ray, Sphere)
- **确定性辅助工具 (Deterministic Utilities)**
  - 随机数 (Random)
  - 容器 (Containers)
  - 序列化 (Serialization for replays and save/load)

### 2. 仿真核心 (Simulation Core)
- **`World`**: 游戏世界，管理所有实体和系统 (Game world, manages all entities and systems)
- **`ECS`**: 实体-组件-系统架构 (Entity-Component-System Architecture)
- **`Time`**: 确定性时间管理器 (Deterministic Time Manager)
- **`Event System`**: 事件系统 (Event System)

### 3. 物理与寻路 (Physics & Pathfinding)
- **确定性物理 (Deterministic Physics)**
  - 碰撞检测 (Collision Detection)
  - 空间划分 (Spatial Partitioning - e.g., Quadtree/Grid)
- **寻路 (Pathfinding)**
  - A* 算法 (A* Algorithm)
  - 地图与路径数据 (Map & Path Data)

### 4. AI 框架 (AI Framework)
- 行为树 / 状态机 (Behavior Tree / FSM)
- AI决策与感知框架 (AI Decision Making & Perception Framework)

### 5. 帧同步框架 (Lockstep Framework)
- **锁步同步管理器 (Lockstep Sync Manager)**: 核心逻辑，负责驱动游戏按帧执行。 (Core logic, drives the game frame by frame.)
- **命令序列化与分发 (Command Serialization & Distribution)**
- *注：本框架不包含底层网络传输实现，仅负责同步逻辑。(Note: This framework does not include the underlying network transport implementation, it is only responsible for the synchronization logic.)*


## 二、 游戏逻辑层 (Game Logic Layer)

### 1. 命令系统 (Command System)
- **玩家输入转换 (Player Input to Command)**
- **命令定义 (Command Definitions)**: 移动、攻击、建造等 (Move, Attack, Build, etc.)

### 2. 游戏玩法逻辑 (Gameplay Logic - Implemented with ECS)
- **Components**: `HealthComponent`, `AttackComponent`, `MovementComponent`, etc.
- **Systems**: `MovementSystem`, `AttackSystem`, `BuildSystem`, `ResourceSystem`, etc.

### 3. AI 逻辑 (AI Logic)
- **单位/建筑的具体AI行为 (Specific AI behaviors for units/buildings)**
- **电脑玩家策略 (Computer Player / AI opponent strategies)**

### 4. 回放系统 (Replay System)
- **命令的录制与回放 (Recording and playback of commands)**
