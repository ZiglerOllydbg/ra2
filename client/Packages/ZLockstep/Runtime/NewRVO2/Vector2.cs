/*
 * Vector2.cs
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
     * <summary>定义二维向量（使用定点数 zVector2）。</summary>
     */
    public struct Vector2
    {
        internal zfloat x_;
        internal zfloat y_;

        /**
         * <summary>从指定的 xy 坐标构造并初始化二维向量。</summary>
         *
         * <param name="x">二维向量的 x 坐标。</param>
         * <param name="y">二维向量的 y 坐标。</param>
         */
        public Vector2(zfloat x, zfloat y)
        {
            x_ = x;
            y_ = y;
        }

        /**
         * <summary>从 zVector2 构造 Vector2。</summary>
         */
        public Vector2(zVector2 vec)
        {
            x_ = vec.x;
            y_ = vec.y;
        }

        /**
         * <summary>转换为 zVector2。</summary>
         */
        public zVector2 ToZVector2()
        {
            return new zVector2(x_, y_);
        }

        /**
         * <summary>返回此向量的字符串表示。</summary>
         *
         * <returns>此向量的字符串表示。</returns>
         */
        public override string ToString()
        {
            return "(" + x_.ToString() + "," + y_.ToString() + ")";
        }

        /**
         * <summary>返回此二维向量的 x 坐标。</summary>
         *
         * <returns>二维向量的 x 坐标。</returns>
         */
        public zfloat x()
        {
            return x_;
        }

        /**
         * <summary>返回此二维向量的 y 坐标。</summary>
         *
         * <returns>二维向量的 y 坐标。</returns>
         */
        public zfloat y()
        {
            return y_;
        }

        /**
         * <summary>计算两个指定二维向量的点积。</summary>
         *
         * <returns>两个指定二维向量的点积。</returns>
         *
         * <param name="vector1">第一个二维向量。</param>
         * <param name="vector2">第二个二维向量。</param>
         */
        public static zfloat operator *(Vector2 vector1, Vector2 vector2)
        {
            return vector1.x_ * vector2.x_ + vector1.y_ * vector2.y_;
        }

        /**
         * <summary>计算指定二维向量与指定标量值的标量乘法。</summary>
         *
         * <returns>指定二维向量与指定标量值的标量乘法。</returns>
         *
         * <param name="scalar">标量值。</param>
         * <param name="vector">二维向量。</param>
         */
        public static Vector2 operator *(zfloat scalar, Vector2 vector)
        {
            return vector * scalar;
        }

        /**
         * <summary>计算指定二维向量与指定标量值的标量乘法。</summary>
         *
         * <returns>指定二维向量与指定标量值的标量乘法。</returns>
         *
         * <param name="vector">二维向量。</param>
         * <param name="scalar">标量值。</param>
         */
        public static Vector2 operator *(Vector2 vector, zfloat scalar)
        {
            return new Vector2(vector.x_ * scalar, vector.y_ * scalar);
        }

        /**
         * <summary>计算指定二维向量与指定标量值的标量除法。</summary>
         *
         * <returns>指定二维向量与指定标量值的标量除法。</returns>
         *
         * <param name="vector">二维向量。</param>
         * <param name="scalar">标量值。</param>
         */
        public static Vector2 operator /(Vector2 vector, zfloat scalar)
        {
            return new Vector2(vector.x_ / scalar, vector.y_ / scalar);
        }

        /**
         * <summary>计算两个指定二维向量的向量和。</summary>
         *
         * <returns>两个指定二维向量的向量和。</returns>
         *
         * <param name="vector1">第一个二维向量。</param>
         * <param name="vector2">第二个二维向量。</param>
         */
        public static Vector2 operator +(Vector2 vector1, Vector2 vector2)
        {
            return new Vector2(vector1.x_ + vector2.x_, vector1.y_ + vector2.y_);
        }

        /**
         * <summary>计算两个指定二维向量的向量差。</summary>
         *
         * <returns>两个指定二维向量的向量差。</returns>
         *
         * <param name="vector1">第一个二维向量。</param>
         * <param name="vector2">第二个二维向量。</param>
         */
        public static Vector2 operator -(Vector2 vector1, Vector2 vector2)
        {
            return new Vector2(vector1.x_ - vector2.x_, vector1.y_ - vector2.y_);
        }

        /**
         * <summary>计算指定二维向量的取反。</summary>
         *
         * <returns>指定二维向量的取反。</returns>
         *
         * <param name="vector">二维向量。</param>
         */
        public static Vector2 operator -(Vector2 vector)
        {
            return new Vector2(-vector.x_, -vector.y_);
        }
    }
}
