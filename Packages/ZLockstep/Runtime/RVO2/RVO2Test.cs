using zUnity;

namespace ZLockstep.RVO
{
    /// <summary>
    /// RVO2基本功能测试
    /// </summary>
    public class RVO2Test
    {
        /// <summary>
        /// 测试两个智能体相向而行
        /// </summary>
        public static void TestTwoAgentsHeadOn()
        {
            RVO2Simulator sim = new RVO2Simulator();

            // 创建两个相向而行的智能体
            zVector2 pos1 = new zVector2((zfloat)(-5), (zfloat)0);
            zVector2 pos2 = new zVector2((zfloat)5, (zfloat)0);
            
            zfloat radius = new zfloat(0, 5000); // 0.5
            zfloat maxSpeed = new zfloat(1);

            int agent1 = sim.AddAgent(pos1, radius, maxSpeed);
            int agent2 = sim.AddAgent(pos2, radius, maxSpeed);

            // 设置相向的期望速度
            sim.SetAgentPrefVelocity(agent1, new zVector2((zfloat)1, (zfloat)0)); // 向右
            sim.SetAgentPrefVelocity(agent2, new zVector2((zfloat)(-1), (zfloat)0)); // 向左

            // 模拟多步
            zfloat deltaTime = new zfloat(0, 1000); // 0.1秒
            for (int i = 0; i < 50; i++)
            {
                sim.DoStep(deltaTime);
                
                zVector2 p1 = sim.GetAgentPosition(agent1);
                zVector2 p2 = sim.GetAgentPosition(agent2);
                zfloat distance = (p2 - p1).magnitude;

                // 检查是否保持安全距离
                zfloat minSafeDistance = radius * zfloat.Two; // 两倍半径
                
                if (i % 10 == 0)
                {
                    zUDebug.Log($"Step {i}: Distance = {distance}, Safe = {distance > minSafeDistance}");
                }
            }
        }

        /// <summary>
        /// 测试单个智能体朝目标移动
        /// </summary>
        public static void TestSingleAgentGoToGoal()
        {
            RVO2Simulator sim = new RVO2Simulator();

            zVector2 startPos = new zVector2((zfloat)0, (zfloat)0);
            zVector2 goalPos = new zVector2((zfloat)10, (zfloat)0);
            
            zfloat radius = new zfloat(0, 5000); // 0.5
            zfloat maxSpeed = new zfloat(2);

            int agent = sim.AddAgent(startPos, radius, maxSpeed);

            zfloat deltaTime = new zfloat(0, 1000); // 0.1秒
            int steps = 0;
            int maxSteps = 100;

            while (steps < maxSteps)
            {
                zVector2 currentPos = sim.GetAgentPosition(agent);
                zVector2 toGoal = goalPos - currentPos;
                zfloat distance = toGoal.magnitude;

                if (distance < new zfloat(0, 1000)) // 0.1
                {
                    zUDebug.Log($"到达目标! 步数: {steps}");
                    break;
                }

                // 设置期望速度
                zVector2 direction = toGoal / distance;
                sim.SetAgentPrefVelocity(agent, direction * maxSpeed);

                sim.DoStep(deltaTime);
                steps++;
            }

            if (steps >= maxSteps)
            {
                zUDebug.Log("未能在规定步数内到达目标");
            }
        }

