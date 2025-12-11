/*
 * Simulator.cs
 * RVO2 Library C#
 *
 * SPDX-FileCopyrightText: 2008 University of North Carolina at Chapel Hill
 * SPDX-License-Identifier: Apache-2.0
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Please send all bug reports to <geom@cs.unc.edu>.
 *
 * The authors may be contacted via:
 *
 * Jur van den Berg, Stephen J. Guy, Jamie Snape, Ming C. Lin, Dinesh Manocha
 * Dept. of Computer Science
 * 201 S. Columbia St.
 * Frederick P. Brooks, Jr. Computer Science Bldg.
 * Chapel Hill, N.C. 27599-3175
 * United States of America
 *
 * <http://gamma.cs.unc.edu/RVO2/>
 */

using System;
using System.Collections.Generic;
using zUnity;

namespace RVO
{
    /**
     * <summary>定义模拟。</summary>
     */
    public class Simulator
    {
        internal IList<Agent> agents_;
        internal IList<Obstacle> obstacles_;
        internal KdTree kdTree_;
        internal zfloat timeStep_;

        private Agent defaultAgent_;
        private zfloat globalTime_;

        /**
         * <summary>向模拟中添加具有默认属性的新智能体。</summary>
         *
         * <returns>智能体的编号，如果未设置智能体默认值则返回 -1。</returns>
         *
         * <param name="position">此智能体的二维起始位置。</param>
         */
        public int addAgent(Vector2 position)
        {
            if (defaultAgent_ == null)
            {
                return -1;
            }

            Agent agent = new();
            agent.simulator_ = this; // 设置Simulator引用
            agent.id_ = agents_.Count;
            agent.maxNeighbors_ = defaultAgent_.maxNeighbors_;
            agent.maxSpeed_ = defaultAgent_.maxSpeed_;
            agent.neighborDist_ = defaultAgent_.neighborDist_;
            agent.position_ = position;
            agent.radius_ = defaultAgent_.radius_;
            agent.timeHorizon_ = defaultAgent_.timeHorizon_;
            agent.timeHorizonObst_ = defaultAgent_.timeHorizonObst_;
            agent.velocity_ = defaultAgent_.velocity_;
            agents_.Add(agent);

            return agent.id_;
        }

        /**
         * <summary>向模拟中添加新智能体。</summary>
         *
         * <returns>智能体的编号。</returns>
         *
         * <param name="position">此智能体的二维起始位置。</param>
         * <param name="neighborDist">此智能体在导航中考虑的其他智能体的最大距离（中心点到中心点）。
         * 此数值越大，模拟运行时间越长。如果数值太低，模拟将不安全。必须为非负数。</param>
         * <param name="maxNeighbors">此智能体在导航中考虑的其他智能体的最大数量。
         * 此数值越大，模拟运行时间越长。如果数值太低，模拟将不安全。</param>
         * <param name="timeHorizon">此智能体由模拟计算的速度相对于其他智能体安全的最短时间。
         * 此数值越大，此智能体对附近其他智能体的响应越快，但选择速度的自由度越小。必须为正数。</param>
         * <param name="timeHorizonObst">此智能体由模拟计算的速度相对于障碍物安全的最短时间。
         * 此数值越大，此智能体对附近障碍物的响应越快，但选择速度的自由度越小。必须为正数。</param>
         * <param name="radius">此智能体的半径。必须为非负数。</param>
         * <param name="maxSpeed">此智能体的最大速度。必须为非负数。</param>
         * <param name="velocity">此智能体的初始二维线性速度。</param>
         */
        public int addAgent(Vector2 position, zfloat neighborDist, int maxNeighbors, zfloat timeHorizon, zfloat timeHorizonObst, zfloat radius, zfloat maxSpeed, Vector2 velocity)
        {
            Agent agent = new();
            agent.simulator_ = this; // 设置Simulator引用
            agent.id_ = agents_.Count;
            agent.maxNeighbors_ = maxNeighbors;
            agent.maxSpeed_ = maxSpeed;
            agent.neighborDist_ = neighborDist;
            agent.position_ = position;
            agent.radius_ = radius;
            agent.timeHorizon_ = timeHorizon;
            agent.timeHorizonObst_ = timeHorizonObst;
            agent.velocity_ = velocity;
            agents_.Add(agent);

            return agent.id_;
        }

