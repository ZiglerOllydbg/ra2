/*
 * Agent.cs
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
     * <summary>定义模拟中的智能体。</summary>
     */
    internal class Agent
    {
        internal Simulator simulator_; // 持有Simulator引用
        internal IList<KeyValuePair<zfloat, Agent>> agentNeighbors_ = new List<KeyValuePair<zfloat, Agent>>();
        internal IList<KeyValuePair<zfloat, Obstacle>> obstacleNeighbors_ = new List<KeyValuePair<zfloat, Obstacle>>();
        internal IList<Line> orcaLines_ = new List<Line>();
        internal Vector2 position_;
        internal Vector2 prefVelocity_;
        internal Vector2 velocity_;
        internal int id_ = 0;
        internal int maxNeighbors_ = 0;
        internal zfloat maxSpeed_ = zfloat.Zero;
        internal zfloat neighborDist_ = zfloat.Zero;
        internal zfloat radius_ = zfloat.Zero;
        internal zfloat timeHorizon_ = zfloat.Zero;
        internal zfloat timeHorizonObst_ = zfloat.Zero;

        private Vector2 newVelocity_;

        /**
         * <summary>计算此智能体的邻居。</summary>
         */
        internal void computeNeighbors()
        {
            obstacleNeighbors_.Clear();
            zfloat rangeSq = RVOMath.sqr(timeHorizonObst_ * maxSpeed_ + radius_);
            simulator_.kdTree_.computeObstacleNeighbors(this, rangeSq);

            agentNeighbors_.Clear();

            if (maxNeighbors_ > 0)
            {
                rangeSq = RVOMath.sqr(neighborDist_);
                simulator_.kdTree_.computeAgentNeighbors(this, ref rangeSq);
            }
        }

        /**
         * <summary>计算此智能体的新速度。</summary>
         */
        internal void computeNewVelocity()
        {
            orcaLines_.Clear();

            zfloat invTimeHorizonObst = zfloat.One / timeHorizonObst_;

            /* 创建障碍物 ORCA 线。 */
            for (int i = 0; i < obstacleNeighbors_.Count; ++i)
            {

                Obstacle obstacle1 = obstacleNeighbors_[i].Value;
                Obstacle obstacle2 = obstacle1.next_;

                Vector2 relativePosition1 = obstacle1.point_ - position_;
                Vector2 relativePosition2 = obstacle2.point_ - position_;

                /*
                 * 检查障碍物的速度障碍是否已被之前构建的障碍物 ORCA 线处理。
                 */
                bool alreadyCovered = false;

                for (int j = 0; j < orcaLines_.Count; ++j)
                {
                    if (RVOMath.det(invTimeHorizonObst * relativePosition1 - orcaLines_[j].point, orcaLines_[j].direction) - invTimeHorizonObst * radius_ >= -RVOMath.RVO_EPSILON && RVOMath.det(invTimeHorizonObst * relativePosition2 - orcaLines_[j].point, orcaLines_[j].direction) - invTimeHorizonObst * radius_ >= -RVOMath.RVO_EPSILON)
                    {
                        alreadyCovered = true;

                        break;
                    }
                }

                if (alreadyCovered)
                {
                    continue;
                }

                /* 尚未覆盖。检查碰撞。 */
                zfloat distSq1 = RVOMath.absSq(relativePosition1);
                zfloat distSq2 = RVOMath.absSq(relativePosition2);

                zfloat radiusSq = RVOMath.sqr(radius_);

                Vector2 obstacleVector = obstacle2.point_ - obstacle1.point_;
                zfloat s = (-relativePosition1 * obstacleVector) / RVOMath.absSq(obstacleVector);
                zfloat distSqLine = RVOMath.absSq(-relativePosition1 - s * obstacleVector);

                Line line;

                if (s < zfloat.Zero && distSq1 <= radiusSq)
                {
                    /* 与左顶点碰撞。如果非凸则忽略。 */
                    if (obstacle1.convex_)
                    {
                        line.point = new Vector2(zfloat.Zero, zfloat.Zero);
                        line.direction = RVOMath.normalize(new Vector2(-relativePosition1.y(), relativePosition1.x()));
                        orcaLines_.Add(line);
                    }

                    continue;
                }
                else if (s > zfloat.One && distSq2 <= radiusSq)
                {
                    /*
                     * 与右顶点碰撞。如果非凸或将被相邻障碍物处理则忽略。
                     */
                    if (obstacle2.convex_ && RVOMath.det(relativePosition2, obstacle2.direction_) >= zfloat.Zero)
                    {
                        line.point = new Vector2(zfloat.Zero, zfloat.Zero);
                        line.direction = RVOMath.normalize(new Vector2(-relativePosition2.y(), relativePosition2.x()));
                        orcaLines_.Add(line);
                    }

                    continue;
                }
                else if (s >= zfloat.Zero && s <= zfloat.One && distSqLine <= radiusSq)
                {
                    /* 与障碍物线段碰撞。 */
                    line.point = new Vector2(zfloat.Zero, zfloat.Zero);
                    line.direction = -obstacle1.direction_;
                    orcaLines_.Add(line);

                    continue;
                }

                /*
                 * No collision. Compute legs. When obliquely viewed, both legs
                 * can come from a single vertex. Legs extend cut-off line when
                 * non-convex vertex.
                 */

                Vector2 leftLegDirection, rightLegDirection;

                if (s < zfloat.Zero && distSqLine <= radiusSq)
                {
                    /*
                     * 障碍物被斜向观察，因此左顶点定义速度障碍。
                     */
                    if (!obstacle1.convex_)
                    {
                        /* 忽略障碍物。 */
                        continue;
                    }

                    obstacle2 = obstacle1;

                    zfloat leg1 = RVOMath.sqrt(distSq1 - radiusSq);
                    leftLegDirection = new Vector2(relativePosition1.x() * leg1 - relativePosition1.y() * radius_, relativePosition1.x() * radius_ + relativePosition1.y() * leg1) / distSq1;
                    rightLegDirection = new Vector2(relativePosition1.x() * leg1 + relativePosition1.y() * radius_, -relativePosition1.x() * radius_ + relativePosition1.y() * leg1) / distSq1;
                }
                else if (s > zfloat.One && distSqLine <= radiusSq)
                {
                    /*
                     * 障碍物被斜向观察，因此右顶点定义速度障碍。
                     */
                    if (!obstacle2.convex_)
                    {
                        /* 忽略障碍物。 */
                        continue;
                    }

                    obstacle1 = obstacle2;

                    zfloat leg2 = RVOMath.sqrt(distSq2 - radiusSq);
                    leftLegDirection = new Vector2(relativePosition2.x() * leg2 - relativePosition2.y() * radius_, relativePosition2.x() * radius_ + relativePosition2.y() * leg2) / distSq2;
                    rightLegDirection = new Vector2(relativePosition2.x() * leg2 + relativePosition2.y() * radius_, -relativePosition2.x() * radius_ + relativePosition2.y() * leg2) / distSq2;
                }
                else
                {
                    /* 通常情况。 */
                    if (obstacle1.convex_)
                    {
                        zfloat leg1 = RVOMath.sqrt(distSq1 - radiusSq);
                        leftLegDirection = new Vector2(relativePosition1.x() * leg1 - relativePosition1.y() * radius_, relativePosition1.x() * radius_ + relativePosition1.y() * leg1) / distSq1;
                    }
                    else
                    {
                        /* 左顶点非凸；左边延伸截止线。 */
                        leftLegDirection = -obstacle1.direction_;
                    }

                    if (obstacle2.convex_)
                    {
                        zfloat leg2 = RVOMath.sqrt(distSq2 - radiusSq);
                        rightLegDirection = new Vector2(relativePosition2.x() * leg2 + relativePosition2.y() * radius_, -relativePosition2.x() * radius_ + relativePosition2.y() * leg2) / distSq2;
                    }
                    else
                    {
                        /* 右顶点非凸；右边延伸截止线。 */
                        rightLegDirection = obstacle1.direction_;
                    }
                }

                /*
                 * 当顶点为凸时，边永远不能指向相邻边，而是采用相邻边的截止线。
                 * 如果速度投影到"外部"边上，则不添加约束。
                 */

                Obstacle leftNeighbor = obstacle1.previous_;

                bool isLeftLegForeign = false;
                bool isRightLegForeign = false;

                if (obstacle1.convex_ && RVOMath.det(leftLegDirection, -leftNeighbor.direction_) >= zfloat.Zero)
                {
                    /* 左边指向障碍物。 */
                    leftLegDirection = -leftNeighbor.direction_;
                    isLeftLegForeign = true;
                }

                if (obstacle2.convex_ && RVOMath.det(rightLegDirection, obstacle2.direction_) <= zfloat.Zero)
                {
                    /* 右边指向障碍物。 */
                    rightLegDirection = obstacle2.direction_;
                    isRightLegForeign = true;
                }

                /* 计算截止中心。 */
                Vector2 leftCutOff = invTimeHorizonObst * (obstacle1.point_ - position_);
                Vector2 rightCutOff = invTimeHorizonObst * (obstacle2.point_ - position_);
                Vector2 cutOffVector = rightCutOff - leftCutOff;

                /* 将当前速度投影到速度障碍上。 */

                /* 检查当前速度是否投影到截止圆上。 */
                zfloat t = obstacle1 == obstacle2 ? zfloat.Half : ((velocity_ - leftCutOff) * cutOffVector) / RVOMath.absSq(cutOffVector);
                zfloat tLeft = (velocity_ - leftCutOff) * leftLegDirection;
                zfloat tRight = (velocity_ - rightCutOff) * rightLegDirection;

                if ((t < zfloat.Zero && tLeft < zfloat.Zero) || (obstacle1 == obstacle2 && tLeft < zfloat.Zero && tRight < zfloat.Zero))
                {
                    /* 投影到左截止圆上。 */
                    Vector2 unitW = RVOMath.normalize(velocity_ - leftCutOff);

                    line.direction = new Vector2(unitW.y(), -unitW.x());
                    line.point = leftCutOff + radius_ * invTimeHorizonObst * unitW;
                    orcaLines_.Add(line);

                    continue;
                }
                else if (t > zfloat.One && tRight < zfloat.Zero)
                {
                    /* 投影到右截止圆上。 */
                    Vector2 unitW = RVOMath.normalize(velocity_ - rightCutOff);

                    line.direction = new Vector2(unitW.y(), -unitW.x());
                    line.point = rightCutOff + radius_ * invTimeHorizonObst * unitW;
                    orcaLines_.Add(line);

                    continue;
                }

                /*
                 * 投影到左边、右边或截止线上，选择最接近速度的。
                 */
                zfloat distSqCutoff = (t < zfloat.Zero || t > zfloat.One || obstacle1 == obstacle2) ? zfloat.Infinity : RVOMath.absSq(velocity_ - (leftCutOff + t * cutOffVector));
                zfloat distSqLeft = tLeft < zfloat.Zero ? zfloat.Infinity : RVOMath.absSq(velocity_ - (leftCutOff + tLeft * leftLegDirection));
                zfloat distSqRight = tRight < zfloat.Zero ? zfloat.Infinity : RVOMath.absSq(velocity_ - (rightCutOff + tRight * rightLegDirection));

                if (distSqCutoff <= distSqLeft && distSqCutoff <= distSqRight)
                {
                    /* 投影到截止线上。 */
                    line.direction = -obstacle1.direction_;
                    line.point = leftCutOff + radius_ * invTimeHorizonObst * new Vector2(-line.direction.y(), line.direction.x());
                    orcaLines_.Add(line);

                    continue;
                }

                if (distSqLeft <= distSqRight)
                {
                    /* 投影到左边上。 */
                    if (isLeftLegForeign)
                    {
                        continue;
                    }

                    line.direction = leftLegDirection;
                    line.point = leftCutOff + radius_ * invTimeHorizonObst * new Vector2(-line.direction.y(), line.direction.x());
                    orcaLines_.Add(line);

                    continue;
                }

                /* 投影到右边上。 */
                if (isRightLegForeign)
                {
                    continue;
                }

                line.direction = -rightLegDirection;
                line.point = rightCutOff + radius_ * invTimeHorizonObst * new Vector2(-line.direction.y(), line.direction.x());
                orcaLines_.Add(line);
            }

            int numObstLines = orcaLines_.Count;

            zfloat invTimeHorizon = zfloat.One / timeHorizon_;

            /* 创建智能体 ORCA 线。 */
            for (int i = 0; i < agentNeighbors_.Count; ++i)
            {
                Agent other = agentNeighbors_[i].Value;

                Vector2 relativePosition = other.position_ - position_;
                Vector2 relativeVelocity = velocity_ - other.velocity_;
                zfloat distSq = RVOMath.absSq(relativePosition);
                zfloat combinedRadius = radius_ + other.radius_;
                zfloat combinedRadiusSq = RVOMath.sqr(combinedRadius);

                Line line;
                Vector2 u;

                if (distSq > combinedRadiusSq)
                {
                    /* 无碰撞。 */
                    Vector2 w = relativeVelocity - invTimeHorizon * relativePosition;

                    /* 从截止中心到相对速度的向量。 */
                    zfloat wLengthSq = RVOMath.absSq(w);
                    zfloat dotProduct1 = w * relativePosition;

                    if (dotProduct1 < zfloat.Zero && RVOMath.sqr(dotProduct1) > combinedRadiusSq * wLengthSq)
                    {
                        /* 投影到截止圆上。 */
                        zfloat wLength = RVOMath.sqrt(wLengthSq);
                        Vector2 unitW = w / wLength;

                        line.direction = new Vector2(unitW.y(), -unitW.x());
                        u = (combinedRadius * invTimeHorizon - wLength) * unitW;
                    }
                    else
                    {
                        /* 投影到边上。 */
                        zfloat leg = RVOMath.sqrt(distSq - combinedRadiusSq);

                        if (RVOMath.det(relativePosition, w) > zfloat.Zero)
                        {
                            /* 投影到左边上。 */
                            line.direction = new Vector2(relativePosition.x() * leg - relativePosition.y() * combinedRadius, relativePosition.x() * combinedRadius + relativePosition.y() * leg) / distSq;
                        }
                        else
                        {
                            /* 投影到右边上。 */
                            line.direction = -new Vector2(relativePosition.x() * leg + relativePosition.y() * combinedRadius, -relativePosition.x() * combinedRadius + relativePosition.y() * leg) / distSq;
                        }

                        zfloat dotProduct2 = relativeVelocity * line.direction;
                        u = dotProduct2 * line.direction - relativeVelocity;
                    }
                }
                else
                {
                    /* 碰撞。投影到时间步长的截止圆上。 */
                    zfloat invTimeStep = zfloat.One / simulator_.timeStep_;

                    /* 从截止中心到相对速度的向量。 */
                    Vector2 w = relativeVelocity - invTimeStep * relativePosition;

                    zfloat wLength = RVOMath.abs(w);
                    Vector2 unitW = w / wLength;

                    line.direction = new Vector2(unitW.y(), -unitW.x());
                    u = (combinedRadius * invTimeStep - wLength) * unitW;
                }

                line.point = velocity_ + zfloat.Half * u;
                orcaLines_.Add(line);
            }

            int lineFail = linearProgram2(orcaLines_, maxSpeed_, prefVelocity_, false, ref newVelocity_);

            if (lineFail < orcaLines_.Count)
            {
                linearProgram3(orcaLines_, numObstLines, lineFail, maxSpeed_, ref newVelocity_);
            }
        }

        /**
         * <summary>将智能体邻居插入到此智能体的邻居集合中。</summary>
         *
         * <param name="agent">要插入的智能体的指针。</param>
         * <param name="rangeSq">此智能体周围的平方范围。</param>
         */
        internal void insertAgentNeighbor(Agent agent, ref zfloat rangeSq)
        {
            if (this != agent)
            {
                zfloat distSq = RVOMath.absSq(position_ - agent.position_);

                if (distSq < rangeSq)
                {
                    if (agentNeighbors_.Count < maxNeighbors_)
                    {
                        agentNeighbors_.Add(new KeyValuePair<zfloat, Agent>(distSq, agent));
                    }

                    int i = agentNeighbors_.Count - 1;

                    while (i != 0 && distSq < agentNeighbors_[i - 1].Key)
                    {
                        agentNeighbors_[i] = agentNeighbors_[i - 1];
                        --i;
                    }

                    agentNeighbors_[i] = new KeyValuePair<zfloat, Agent>(distSq, agent);

                    if (agentNeighbors_.Count == maxNeighbors_)
                    {
                        rangeSq = agentNeighbors_[agentNeighbors_.Count - 1].Key;
                    }
                }
            }
        }

        /**
         * <summary>将静态障碍物邻居插入到此智能体的邻居集合中。</summary>
         *
         * <param name="obstacle">要插入的静态障碍物的编号。</param>
         * <param name="rangeSq">此智能体周围的平方范围。</param>
         */
        internal void insertObstacleNeighbor(Obstacle obstacle, zfloat rangeSq)
        {
            Obstacle nextObstacle = obstacle.next_;

            zfloat distSq = RVOMath.distSqPointLineSegment(obstacle.point_, nextObstacle.point_, position_);

            if (distSq < rangeSq)
            {
                obstacleNeighbors_.Add(new KeyValuePair<zfloat, Obstacle>(distSq, obstacle));

                int i = obstacleNeighbors_.Count - 1;

                while (i != 0 && distSq < obstacleNeighbors_[i - 1].Key)
                {
                    obstacleNeighbors_[i] = obstacleNeighbors_[i - 1];
                    --i;
                }
                obstacleNeighbors_[i] = new KeyValuePair<zfloat, Obstacle>(distSq, obstacle);
            }
        }

        /**
         * <summary>更新此智能体的二维位置和二维速度。</summary>
         */
        internal void update()
        {
            velocity_ = newVelocity_;
            position_ += velocity_ * simulator_.timeStep_;
        }

        /**
         * <summary>在指定线上求解一维线性规划，受由线定义的线性约束和圆形约束限制。</summary>
         *
         * <returns>如果成功则返回 true。</returns>
         *
         * <param name="lines">定义线性约束的线。</param>
         * <param name="lineNo">指定的线约束。</param>
         * <param name="radius">圆形约束的半径。</param>
         * <param name="optVelocity">优化速度。</param>
         * <param name="directionOpt">如果应优化方向则为 true。</param>
         * <param name="result">线性规划结果的引用。</param>
         */
        private bool linearProgram1(IList<Line> lines, int lineNo, zfloat radius, Vector2 optVelocity, bool directionOpt, ref Vector2 result)
        {
            zfloat dotProduct = lines[lineNo].point * lines[lineNo].direction;
            zfloat discriminant = RVOMath.sqr(dotProduct) + RVOMath.sqr(radius) - RVOMath.absSq(lines[lineNo].point);

            if (discriminant < zfloat.Zero)
            {
                /* 最大速度圆完全使 lineNo 线无效。 */
                return false;
            }

            zfloat sqrtDiscriminant = RVOMath.sqrt(discriminant);
            zfloat tLeft = -dotProduct - sqrtDiscriminant;
            zfloat tRight = -dotProduct + sqrtDiscriminant;

            for (int i = 0; i < lineNo; ++i)
            {
                zfloat denominator = RVOMath.det(lines[lineNo].direction, lines[i].direction);
                zfloat numerator = RVOMath.det(lines[i].direction, lines[lineNo].point - lines[i].point);

                if (RVOMath.fabs(denominator) <= RVOMath.RVO_EPSILON)
                {
                    /* 线 lineNo 和 i 是（几乎）平行的。 */
                    if (numerator < zfloat.Zero)
                    {
                        return false;
                    }

                    continue;
                }

                zfloat t = numerator / denominator;

                if (denominator >= zfloat.Zero)
                {
                    /* 线 i 在右侧限制线 lineNo。 */
                    tRight = zMathf.Min(tRight, t);
                }
                else
                {
                    /* 线 i 在左侧限制线 lineNo。 */
                    tLeft = zMathf.Max(tLeft, t);
                }

                if (tLeft > tRight)
                {
                    return false;
                }
            }

            if (directionOpt)
            {
                /* 优化方向。 */
                if (optVelocity * lines[lineNo].direction > zfloat.Zero)
                {
                    /* 取右极值。 */
                    result = lines[lineNo].point + tRight * lines[lineNo].direction;
                }
                else
                {
                    /* 取左极值。 */
                    result = lines[lineNo].point + tLeft * lines[lineNo].direction;
                }
            }
            else
            {
                /* 优化最近点。 */
                zfloat t = lines[lineNo].direction * (optVelocity - lines[lineNo].point);

                if (t < tLeft)
                {
                    result = lines[lineNo].point + tLeft * lines[lineNo].direction;
                }
                else if (t > tRight)
                {
                    result = lines[lineNo].point + tRight * lines[lineNo].direction;
                }
                else
                {
                    result = lines[lineNo].point + t * lines[lineNo].direction;
                }
            }

            return true;
        }

        /**
         * <summary>Solves a two-dimensional linear program subject to linear
         * constraints defined by lines and a circular constraint.</summary>
         *
         * <returns>The number of the line it fails on, and the number of lines
         * if successful.</returns>
         *
         * <param name="lines">Lines defining the linear constraints.</param>
         * <param name="radius">The radius of the circular constraint.</param>
         * <param name="optVelocity">The optimization velocity.</param>
         * <param name="directionOpt">True if the direction should be optimized.
         * </param>
         * <param name="result">A reference to the result of the linear program.
         * </param>
         */
        private int linearProgram2(IList<Line> lines, zfloat radius, Vector2 optVelocity, bool directionOpt, ref Vector2 result)
        {
            if (directionOpt)
            {
                /*
                 * 优化方向。注意在这种情况下优化速度为单位长度。
                 */
                result = optVelocity * radius;
            }
            else if (RVOMath.absSq(optVelocity) > RVOMath.sqr(radius))
            {
                /* 优化最近点并在圆外。 */
                result = RVOMath.normalize(optVelocity) * radius;
            }
            else
            {
                /* 优化最近点并在圆内。 */
                result = optVelocity;
            }

            for (int i = 0; i < lines.Count; ++i)
            {
                if (RVOMath.det(lines[i].direction, lines[i].point - result) > zfloat.Zero)
                {
                    /* 结果不满足约束 i。计算新的最优结果。 */
                    Vector2 tempResult = result;
                    if (!linearProgram1(lines, i, radius, optVelocity, directionOpt, ref result))
                    {
                        result = tempResult;

                        return i;
                    }
                }
            }

            return lines.Count;
        }

        /**
         * <summary>求解二维线性规划，受由线定义的线性约束和圆形约束限制。</summary>
         *
         * <param name="lines">定义线性约束的线。</param>
         * <param name="numObstLines">障碍物线的数量。</param>
         * <param name="beginLine">二维线性规划失败的线。</param>
         * <param name="radius">圆形约束的半径。</param>
         * <param name="result">线性规划结果的引用。</param>
         */
        private void linearProgram3(IList<Line> lines, int numObstLines, int beginLine, zfloat radius, ref Vector2 result)
        {
            zfloat distance = zfloat.Zero;

            for (int i = beginLine; i < lines.Count; ++i)
            {
                if (RVOMath.det(lines[i].direction, lines[i].point - result) > distance)
                {
                    /* 结果不满足线 i 的约束。 */
                    IList<Line> projLines = new List<Line>();
                    for (int ii = 0; ii < numObstLines; ++ii)
                    {
                        projLines.Add(lines[ii]);
                    }

                    for (int j = numObstLines; j < i; ++j)
                    {
                        Line line;

                        zfloat determinant = RVOMath.det(lines[i].direction, lines[j].direction);

                        if (RVOMath.fabs(determinant) <= RVOMath.RVO_EPSILON)
                        {
                            /* 线 i 和线 j 平行。 */
                            if (lines[i].direction * lines[j].direction > zfloat.Zero)
                            {
                                /* 线 i 和线 j 指向同一方向。 */
                                continue;
                            }
                            else
                            {
                                /* 线 i 和线 j 指向相反方向。 */
                                line.point = zfloat.Half * (lines[i].point + lines[j].point);
                            }
                        }
                        else
                        {
                            line.point = lines[i].point + (RVOMath.det(lines[j].direction, lines[i].point - lines[j].point) / determinant) * lines[i].direction;
                        }

                        line.direction = RVOMath.normalize(lines[j].direction - lines[i].direction);
                        projLines.Add(line);
                    }

                    Vector2 tempResult = result;
                    if (linearProgram2(projLines, radius, new Vector2(-lines[i].direction.y(), lines[i].direction.x()), true, ref result) < projLines.Count)
                    {
                        /*
                         * 原则上这不应该发生。根据定义，结果已经在此线性规划的可行区域内。
                         * 如果失败，是由于小的浮点误差，保留当前结果。
                         */
                        result = tempResult;
                    }

                    distance = RVOMath.det(lines[i].direction, lines[i].point - result);
                }
            }
        }
    }
}