        /// <summary>
        /// 测试多智能体避障
        /// </summary>
        public static void TestMultipleAgentsAvoidance()
        {
            RVO2Simulator sim = new RVO2Simulator();

            int numAgents = 4;
            int[] agentIds = new int[numAgents];
            zVector2[] startPositions = new zVector2[numAgents];
            zVector2[] goalPositions = new zVector2[numAgents];

            // 创建四个角落的智能体，目标是对角
            startPositions[0] = new zVector2((zfloat)(-5), (zfloat)(-5));
            goalPositions[0] = new zVector2((zfloat)5, (zfloat)5);

            startPositions[1] = new zVector2((zfloat)5, (zfloat)(-5));
            goalPositions[1] = new zVector2((zfloat)(-5), (zfloat)5);

            startPositions[2] = new zVector2((zfloat)5, (zfloat)5);
            goalPositions[2] = new zVector2((zfloat)(-5), (zfloat)(-5));

            startPositions[3] = new zVector2((zfloat)(-5), (zfloat)5);
            goalPositions[3] = new zVector2((zfloat)5, (zfloat)(-5));

            zfloat radius = new zfloat(0, 5000); // 0.5
            zfloat maxSpeed = new zfloat(1, 5000); // 1.5

            for (int i = 0; i < numAgents; i++)
            {
                agentIds[i] = sim.AddAgent(startPositions[i], radius, maxSpeed);
            }

            // 模拟
            zfloat deltaTime = new zfloat(0, 1000); // 0.1秒
            bool hasCollision = false;

            for (int step = 0; step < 100; step++)
            {
                // 设置期望速度
                for (int i = 0; i < numAgents; i++)
                {
                    zVector2 currentPos = sim.GetAgentPosition(agentIds[i]);
                    zVector2 toGoal = goalPositions[i] - currentPos;
                    zfloat distance = toGoal.magnitude;

                    if (distance > new zfloat(0, 1000)) // 0.1
                    {
                        zVector2 direction = toGoal / distance;
                        sim.SetAgentPrefVelocity(agentIds[i], direction * maxSpeed);
                    }
                    else
                    {
                        sim.SetAgentPrefVelocity(agentIds[i], zVector2.zero);
                    }
                }

                sim.DoStep(deltaTime);

                // 检查碰撞
                for (int i = 0; i < numAgents; i++)
                {
                    for (int j = i + 1; j < numAgents; j++)
                    {
                        zVector2 p1 = sim.GetAgentPosition(agentIds[i]);
                        zVector2 p2 = sim.GetAgentPosition(agentIds[j]);
                        zfloat dist = (p2 - p1).magnitude;
                        zfloat minDist = radius * zfloat.Two;

                        if (dist < minDist)
                        {
                            hasCollision = true;
                            zUDebug.Log($"Step {step}: 检测到碰撞! Agent {i} 和 Agent {j}, 距离 = {dist}");
                        }
                    }
                }
            }

            if (!hasCollision)
            {
                zUDebug.Log("测试通过: 无碰撞发生");
            }
        }

        /// <summary>
        /// 性能测试
        /// </summary>
        public static void TestPerformance()
        {
            RVO2Simulator sim = new RVO2Simulator();

            int numAgents = 50;
            zfloat radius = new zfloat(0, 5000); // 0.5
            zfloat maxSpeed = new zfloat(2);

            // 创建网格布局的智能体
            for (int i = 0; i < numAgents; i++)
            {
                zfloat x = (zfloat)(i % 10);
                zfloat y = (zfloat)(i / 10);
                zVector2 pos = new zVector2(x, y);
                sim.AddAgent(pos, radius, maxSpeed);
            }

            // 模拟100步并计时
            zfloat deltaTime = new zfloat(0, 1000); // 0.1秒
            
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            for (int step = 0; step < 100; step++)
            {
                // 设置随机期望速度
                for (int i = 0; i < numAgents; i++)
                {
                    zVector2 randomDir = new zVector2(
                        new zfloat(0, 5000), // 0.5
                        new zfloat(0, 5000)  // 0.5
                    ).normalized;
                    sim.SetAgentPrefVelocity(i, randomDir * maxSpeed);
                }

                sim.DoStep(deltaTime);
            }

            sw.Stop();
            zUDebug.Log($"性能测试: {numAgents} 个智能体, 100 步模拟耗时: {sw.ElapsedMilliseconds} ms");
            zUDebug.Log($"平均每步: {sw.ElapsedMilliseconds / 100.0} ms");
        }

        /// <summary>
        /// 运行所有测试
        /// </summary>
        public static void RunAllTests()
        {
            zUDebug.Log("========== RVO2 测试开始 ==========");
            
            zUDebug.Log("\n[测试1] 两个智能体相向而行:");
            TestTwoAgentsHeadOn();
            
            zUDebug.Log("\n[测试2] 单个智能体移动到目标:");
            TestSingleAgentGoToGoal();
            
            zUDebug.Log("\n[测试3] 多智能体避障:");
            TestMultipleAgentsAvoidance();
            
            zUDebug.Log("\n[测试4] 性能测试:");
            TestPerformance();
            
            zUDebug.Log("\n========== RVO2 测试结束 ==========");
        }
    }
}