        /**
         * <summary>向模拟中添加新障碍物。</summary>
         *
         * <returns>障碍物第一个顶点的编号，如果顶点数少于两个则返回 -1。</returns>
         *
         * <param name="vertices">多边形障碍物的顶点列表，按逆时针顺序排列。</param>
         *
         * <remarks>要添加"负"障碍物，例如环境周围的边界多边形，顶点应按顺时针顺序列出。</remarks>
         */
        public int addObstacle(IList<Vector2> vertices)
        {
            if (vertices.Count < 2)
            {
                return -1;
            }

            int obstacleNo = obstacles_.Count;

            for (int i = 0; i < vertices.Count; ++i)
            {
                Obstacle obstacle = new();
                obstacle.point_ = vertices[i];

                if (i != 0)
                {
                    obstacle.previous_ = obstacles_[obstacles_.Count - 1];
                    obstacle.previous_.next_ = obstacle;
                }

                if (i == vertices.Count - 1)
                {
                    obstacle.next_ = obstacles_[obstacleNo];
                    obstacle.next_.previous_ = obstacle;
                }

                obstacle.direction_ = RVOMath.normalize(vertices[(i == vertices.Count - 1 ? 0 : i + 1)] - vertices[i]);

                if (vertices.Count == 2)
                {
                    obstacle.convex_ = true;
                }
                else
                {
                    obstacle.convex_ = (RVOMath.leftOf(vertices[(i == 0 ? vertices.Count - 1 : i - 1)], vertices[i], vertices[(i == vertices.Count - 1 ? 0 : i + 1)]) >= zfloat.Zero);
                }

                obstacle.id_ = obstacles_.Count;
                obstacles_.Add(obstacle);
            }

            return obstacleNo;
        }

        /**
         * <summary>清除模拟。</summary>
         */
        public void Clear()
        {
            agents_ = new List<Agent>();
            defaultAgent_ = null;
            kdTree_ = new KdTree();
            kdTree_.simulator_ = this; // 设置Simulator引用
            obstacles_ = new List<Obstacle>();
            globalTime_ = zfloat.Zero;
            timeStep_ = new zfloat(0, 1000); // 0.1
        }

        /**
         * <summary>执行模拟步骤并更新每个智能体的二维位置和二维速度。</summary>
         *
         * <returns>模拟步骤后的全局时间。</returns>
         */
        public zfloat doStep()
        {
            kdTree_.buildAgentTree();

            // 单线程顺序执行：计算所有智能体的邻居和新速度
            for (int agentNo = 0; agentNo < agents_.Count; ++agentNo)
            {
                agents_[agentNo].computeNeighbors();
                agents_[agentNo].computeNewVelocity();
            }

            // 单线程顺序执行：更新所有智能体的位置和速度
            for (int agentNo = 0; agentNo < agents_.Count; ++agentNo)
            {
                agents_[agentNo].update();
            }

            globalTime_ += timeStep_;

            return globalTime_;
        }

        /**
         * <summary>返回指定智能体的指定智能体邻居。</summary>
         *
         * <returns>相邻智能体的编号。</returns>
         *
         * <param name="agentNo">要检索智能体邻居的智能体编号。</param>
         * <param name="neighborNo">要检索的智能体邻居编号。</param>
         */
        public int getAgentAgentNeighbor(int agentNo, int neighborNo)
        {
            return agents_[agentNo].agentNeighbors_[neighborNo].Value.id_;
        }

        /**
         * <summary>返回指定智能体的最大邻居数量。</summary>
         *
         * <returns>智能体的当前最大邻居数量。</returns>
         *
         * <param name="agentNo">要检索最大邻居数量的智能体编号。</param>
         */
        public int getAgentMaxNeighbors(int agentNo)
        {
            return agents_[agentNo].maxNeighbors_;
        }

        /**
         * <summary>返回指定智能体的最大速度。</summary>
         *
         * <returns>智能体的当前最大速度。</returns>
         *
         * <param name="agentNo">要检索最大速度的智能体编号。</param>
         */
        public zfloat getAgentMaxSpeed(int agentNo)
        {
            return agents_[agentNo].maxSpeed_;
        }

        /**
         * <summary>返回指定智能体的最大邻居距离。</summary>
         *
         * <returns>智能体的当前最大邻居距离。</returns>
         *
         * <param name="agentNo">要检索最大邻居距离的智能体编号。</param>
         */
        public zfloat getAgentNeighborDist(int agentNo)
        {
            return agents_[agentNo].neighborDist_;
        }

        /**
         * <summary>返回为计算指定智能体的当前速度而考虑的智能体邻居数量。</summary>
         *
         * <returns>为计算指定智能体的当前速度而考虑的智能体邻居数量。</returns>
         *
         * <param name="agentNo">要检索智能体邻居数量的智能体编号。</param>
         */
        public int getAgentNumAgentNeighbors(int agentNo)
        {
            return agents_[agentNo].agentNeighbors_.Count;
        }

