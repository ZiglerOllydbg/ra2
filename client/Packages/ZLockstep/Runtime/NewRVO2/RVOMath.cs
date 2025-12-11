/*
 * RVOMath.cs
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

using zUnity;

namespace RVO
{
    /**
     * <summary>包含在多个类中使用的函数和常量。</summary>
     */
    public struct RVOMath
    {
        /**
         * <summary>一个足够小的正数。</summary>
         */
        internal static readonly zfloat RVO_EPSILON = zfloat.CreateFloat(1); // 0.00001

        /**
         * <summary>计算指定二维向量的长度。</summary>
         *
         * <param name="vector">要计算长度的二维向量。</param>
         * <returns>二维向量的长度。</returns>
         */
        public static zfloat abs(Vector2 vector)
        {
            return sqrt(absSq(vector));
        }

        /**
         * <summary>计算指定二维向量的平方长度。</summary>
         *
         * <returns>二维向量的平方长度。</returns>
         *
         * <param name="vector">要计算平方长度的二维向量。</param>
         */
        public static zfloat absSq(Vector2 vector)
        {
            return vector * vector;
        }

        /**
         * <summary>计算指定二维向量的归一化。</summary>
         *
         * <returns>二维向量的归一化。</returns>
         *
         * <param name="vector">要计算归一化的二维向量。</param>
         */
        public static Vector2 normalize(Vector2 vector)
        {
            zfloat len = abs(vector);
            if (len > zfloat.Zero)
            {
                return vector / len;
            }
            return new Vector2(zfloat.Zero, zfloat.Zero);
        }

        /**
         * <summary>计算由指定二维向量作为行组成的二维方阵的行列式。</summary>
         *
         * <returns>二维方阵的行列式。</returns>
         *
         * <param name="vector1">二维方阵的顶行。</param>
         * <param name="vector2">二维方阵的底行。</param>
         */
        internal static zfloat det(Vector2 vector1, Vector2 vector2)
        {
            return vector1.x_ * vector2.y_ - vector1.y_ * vector2.x_;
        }

        /**
         * <summary>计算从具有指定端点的线段到指定点的平方距离。</summary>
         *
         * <returns>从线段到点的平方距离。</returns>
         *
         * <param name="vector1">线段的第一个端点。</param>
         * <param name="vector2">线段的第二个端点。</param>
         * <param name="vector3">要计算平方距离的点。</param>
         */
        internal static zfloat distSqPointLineSegment(Vector2 vector1, Vector2 vector2, Vector2 vector3)
        {
            zfloat r = ((vector3 - vector1) * (vector2 - vector1)) / absSq(vector2 - vector1);

            if (r < zfloat.Zero)
            {
                return absSq(vector3 - vector1);
            }

            if (r > zfloat.One)
            {
                return absSq(vector3 - vector2);
            }

            return absSq(vector3 - (vector1 + r * (vector2 - vector1)));
        }

        /**
         * <summary>计算定点数的绝对值。</summary>
         *
         * <returns>定点数的绝对值。</returns>
         *
         * <param name="scalar">要计算绝对值的定点数。</param>
         */
        internal static zfloat fabs(zfloat scalar)
        {
            return zMathf.Abs(scalar);
        }

        /**
         * <summary>计算从连接指定点的线到指定点的有符号距离。</summary>
         *
         * <returns>当点 c 位于线 ab 左侧时为正。</returns>
         *
         * <param name="a">线上的第一个点。</param>
         * <param name="b">线上的第二个点。</param>
         * <param name="c">要计算有符号距离的点。</param>
         */
        internal static zfloat leftOf(Vector2 a, Vector2 b, Vector2 c)
        {
            return det(a - c, b - a);
        }

        /**
         * <summary>计算定点数的平方。</summary>
         *
         * <returns>定点数的平方。</returns>
         *
         * <param name="scalar">要计算平方的定点数。</param>
         */
        internal static zfloat sqr(zfloat scalar)
        {
            return scalar * scalar;
        }

        /**
         * <summary>计算定点数的平方根。</summary>
         *
         * <returns>定点数的平方根。</returns>
         *
         * <param name="scalar">要计算平方根的定点数。</param>
         */
        internal static zfloat sqrt(zfloat scalar)
        {
            return zMathf.Sqrt(scalar);
        }
    }
}
