/*
 * KdTree.cs
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
     * <summary>定义模拟中智能体和静态障碍物的 k-D 树。</summary>
     */
    internal class KdTree
    {
        internal Simulator simulator_; // 持有Simulator引用

        /**
         * <summary>定义智能体 k-D 树的节点。</summary>
         */
        private struct AgentTreeNode
        {
            internal int begin_;
            internal int end_;
            internal int left_;
            internal int right_;
            internal zfloat maxX_;
            internal zfloat maxY_;
            internal zfloat minX_;
            internal zfloat minY_;
        }

        /**
         * <summary>定义一对标量值。</summary>
         */
        private struct FloatPair
        {
            private readonly zfloat a_;
            private readonly zfloat b_;

            /**
             * <summary>构造并初始化一对标量值。</summary>
             *
             * <param name="a">第一个标量值。</param>
             * <param name="b">第二个标量值。</param>
             */
            internal FloatPair(zfloat a, zfloat b)
            {
                a_ = a;
                b_ = b;
            }

            /**
             * <summary>如果第一对标量值小于第二对标量值则返回 true。</summary>
             *
             * <returns>如果第一对标量值小于第二对标量值则返回 true。</returns>
             *
             * <param name="pair1">第一对标量值。</param>
             * <param name="pair2">第二对标量值。</param>
             */
            public static bool operator <(FloatPair pair1, FloatPair pair2)
            {
                return pair1.a_ < pair2.a_ || !(pair2.a_ < pair1.a_) && pair1.b_ < pair2.b_;
            }

            /**
             * <summary>如果第一对标量值小于或等于第二对标量值则返回 true。</summary>
             *
             * <returns>如果第一对标量值小于或等于第二对标量值则返回 true。</returns>
             *
             * <param name="pair1">第一对标量值。</param>
             * <param name="pair2">第二对标量值。</param>
             */
            public static bool operator <=(FloatPair pair1, FloatPair pair2)
            {
                return (pair1.a_ == pair2.a_ && pair1.b_ == pair2.b_) || pair1 < pair2;
            }

            /**
             * <summary>如果第一对标量值大于第二对标量值则返回 true。</summary>
             *
             * <returns>如果第一对标量值大于第二对标量值则返回 true。</returns>
             *
             * <param name="pair1">第一对标量值。</param>
             * <param name="pair2">第二对标量值。</param>
             */
            public static bool operator >(FloatPair pair1, FloatPair pair2)
            {
                return !(pair1 <= pair2);
            }

            /**
             * <summary>如果第一对标量值大于或等于第二对标量值则返回 true。</summary>
             *
             * <returns>如果第一对标量值大于或等于第二对标量值则返回 true。</returns>
             *
             * <param name="pair1">第一对标量值。</param>
             * <param name="pair2">第二对标量值。</param>
             */
            public static bool operator >=(FloatPair pair1, FloatPair pair2)
            {
                return !(pair1 < pair2);
            }
        }

        /**
         * <summary>定义障碍物 k-D 树的节点。</summary>
         */
        private class ObstacleTreeNode
        {
            internal Obstacle obstacle_;
            internal ObstacleTreeNode left_;
            internal ObstacleTreeNode right_;
        };

        /**
         * <summary>智能体 k-D 树叶节点的最大大小。</summary>
         */
        private const int MAX_LEAF_SIZE = 10;

        private Agent[] agents_;
        private AgentTreeNode[] agentTree_;
        private ObstacleTreeNode obstacleTree_;

        /**
         * <summary>构建智能体 k-D 树。</summary>
         */
        internal void buildAgentTree()
        {
            if (agents_ == null || agents_.Length != simulator_.agents_.Count)
            {
                agents_ = new Agent[simulator_.agents_.Count];

                for (int i = 0; i < agents_.Length; ++i)
                {
                    agents_[i] = simulator_.agents_[i];
                }

                agentTree_ = new AgentTreeNode[2 * agents_.Length];

                for (int i = 0; i < agentTree_.Length; ++i)
                {
                    agentTree_[i] = new AgentTreeNode();
                }
            }

            if (agents_.Length != 0)
            {
                buildAgentTreeRecursive(0, agents_.Length, 0);
            }
        }

        /**
         * <summary>构建障碍物 k-D 树。</summary>
         */
        internal void buildObstacleTree()
        {
            obstacleTree_ = new ObstacleTreeNode();

            IList<Obstacle> obstacles = new List<Obstacle>(simulator_.obstacles_.Count);

            for (int i = 0; i < simulator_.obstacles_.Count; ++i)
            {
                obstacles.Add(simulator_.obstacles_[i]);
            }

            obstacleTree_ = buildObstacleTreeRecursive(obstacles);
        }

        /**
         * <summary>计算指定智能体的智能体邻居。</summary>
         *
         * <param name="agent">要计算智能体邻居的智能体。</param>
         * <param name="rangeSq">智能体周围的平方范围。</param>
         */
        internal void computeAgentNeighbors(Agent agent, ref zfloat rangeSq)
        {
            queryAgentTreeRecursive(agent, ref rangeSq, 0);
        }

        /**
         * <summary>计算指定智能体的障碍物邻居。</summary>
         *
         * <param name="agent">要计算障碍物邻居的智能体。</param>
         * <param name="rangeSq">智能体周围的平方范围。</param>
         */
        internal void computeObstacleNeighbors(Agent agent, zfloat rangeSq)
        {
            queryObstacleTreeRecursive(agent, rangeSq, obstacleTree_);
        }

        /**
         * <summary>查询指定半径内两点之间的可见性。</summary>
         *
         * <returns>如果 q1 和 q2 在半径内相互可见则返回 true；否则返回 false。</returns>
         *
         * <param name="q1">要测试可见性的第一个点。</param>
         * <param name="q2">要测试可见性的第二个点。</param>
         * <param name="radius">要测试可见性的半径。</param>
         */
        internal bool queryVisibility(Vector2 q1, Vector2 q2, zfloat radius)
        {
            return queryVisibilityRecursive(q1, q2, radius, obstacleTree_);
        }

        /**
         * <summary>构建智能体 k-D 树的递归方法。</summary>
         *
         * <param name="begin">智能体 k-D 树节点的起始索引。</param>
         * <param name="end">智能体 k-D 树节点的结束索引。</param>
         * <param name="node">当前智能体 k-D 树节点索引。</param>
         */
        private void buildAgentTreeRecursive(int begin, int end, int node)
        {
            agentTree_[node].begin_ = begin;
            agentTree_[node].end_ = end;
            agentTree_[node].minX_ = agentTree_[node].maxX_ = agents_[begin].position_.x_;
            agentTree_[node].minY_ = agentTree_[node].maxY_ = agents_[begin].position_.y_;

            for (int i = begin + 1; i < end; ++i)
            {
                agentTree_[node].maxX_ = zMathf.Max(agentTree_[node].maxX_, agents_[i].position_.x_);
                agentTree_[node].minX_ = zMathf.Min(agentTree_[node].minX_, agents_[i].position_.x_);
                agentTree_[node].maxY_ = zMathf.Max(agentTree_[node].maxY_, agents_[i].position_.y_);
                agentTree_[node].minY_ = zMathf.Min(agentTree_[node].minY_, agents_[i].position_.y_);
            }

            if (end - begin > MAX_LEAF_SIZE)
            {
                /* 非叶节点。 */
                bool isVertical = agentTree_[node].maxX_ - agentTree_[node].minX_ > agentTree_[node].maxY_ - agentTree_[node].minY_;
                zfloat splitValue = zfloat.Half * (isVertical ? agentTree_[node].maxX_ + agentTree_[node].minX_ : agentTree_[node].maxY_ + agentTree_[node].minY_);

                int left = begin;
                int right = end;

                while (left < right)
                {
                    while (left < right && (isVertical ? agents_[left].position_.x_ : agents_[left].position_.y_) < splitValue)
                    {
                        ++left;
                    }

                    while (right > left && (isVertical ? agents_[right - 1].position_.x_ : agents_[right - 1].position_.y_) >= splitValue)
                    {
                        --right;
                    }

                    if (left < right)
                    {
                        Agent tempAgent = agents_[left];
                        agents_[left] = agents_[right - 1];
                        agents_[right - 1] = tempAgent;
                        ++left;
                        --right;
                    }
                }

                int leftSize = left - begin;

                if (leftSize == 0)
                {
                    ++leftSize;
                    ++left;
                }

                agentTree_[node].left_ = node + 1;
                agentTree_[node].right_ = node + 2 * leftSize;

                buildAgentTreeRecursive(begin, left, agentTree_[node].left_);
                buildAgentTreeRecursive(left, end, agentTree_[node].right_);
            }
        }

        /**
         * <summary>构建障碍物 k-D 树的递归方法。</summary>
         *
         * <returns>障碍物 k-D 树节点。</returns>
         *
         * <param name="obstacles">障碍物列表。</param>
         */
        private ObstacleTreeNode buildObstacleTreeRecursive(IList<Obstacle> obstacles)
        {
            if (obstacles.Count == 0)
            {
                return null;
            }

            ObstacleTreeNode node = new();

            int optimalSplit = 0;
            int minLeft = obstacles.Count;
            int minRight = obstacles.Count;

            for (int i = 0; i < obstacles.Count; ++i)
            {
                int leftSize = 0;
                int rightSize = 0;

                Obstacle obstacleI1 = obstacles[i];
                Obstacle obstacleI2 = obstacleI1.next_;

                /* 计算最优分割节点。 */
                for (int j = 0; j < obstacles.Count; ++j)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    Obstacle obstacleJ1 = obstacles[j];
                    Obstacle obstacleJ2 = obstacleJ1.next_;

                    zfloat j1LeftOfI = RVOMath.leftOf(obstacleI1.point_, obstacleI2.point_, obstacleJ1.point_);
                    zfloat j2LeftOfI = RVOMath.leftOf(obstacleI1.point_, obstacleI2.point_, obstacleJ2.point_);

                    if (j1LeftOfI >= -RVOMath.RVO_EPSILON && j2LeftOfI >= -RVOMath.RVO_EPSILON)
                    {
                        ++leftSize;
                    }
                    else if (j1LeftOfI <= RVOMath.RVO_EPSILON && j2LeftOfI <= RVOMath.RVO_EPSILON)
                    {
                        ++rightSize;
                    }
                    else
                    {
                        ++leftSize;
                        ++rightSize;
                    }

                    if (new FloatPair((zfloat)Math.Max(leftSize, rightSize), (zfloat)Math.Min(leftSize, rightSize)) >= new FloatPair((zfloat)Math.Max(minLeft, minRight), (zfloat)Math.Min(minLeft, minRight)))
                    {
                        break;
                    }
                }

                if (new FloatPair((zfloat)Math.Max(leftSize, rightSize), (zfloat)Math.Min(leftSize, rightSize)) < new FloatPair((zfloat)Math.Max(minLeft, minRight), (zfloat)Math.Min(minLeft, minRight)))
                {
                    minLeft = leftSize;
                    minRight = rightSize;
                    optimalSplit = i;
                }
            }

            {
                /* 构建分割节点。 */
                IList<Obstacle> leftObstacles = new List<Obstacle>(minLeft);

                for (int n = 0; n < minLeft; ++n)
                {
                    leftObstacles.Add(null);
                }

                IList<Obstacle> rightObstacles = new List<Obstacle>(minRight);

                for (int n = 0; n < minRight; ++n)
                {
                    rightObstacles.Add(null);
                }

                int leftCounter = 0;
                int rightCounter = 0;
                int i = optimalSplit;

                Obstacle obstacleI1 = obstacles[i];
                Obstacle obstacleI2 = obstacleI1.next_;

                for (int j = 0; j < obstacles.Count; ++j)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    Obstacle obstacleJ1 = obstacles[j];
                    Obstacle obstacleJ2 = obstacleJ1.next_;

                    zfloat j1LeftOfI = RVOMath.leftOf(obstacleI1.point_, obstacleI2.point_, obstacleJ1.point_);
                    zfloat j2LeftOfI = RVOMath.leftOf(obstacleI1.point_, obstacleI2.point_, obstacleJ2.point_);

                    if (j1LeftOfI >= -RVOMath.RVO_EPSILON && j2LeftOfI >= -RVOMath.RVO_EPSILON)
                    {
                        leftObstacles[leftCounter++] = obstacles[j];
                    }
                    else if (j1LeftOfI <= RVOMath.RVO_EPSILON && j2LeftOfI <= RVOMath.RVO_EPSILON)
                    {
                        rightObstacles[rightCounter++] = obstacles[j];
                    }
                    else
                    {
                        /* 分割障碍物 j。 */
                        zfloat t = RVOMath.det(obstacleI2.point_ - obstacleI1.point_, obstacleJ1.point_ - obstacleI1.point_) / RVOMath.det(obstacleI2.point_ - obstacleI1.point_, obstacleJ1.point_ - obstacleJ2.point_);

                        Vector2 splitPoint = obstacleJ1.point_ + t * (obstacleJ2.point_ - obstacleJ1.point_);

                        Obstacle newObstacle = new();
                        newObstacle.point_ = splitPoint;
                        newObstacle.previous_ = obstacleJ1;
                        newObstacle.next_ = obstacleJ2;
                        newObstacle.convex_ = true;
                        newObstacle.direction_ = obstacleJ1.direction_;

                        newObstacle.id_ = simulator_.obstacles_.Count;

                        simulator_.obstacles_.Add(newObstacle);

                        obstacleJ1.next_ = newObstacle;
                        obstacleJ2.previous_ = newObstacle;

                        if (j1LeftOfI > zfloat.Zero)
                        {
                            leftObstacles[leftCounter++] = obstacleJ1;
                            rightObstacles[rightCounter++] = newObstacle;
                        }
                        else
                        {
                            rightObstacles[rightCounter++] = obstacleJ1;
                            leftObstacles[leftCounter++] = newObstacle;
                        }
                    }
                }

                node.obstacle_ = obstacleI1;
                node.left_ = buildObstacleTreeRecursive(leftObstacles);
                node.right_ = buildObstacleTreeRecursive(rightObstacles);

                return node;
            }
        }

        /**
         * <summary>计算指定智能体的智能体邻居的递归方法。</summary>
         *
         * <param name="agent">要计算智能体邻居的智能体。</param>
         * <param name="rangeSq">智能体周围的平方范围。</param>
         * <param name="node">当前智能体 k-D 树节点索引。</param>
         */
        private void queryAgentTreeRecursive(Agent agent, ref zfloat rangeSq, int node)
        {
            if (agentTree_[node].end_ - agentTree_[node].begin_ <= MAX_LEAF_SIZE)
            {
                for (int i = agentTree_[node].begin_; i < agentTree_[node].end_; ++i)
                {
                    agent.insertAgentNeighbor(agents_[i], ref rangeSq);
                }
            }
            else
            {
                zfloat distSqLeft = RVOMath.sqr(zMathf.Max(zfloat.Zero, agentTree_[agentTree_[node].left_].minX_ - agent.position_.x_)) + RVOMath.sqr(zMathf.Max(zfloat.Zero, agent.position_.x_ - agentTree_[agentTree_[node].left_].maxX_)) + RVOMath.sqr(zMathf.Max(zfloat.Zero, agentTree_[agentTree_[node].left_].minY_ - agent.position_.y_)) + RVOMath.sqr(zMathf.Max(zfloat.Zero, agent.position_.y_ - agentTree_[agentTree_[node].left_].maxY_));
                zfloat distSqRight = RVOMath.sqr(zMathf.Max(zfloat.Zero, agentTree_[agentTree_[node].right_].minX_ - agent.position_.x_)) + RVOMath.sqr(zMathf.Max(zfloat.Zero, agent.position_.x_ - agentTree_[agentTree_[node].right_].maxX_)) + RVOMath.sqr(zMathf.Max(zfloat.Zero, agentTree_[agentTree_[node].right_].minY_ - agent.position_.y_)) + RVOMath.sqr(zMathf.Max(zfloat.Zero, agent.position_.y_ - agentTree_[agentTree_[node].right_].maxY_));

                if (distSqLeft < distSqRight)
                {
                    if (distSqLeft < rangeSq)
                    {
                        queryAgentTreeRecursive(agent, ref rangeSq, agentTree_[node].left_);

                        if (distSqRight < rangeSq)
                        {
                            queryAgentTreeRecursive(agent, ref rangeSq, agentTree_[node].right_);
                        }
                    }
                }
                else
                {
                    if (distSqRight < rangeSq)
                    {
                        queryAgentTreeRecursive(agent, ref rangeSq, agentTree_[node].right_);

                        if (distSqLeft < rangeSq)
                        {
                            queryAgentTreeRecursive(agent, ref rangeSq, agentTree_[node].left_);
                        }
                    }
                }

            }
        }

        /**
         * <summary>计算指定智能体的障碍物邻居的递归方法。</summary>
         *
         * <param name="agent">要计算障碍物邻居的智能体。</param>
         * <param name="rangeSq">智能体周围的平方范围。</param>
         * <param name="node">当前障碍物 k-D 节点。</param>
         */
        private void queryObstacleTreeRecursive(Agent agent, zfloat rangeSq, ObstacleTreeNode node)
        {
            if (node != null)
            {
                Obstacle obstacle1 = node.obstacle_;
                Obstacle obstacle2 = obstacle1.next_;

                zfloat agentLeftOfLine = RVOMath.leftOf(obstacle1.point_, obstacle2.point_, agent.position_);

                queryObstacleTreeRecursive(agent, rangeSq, agentLeftOfLine >= zfloat.Zero ? node.left_ : node.right_);

                zfloat distSqLine = RVOMath.sqr(agentLeftOfLine) / RVOMath.absSq(obstacle2.point_ - obstacle1.point_);

                if (distSqLine < rangeSq)
                {
                    if (agentLeftOfLine < zfloat.Zero)
                    {
                        /*
                         * 仅当智能体在障碍物右侧（且能看到障碍物）时才尝试此节点的障碍物。
                         */
                        agent.insertObstacleNeighbor(node.obstacle_, rangeSq);
                    }

                    /* 尝试线的另一侧。 */
                    queryObstacleTreeRecursive(agent, rangeSq, agentLeftOfLine >= zfloat.Zero ? node.right_ : node.left_);
                }
            }
        }

        /**
         * <summary>查询指定半径内两点之间可见性的递归方法。</summary>
         *
         * <returns>如果 q1 和 q2 在半径内相互可见则返回 true；否则返回 false。</returns>
         *
         * <param name="q1">要测试可见性的第一个点。</param>
         * <param name="q2">要测试可见性的第二个点。</param>
         * <param name="radius">要测试可见性的半径。</param>
         * <param name="node">当前障碍物 k-D 节点。</param>
         */
        private bool queryVisibilityRecursive(Vector2 q1, Vector2 q2, zfloat radius, ObstacleTreeNode node)
        {
            if (node == null)
            {
                return true;
            }

            Obstacle obstacle1 = node.obstacle_;
            Obstacle obstacle2 = obstacle1.next_;

            zfloat q1LeftOfI = RVOMath.leftOf(obstacle1.point_, obstacle2.point_, q1);
            zfloat q2LeftOfI = RVOMath.leftOf(obstacle1.point_, obstacle2.point_, q2);
            zfloat invLengthI = zfloat.One / RVOMath.absSq(obstacle2.point_ - obstacle1.point_);

            if (q1LeftOfI >= zfloat.Zero && q2LeftOfI >= zfloat.Zero)
            {
                return queryVisibilityRecursive(q1, q2, radius, node.left_) && ((RVOMath.sqr(q1LeftOfI) * invLengthI >= RVOMath.sqr(radius) && RVOMath.sqr(q2LeftOfI) * invLengthI >= RVOMath.sqr(radius)) || queryVisibilityRecursive(q1, q2, radius, node.right_));
            }

            if (q1LeftOfI <= zfloat.Zero && q2LeftOfI <= zfloat.Zero)
            {
                return queryVisibilityRecursive(q1, q2, radius, node.right_) && ((RVOMath.sqr(q1LeftOfI) * invLengthI >= RVOMath.sqr(radius) && RVOMath.sqr(q2LeftOfI) * invLengthI >= RVOMath.sqr(radius)) || queryVisibilityRecursive(q1, q2, radius, node.left_));
            }

            if (q1LeftOfI >= zfloat.Zero && q2LeftOfI <= zfloat.Zero)
            {
                /* 可以从左到右看穿障碍物。 */
                return queryVisibilityRecursive(q1, q2, radius, node.left_) && queryVisibilityRecursive(q1, q2, radius, node.right_);
            }

            zfloat point1LeftOfQ = RVOMath.leftOf(q1, q2, obstacle1.point_);
            zfloat point2LeftOfQ = RVOMath.leftOf(q1, q2, obstacle2.point_);
            zfloat invLengthQ = zfloat.One / RVOMath.absSq(q2 - q1);

            return point1LeftOfQ * point2LeftOfQ >= zfloat.Zero && RVOMath.sqr(point1LeftOfQ) * invLengthQ > RVOMath.sqr(radius) && RVOMath.sqr(point2LeftOfQ) * invLengthQ > RVOMath.sqr(radius) && queryVisibilityRecursive(q1, q2, radius, node.left_) && queryVisibilityRecursive(q1, q2, radius, node.right_);
        }
    }
}