        /**
         * <summary>返回为计算指定智能体的当前速度而考虑的障碍物邻居数量。</summary>
         *
         * <returns>为计算指定智能体的当前速度而考虑的障碍物邻居数量。</returns>
         *
         * <param name="agentNo">要检索障碍物邻居数量的智能体编号。</param>
         */
        public int getAgentNumObstacleNeighbors(int agentNo)
        {
            return agents_[agentNo].obstacleNeighbors_.Count;
        }

        /**
         * <summary>返回指定智能体的指定障碍物邻居。</summary>
         *
         * <returns>相邻障碍物边的第一个顶点编号。</returns>
         *
         * <param name="agentNo">要检索障碍物邻居的智能体编号。</param>
         * <param name="neighborNo">要检索的障碍物邻居编号。</param>
         */
        public int getAgentObstacleNeighbor(int agentNo, int neighborNo)
        {
            return agents_[agentNo].obstacleNeighbors_[neighborNo].Value.id_;
        }

        /**
         * <summary>返回指定智能体的 ORCA 约束。</summary>
         *
         * <returns>表示 ORCA 约束的线列表。</returns>
         *
         * <param name="agentNo">要检索 ORCA 约束的智能体编号。</param>
         *
         * <remarks>每条线左侧的半平面是相对于该 ORCA 约束的允许速度区域。</remarks>
         */
        public IList<Line> getAgentOrcaLines(int agentNo)
        {
            return agents_[agentNo].orcaLines_;
        }

        /**
         * <summary>返回指定智能体的二维位置。</summary>
         *
         * <returns>智能体（中心）的当前二维位置。</returns>
         *
         * <param name="agentNo">要检索二维位置的智能体编号。</param>
         */
        public Vector2 getAgentPosition(int agentNo)
        {
            return agents_[agentNo].position_;
        }

        /**
         * <summary>返回指定智能体的二维首选速度。</summary>
         *
         * <returns>智能体的当前二维首选速度。</returns>
         *
         * <param name="agentNo">要检索二维首选速度的智能体编号。</param>
         */
        public Vector2 getAgentPrefVelocity(int agentNo)
        {
            return agents_[agentNo].prefVelocity_;
        }

        /**
         * <summary>返回指定智能体的半径。</summary>
         *
         * <returns>智能体的当前半径。</returns>
         *
         * <param name="agentNo">要检索半径的智能体编号。</param>
         */
        public zfloat getAgentRadius(int agentNo)
        {
            return agents_[agentNo].radius_;
        }

        /**
         * <summary>返回指定智能体的时间范围。</summary>
         *
         * <returns>智能体的当前时间范围。</returns>
         *
         * <param name="agentNo">要检索时间范围的智能体编号。</param>
         */
        public zfloat getAgentTimeHorizon(int agentNo)
        {
            return agents_[agentNo].timeHorizon_;
        }

        /**
         * <summary>返回指定智能体相对于障碍物的时间范围。</summary>
         *
         * <returns>智能体相对于障碍物的当前时间范围。</returns>
         *
         * <param name="agentNo">要检索相对于障碍物时间范围的智能体编号。</param>
         */
        public zfloat getAgentTimeHorizonObst(int agentNo)
        {
            return agents_[agentNo].timeHorizonObst_;
        }

        /**
         * <summary>返回指定智能体的二维线性速度。</summary>
         *
         * <returns>智能体的当前二维线性速度。</returns>
         *
         * <param name="agentNo">要检索二维线性速度的智能体编号。</param>
         */
        public Vector2 getAgentVelocity(int agentNo)
        {
            return agents_[agentNo].velocity_;
        }

        /**
         * <summary>返回模拟的全局时间。</summary>
         *
         * <returns>模拟的当前全局时间（初始为零）。</returns>
         */
        public zfloat getGlobalTime()
        {
            return globalTime_;
        }

        /**
         * <summary>返回模拟中的智能体数量。</summary>
         *
         * <returns>模拟中的智能体数量。</returns>
         */
        public int getNumAgents()
        {
            return agents_.Count;
        }

        /**
         * <summary>返回模拟中的障碍物顶点数量。</summary>
         *
         * <returns>模拟中的障碍物顶点数量。</returns>
         */
        public int getNumObstacleVertices()
        {
            return obstacles_.Count;
        }

        /**
         * <summary>返回工作线程数量（单线程模式下始终返回1）。</summary>
         *
         * <returns>工作线程数量，单线程模式下始终为1。</returns>
         */
        public int GetNumWorkers()
        {
            return 1;
        }

        /**
         * <summary>返回指定障碍物顶点的二维位置。</summary>
         *
         * <returns>指定障碍物顶点的二维位置。</returns>
         *
         * <param name="vertexNo">要检索的障碍物顶点编号。</param>
         */
        public Vector2 getObstacleVertex(int vertexNo)
        {
            return obstacles_[vertexNo].point_;
        }

        /**
         * <summary>返回指定障碍物顶点在其多边形中的后继顶点编号。</summary>
         *
         * <returns>指定障碍物顶点在其多边形中的后继顶点编号。</returns>
         *
         * <param name="vertexNo">要检索后继顶点的障碍物顶点编号。</param>
         */
        public int getNextObstacleVertexNo(int vertexNo)
        {
            return obstacles_[vertexNo].next_.id_;
        }

        /**
         * <summary>返回指定障碍物顶点在其多边形中的前驱顶点编号。</summary>
         *
         * <returns>指定障碍物顶点在其多边形中的前驱顶点编号。</returns>
         *
         * <param name="vertexNo">要检索前驱顶点的障碍物顶点编号。</param>
         */
        public int getPrevObstacleVertexNo(int vertexNo)
        {
            return obstacles_[vertexNo].previous_.id_;
        }

        /**
         * <summary>返回模拟的时间步长。</summary>
         *
         * <returns>模拟的当前时间步长。</returns>
         */
        public zfloat getTimeStep()
        {
            return timeStep_;
        }

        /**
         * <summary>处理已添加的障碍物，以便在模拟中考虑它们。</summary>
         *
         * <remarks>在此函数调用后添加到模拟中的障碍物不会在模拟中考虑。</remarks>
         */
        public void processObstacles()
        {
            kdTree_.buildObstacleTree();
        }

        /**
         * <summary>对两个指定点之间相对于障碍物执行可见性查询。</summary>
         *
         * <returns>指定两点是否相互可见的布尔值。当障碍物尚未处理时返回 true。</returns>
         *
         * <param name="point1">查询的第一个点。</param>
         * <param name="point2">查询的第二个点。</param>
         * <param name="radius">连接两点的线与障碍物之间的最小距离，以便两点相互可见（可选）。必须为非负数。</param>
         */
        public bool queryVisibility(Vector2 point1, Vector2 point2, zfloat radius)
        {
            return kdTree_.queryVisibility(point1, point2, radius);
        }

        /**
         * <summary>设置任何新添加智能体的默认属性。</summary>
         *
         * <param name="neighborDist">新智能体在导航中考虑的其他智能体的默认最大距离（中心点到中心点）。
         * 此数值越大，模拟运行时间越长。如果数值太低，模拟将不安全。必须为非负数。</param>
         * <param name="maxNeighbors">新智能体在导航中考虑的其他智能体的默认最大数量。
         * 此数值越大，模拟运行时间越长。如果数值太低，模拟将不安全。</param>
         * <param name="timeHorizon">新智能体由模拟计算的速度相对于其他智能体安全的默认最短时间。
         * 此数值越大，智能体对附近其他智能体的响应越快，但选择速度的自由度越小。必须为正数。</param>
         * <param name="timeHorizonObst">新智能体由模拟计算的速度相对于障碍物安全的默认最短时间。
         * 此数值越大，智能体对附近障碍物的响应越快，但选择速度的自由度越小。必须为正数。</param>
         * <param name="radius">新智能体的默认半径。必须为非负数。</param>
         * <param name="maxSpeed">新智能体的默认最大速度。必须为非负数。</param>
         * <param name="velocity">新智能体的默认初始二维线性速度。</param>
         */
        public void setAgentDefaults(zfloat neighborDist, int maxNeighbors, zfloat timeHorizon, zfloat timeHorizonObst, zfloat radius, zfloat maxSpeed, Vector2 velocity)
        {
            if (defaultAgent_ == null)
            {
                defaultAgent_ = new Agent();
            }

            defaultAgent_.maxNeighbors_ = maxNeighbors;
            defaultAgent_.maxSpeed_ = maxSpeed;
            defaultAgent_.neighborDist_ = neighborDist;
            defaultAgent_.radius_ = radius;
            defaultAgent_.timeHorizon_ = timeHorizon;
            defaultAgent_.timeHorizonObst_ = timeHorizonObst;
            defaultAgent_.velocity_ = velocity;
        }

        /**
         * <summary>设置指定智能体的最大邻居数量。</summary>
         *
         * <param name="agentNo">要修改最大邻居数量的智能体编号。</param>
         * <param name="maxNeighbors">替换的最大邻居数量。</param>
         */
        public void setAgentMaxNeighbors(int agentNo, int maxNeighbors)
        {
            agents_[agentNo].maxNeighbors_ = maxNeighbors;
        }

        /**
         * <summary>设置指定智能体的最大速度。</summary>
         *
         * <param name="agentNo">要修改最大速度的智能体编号。</param>
         * <param name="maxSpeed">替换的最大速度。必须为非负数。</param>
         */
        public void setAgentMaxSpeed(int agentNo, zfloat maxSpeed)
        {
            agents_[agentNo].maxSpeed_ = maxSpeed;
        }

        /**
         * <summary>设置指定智能体的最大邻居距离。</summary>
         *
         * <param name="agentNo">要修改最大邻居距离的智能体编号。</param>
         * <param name="neighborDist">替换的最大邻居距离。必须为非负数。</param>
         */
        public void setAgentNeighborDist(int agentNo, zfloat neighborDist)
        {
            agents_[agentNo].neighborDist_ = neighborDist;
        }

        /**
         * <summary>设置指定智能体的二维位置。</summary>
         *
         * <param name="agentNo">要修改二维位置的智能体编号。</param>
         * <param name="position">替换的二维位置。</param>
         */
        public void setAgentPosition(int agentNo, Vector2 position)
        {
            agents_[agentNo].position_ = position;
        }

        /**
         * <summary>设置指定智能体的二维首选速度。</summary>
         *
         * <param name="agentNo">要修改二维首选速度的智能体编号。</param>
         * <param name="prefVelocity">替换的二维首选速度。</param>
         */
        public void setAgentPrefVelocity(int agentNo, Vector2 prefVelocity)
        {
            agents_[agentNo].prefVelocity_ = prefVelocity;
        }

        /**
         * <summary>设置指定智能体的半径。</summary>
         *
         * <param name="agentNo">要修改半径的智能体编号。</param>
         * <param name="radius">替换的半径。必须为非负数。</param>
         */
        public void setAgentRadius(int agentNo, zfloat radius)
        {
            agents_[agentNo].radius_ = radius;
        }

        /**
         * <summary>设置指定智能体相对于其他智能体的时间范围。</summary>
         *
         * <param name="agentNo">要修改时间范围的智能体编号。</param>
         * <param name="timeHorizon">相对于其他智能体的替换时间范围。必须为正数。</param>
         */
        public void setAgentTimeHorizon(int agentNo, zfloat timeHorizon)
        {
            agents_[agentNo].timeHorizon_ = timeHorizon;
        }

        /**
         * <summary>设置指定智能体相对于障碍物的时间范围。</summary>
         *
         * <param name="agentNo">要修改相对于障碍物时间范围的智能体编号。</param>
         * <param name="timeHorizonObst">相对于障碍物的替换时间范围。必须为正数。</param>
         */
        public void setAgentTimeHorizonObst(int agentNo, zfloat timeHorizonObst)
        {
            agents_[agentNo].timeHorizonObst_ = timeHorizonObst;
        }

        /**
         * <summary>设置指定智能体的二维线性速度。</summary>
         *
         * <param name="agentNo">要修改二维线性速度的智能体编号。</param>
         * <param name="velocity">替换的二维线性速度。</param>
         */
        public void setAgentVelocity(int agentNo, Vector2 velocity)
        {
            agents_[agentNo].velocity_ = velocity;
        }

        /**
         * <summary>设置模拟的全局时间。</summary>
         *
         * <param name="globalTime">模拟的全局时间。</param>
         */
        public void setGlobalTime(zfloat globalTime)
        {
            globalTime_ = globalTime;
        }

        /**
         * <summary>设置工作线程数量（单线程模式下此方法无效，始终使用单线程）。</summary>
         *
         * <param name="numWorkers">工作线程数量（在单线程模式下被忽略）。</param>
         */
        public void SetNumWorkers(int numWorkers)
        {
            // 单线程模式下忽略此设置
        }

        /**
         * <summary>设置模拟的时间步长。</summary>
         *
         * <param name="timeStep">模拟的时间步长。必须为正数。</param>
         */
        public void setTimeStep(zfloat timeStep)
        {
            timeStep_ = timeStep;
        }

        /**
         * <summary>构造并初始化模拟。</summary>
         */
        public Simulator()
        {
            Clear();
        }
    }
}
