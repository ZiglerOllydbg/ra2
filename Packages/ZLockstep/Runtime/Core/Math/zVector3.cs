using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace zUnity
{
	/// <summary>
	/// 三维向量结构体，用于确定性计算
	/// 包含x、y、z三个分量，每个分量都是zfloat类型
	/// 提供了向量运算的各种方法，如加减乘除、点积、叉积、归一化等
	/// </summary>
	[Serializable]
	public struct zVector3 : ISerializable
	{
		public static readonly zVector3 zero = new zVector3((zfloat)0, (zfloat)0, (zfloat)0);
		public static readonly zVector3 one = new zVector3((zfloat)1, (zfloat)1, (zfloat)1);
		public static readonly zVector3 forward = new zVector3((zfloat)0, (zfloat)0, (zfloat)1);
		public static readonly zVector3 back = new zVector3((zfloat)0, (zfloat)0, (zfloat)(-1));
		public static readonly zVector3 up = new zVector3((zfloat)0, (zfloat)1, (zfloat)0);
		public static readonly zVector3 down = new zVector3((zfloat)0, (zfloat)(-1), (zfloat)0);
		public static readonly zVector3 left = new zVector3((zfloat)(-1), (zfloat)0, (zfloat)0);
		public static readonly zVector3 right = new zVector3((zfloat)1, (zfloat)0, (zfloat)0);

        public static readonly zVector3 NULL = new zVector3((zfloat)(-999999), (zfloat)(-999999), (zfloat)(-999999));

		public bool IsZero()
		{
			return this.x.value == 0 && this.y.value == 0 && this.z.value == 0;
		}

		public zVector3(zfloat x, zfloat y, zfloat z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public zVector3(int x, int y, int z)
		{
			this.x = (zfloat)x;
			this.y = (zfloat)y;
			this.z = (zfloat)z;
		}

		public zVector3(zVector3 vec)
		{
			this.x = vec.x;
			this.y = vec.y;
			this.z = vec.z;
		}

		/// <summary>
		/// X轴坐标分量
		/// </summary>
		[JsonProperty]
		public zfloat x;
		
		/// <summary>
		/// Y轴坐标分量
		/// </summary>
		[JsonProperty]
		public zfloat y;

		/// <summary>
		/// Z轴坐标分量
		/// </summary>
		[JsonProperty]
		public zfloat z;

		public zfloat this[int index]
		{
			get
			{
				if (index == 0)
				{
					return x;
				}
				else if (index == 1)
				{
					return y;
				}
				else if (index == 2)
				{
					return z;
				}
				else
				{
					throw new IndexOutOfRangeException("zVector3 Only Contains x,y,z，so the index must use 0,1 or 2 !");
				}
				return zfloat.Zero;
			}

			set
			{
				if (index == 0)
				{
					x = value;
				}
				else if (index == 1)
				{
					y = value;
				}
				else if (index == 2)
				{
					z = value;
				}
				else
				{
					throw new IndexOutOfRangeException("zVector3 Only Contains x,y,z，so the index must use 0,1 or 2 !");
				}
			}
		}



		public void Add(ref zVector3 vec)
		{
			x.value += vec.x.value;
			y.value += vec.y.value;
			z.value += vec.z.value;
		}

		public void Sub(ref zVector3 vec)
		{
			x.value -= vec.x.value;
			y.value -= vec.y.value;
			z.value -= vec.z.value;
		}

		public void Mul(ref zVector3 vec)
		{
			x.value = x.value * vec.x.value / zfloat.SCALE_10000;
			y.value = y.value * vec.y.value / zfloat.SCALE_10000;
			z.value = z.value * vec.z.value / zfloat.SCALE_10000;
		}

		public void Div(ref zVector3 vec)
		{
			x.value = x.value * zfloat.SCALE_10000 / vec.x.value;
			y.value = y.value * zfloat.SCALE_10000 / vec.y.value;
			z.value = z.value * zfloat.SCALE_10000 / vec.z.value;
		}

		//	private zVector3 normalized;

		/// <summary>
		/// 获取归一化向量，注意每次调用，都会重新归一化。
		/// </summary>
		[JsonIgnore]
		public zVector3 normalized
		{
			get
			{
				return zVector3.Normalize(ref this);
			}
		}

		public zVector3 GetNormalizedForMagnitude(zfloat __magnitude)
		{
			zVector3 vec = this;

			if (__magnitude > 0)
			{
				vec.x.value = vec.x.value * zfloat.SCALE_10000 / __magnitude.value;
				vec.y.value = vec.y.value * zfloat.SCALE_10000 / __magnitude.value;
				vec.z.value = vec.z.value * zfloat.SCALE_10000 / __magnitude.value;
				return vec;
			}
			else
			{
				return zVector3.zero;
			}
		}

		[JsonIgnore]
		public zVector3 approxNormalizedXZ
		{
			get
			{
				zVector3 vec;
				vec.y.value = 0;

				zfloat xzLen = zMathf.ApproximateHypotenuse(x, z);
				if (xzLen.value == 0)
				{
					return zVector3.zero;
				}
				vec.x.value = x.value * zfloat.SCALE_10000 / xzLen.value;
				vec.z.value = z.value * zfloat.SCALE_10000 / xzLen.value;

				return vec;
			}
		}

		public zVector3 GetApproxNormalizedXZForMagnitude(zfloat __magnitude)
		{
				zVector3 vec;
				vec.y.value = 0;

				//zfloat xzLen = zMathf.ApproximateHypotenuse(x, z);
				if (__magnitude.value == 0)
				{
					return zVector3.zero;
				}
				vec.x.value = x.value * zfloat.SCALE_10000 / __magnitude.value;
				vec.z.value = z.value * zfloat.SCALE_10000 / __magnitude.value;

				return vec;
		}

		/// <summary>
		/// 向量长度的平方
		/// </summary>
		public zfloat sqrMagnitude
		{
			get
			{
				return zVector3.SqrMagnitude(ref this);
			}
		}


		public zfloat magnitude
		{
			get { return zVector3.Magnitude(ref this); }

		}

		/// <summary>
		/// 归一化
		/// </summary>
		/// <returns></returns>
		public void Normalize()
		{
			/*zfloat num = zVector3.Magnitude(ref this);

			zVector3 a = v3 * zfloat.SCALE_100;
			zfloat f1;
			f1.value = zMathf.Sqrt(zfloat.CreateFloat((a.x.value * a.x.value + a.y.value * a.y.value + a.z.value * a.z.value) / zfloat.SCALE_10000)).value / zfloat.SCALE_100;*/

			long num = (x.value * x.value + y.value * y.value + z.value * z.value) / zfloat.SCALE_10000;
            num = zMathf.SqrtScale(num);

			if (num > 0)
			{
				x.value = x.value * zfloat.SCALE_10000 / num;
				y.value = y.value * zfloat.SCALE_10000 / num;
				z.value = z.value * zfloat.SCALE_10000 / num;
			}
			else
			{
				x.value = 0;
				y.value = 0;
				z.value = 0;
			}
		}

		/// <summary>
		/// 归一化
		/// </summary>
		/// <param name="vec">ref传入只是为了节省性能，不会修改vec的值</param>
		/// <returns></returns>
		public static zVector3 Normalize(ref zVector3 __vec)
		{
			zVector3 vec = __vec;
			long num = (vec.x.value * vec.x.value + vec.y.value * vec.y.value + vec.z.value * vec.z.value)/zfloat.SCALE_10000;
            num = zMathf.SqrtScale(num);

			if (num > 0)
			{
				vec.x.value = vec.x.value * zfloat.SCALE_10000 / num;
				vec.y.value = vec.y.value * zfloat.SCALE_10000 / num;
				vec.z.value = vec.z.value * zfloat.SCALE_10000 / num;
				return vec;
			}
			else
			{
				return zVector3.zero;
			}
			//zfloat num = zVector3.Magnitude(ref vec);
			//if (num > 0)
			//{
			//	return vec / num;
			//}
			//return zVector3.zero;

		}

		/// <summary>
		/// 向量长度
		/// </summary>
		/// <param name="a"></param>
		/// <returns></returns>
		public static zfloat Magnitude(ref zVector3 v3)
		{

			zVector3 a = v3 * zfloat.SCALE_100;
			zfloat f1;
			long lsqr = (a.x.value * a.x.value + a.y.value * a.y.value + a.z.value * a.z.value);
			f1.value = zMathf.Sqrt(zfloat.CreateFloat(lsqr / zfloat.SCALE_10000)).value / zfloat.SCALE_100;
			
			return f1;

			//return zMathf.Sqrt(v3.x * v3.x + v3.y * v3.y + v3.z * v3.z);
		}

		/// <summary>
		/// 将向量按照scale进行缩放。即当前向量与scale对应位相乘
		/// </summary>
		/// <param name="scale"></param>
		public void Scale(zVector3 scale)
		{
            x.value = x.value * scale.x.value / zfloat.SCALE_10000;
            y.value = y.value * scale.y.value / zfloat.SCALE_10000;
            z.value = z.value * scale.z.value / zfloat.SCALE_10000;
		}

		/// <summary>
		/// 向量a长度的平方
		/// </summary>
		/// <param name="a"></param>
		/// <returns></returns>
		public static zfloat SqrMagnitude(ref zVector3 a)
		{
			zfloat f;
			f.value = (a.x.value * a.x.value + a.y.value * a.y.value + a.z.value * a.z.value) / zfloat.SCALE_10000;
			return f;
		}

		/// <summary>
		/// 向量A旋转到向量B的夹角
		/// 以右手系为基准，逆时针为正数，顺时针为负数
		/// </summary>
		/// <param name="A"></param>
		/// <param name="B"></param>
		/// <returns></returns>
		public static zfloat A2B_angle(zVector3 A, zVector3 B)
		{
			zfloat angle;
            angle.value = zMathf.Rad2Deg.value * zMathf.Acos(zVector3.Dot(ref A, ref B)).value / zfloat.SCALE_10000;
			//确定旋转方向
			if (zVector3.Cross(A, B).y.value < 0)
			{
				angle = -angle;
			}
			return angle;
		}

		/// <summary>
		/// 按照数字t在from到to之间插值。
		/// value = from + t * to
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="t">应在0~1之间，超过该范围，会自动规范到该范围</param>
		/// <returns></returns>
		public static zVector3 Lerp(zVector3 from, zVector3 to, zfloat t)
		{
			t = zMathf.Clamp01(t);
			return new zVector3(from.x + (to.x - from.x) * t, from.y + (to.y - from.y) * t, from.z + (to.z - from.z) * t);
		}

		/// <summary>
		/// 球面插值  TODO TODO 这个似乎BUG很严重，先不要用了
		///                    sin((1-t)θ)           sin(tθ)
		/// Slerp(p0,p1,t) = --------------- p0 + --------------- p1
		///                        sinθ               sinθ
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static zVector3 Slerp(zVector3 from, zVector3 to, zfloat t)
		{
			t = zMathf.Clamp01(t);
			zVector3 fromNormal = from.normalized;
			zVector3 toNormal = to.normalized;
			zfloat cos = zVector3.Dot(ref fromNormal, ref toNormal);
			zfloat dis = 1 - cos;
			if (dis.Approximate(zfloat.One))
			{
				return to;
			}
			else
			{
				zfloat angle = zMathf.Acos(cos);
				return zMathf.Sin((zfloat.One - t) * angle) / zMathf.Sin(angle) * from + zMathf.Sin(t * angle) / zMathf.Sin(angle) * to;
			}
		}

		/// <summary>
		/// TODO TODO #########还没有实现#########
		/// normal和tangent分别归一化
		/// 并且调整tangent使垂直于normal
		/// </summary>
		/// <param name="normal"></param>
		/// <param name="tangent"></param>
		/// <returns></returns>
		public static void OrthoNormalize(ref zVector3 normal, ref zVector3 tangent)
		{
			normal.Normalize();
			//  tangent.Normalize();
			zVector3 bn = zVector3.Cross(normal, tangent);
			tangent = zVector3.Cross(bn, normal);
			tangent.Normalize();
		}

		/// <summary>
		/// 当前的地点移向目标
		/// 这个函数基本上和Vector3.Lerp相同，而是该函数将确保我们的速度不会超过maxDistanceDelta。
		/// maxDistanceDelta的负值从目标推开向量，就是说maxDistanceDelta是正值，当前地点移向目标，如果是负值当前地点将远离目标。
		/// </summary>
		/// <param name="current"></param>
		/// <param name="target"></param>
		/// <param name="maxDistanceDelta"></param>
		/// <returns></returns>
		public static zVector3 MoveTowards(zVector3 current, zVector3 target, zfloat maxDistanceDelta)
		{
			zVector3 a = target - current;
			zfloat magnitude = a.magnitude;
			if (magnitude <= maxDistanceDelta || magnitude == zfloat.Zero)
			{
				return target;
			}
			return current + a / magnitude * maxDistanceDelta;
		}

		/// <summary>
		/// TODO TODO #########还没有实现#########
		/// </summary>
		/// <param name="current"></param>
		/// <param name="target"></param>
		/// <param name="maxMagnitudeDelta"></param>
		/// <returns></returns>
		public static zVector3 RotateTowards(zVector3 current, zVector3 target, zfloat maxMagnitudeDelta)
		{
			return zero;
		}

		/// <summary>
		/// 随着时间的推移，逐渐改变一个向量朝向预期的目标。
		/// 没动啥意思，直接从unity的反编译抄过来了。
		/// </summary>
		/// <param name="current"></param>
		/// <param name="target"></param>
		/// <param name="currentVelocity"></param>
		/// <param name="smoothTime"></param>
		/// <param name="maxSpeed"></param>
		/// <param name="deltaTime"></param>
		/// <returns></returns>
		public static zVector3 SmoothDamp(zVector3 current, zVector3 target, ref zVector3 currentVelocity, zfloat smoothTime, zfloat maxSpeed, zfloat deltaTime)
		{
			smoothTime = zMathf.Max(new zfloat(0, 1), smoothTime);
			zfloat num = zfloat.Two / smoothTime;
			zfloat num2 = num * deltaTime;
			zfloat d = zfloat.One / (zfloat.One + num2 + new zfloat(0, 4800) * num2 * num2 + new zfloat(0, 2350) * num2 * num2 * num2);
			zVector3 vector = current - target;
			zVector3 vector2 = target;
			zfloat maxLength = maxSpeed * smoothTime;
			vector = zVector3.ClampMagnitude(vector, maxLength);
			target = current - vector;
			zVector3 vector3 = (currentVelocity + num * vector) * deltaTime;
			currentVelocity = (currentVelocity - num * vector3) * d;
			zVector3 vector4 = target + (vector + vector3) * d;
			zVector3 v2ToCurr = vector2 - current;
			zVector3 v4Tov2 = vector4 - vector2;
			if (zVector3.Dot(ref v2ToCurr, ref v4Tov2) > zfloat.Zero)
			{
				vector4 = vector2;
				currentVelocity = (vector4 - vector2) / deltaTime;
			}
			return vector4;
		}

		/// <summary>
		/// 两个Vector对应相乘
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static zVector3 Scale(zVector3 a, zVector3 b)
		{
            zVector3 vec;
            vec.x.value = a.x.value * b.x.value / zfloat.SCALE_10000;
            vec.y.value = a.x.value * b.y.value / zfloat.SCALE_10000;
            vec.z.value = a.x.value * b.z.value / zfloat.SCALE_10000;
			return vec;
		}


		/// <summary>
		/// 叉乘  向量积
		/// </summary>
		/// <param name="lhs"></param>
		/// <param name="rhs"></param>
		/// <returns></returns>
		public static zVector3 Cross(zVector3 lhs, zVector3 rhs)
		{
            zVector3 vec;
            vec.x.value = (lhs.y.value * rhs.z.value - lhs.z.value * rhs.y.value) / zfloat.SCALE_10000;
            vec.y.value = (lhs.z.value * rhs.x.value - lhs.x.value * rhs.z.value) / zfloat.SCALE_10000;
            vec.z.value = (lhs.x.value * rhs.y.value - lhs.y.value * rhs.x.value) / zfloat.SCALE_10000;

            return vec;
			//return new zVector3(lhs.y * rhs.z - lhs.z * rhs.y, lhs.z * rhs.x - lhs.x * rhs.z, lhs.x * rhs.y - lhs.y * rhs.x);
		}

		/// <summary>
		/// 反射向量
		/// </summary>
		/// <param name="inDir"></param>
		/// <param name="inNormal"></param>
		/// <returns></returns>
		public static zVector3 Reflect(zVector3 inDir, zVector3 inNormal)
		{
			//return -zfloat.Two * zVector3.Dot(ref inNormal, ref inDir) * inNormal + inDir;
            long lTempNum;
            zVector3 vec;
            lTempNum = -2 * zVector3.Dot(ref inNormal, ref inDir).value;
            vec.x.value = lTempNum * inNormal.x.value / zfloat.SCALE_10000 + inDir.x.value;
            vec.y.value = lTempNum * inNormal.y.value / zfloat.SCALE_10000 + inDir.y.value;
            vec.z.value = lTempNum * inNormal.z.value / zfloat.SCALE_10000 + inDir.z.value;
            return vec;
		}

		/// <summary>
		/// 点积  数量积
		/// </summary>
		/// <param name="lhs"></param>
		/// <param name="rhs"></param>
		/// <returns></returns>
		public static zfloat Dot(ref zVector3 lhs, ref zVector3 rhs)
		{
            zfloat num;
            num.value = (lhs.x.value * rhs.x.value + lhs.y.value * rhs.y.value + lhs.z.value * rhs.z.value) / zfloat.SCALE_10000;
            return num;
			//return zfloat.CreateFloat((lhs.x.value * rhs.x.value + lhs.y.value * rhs.y.value + lhs.z.value * rhs.z.value)/zfloat.SCALE_10000);
		}

		public void Set(zfloat new_x, zfloat new_y, zfloat new_z)
		{
			this.x.value = new_x.value;
            this.y.value = new_y.value;
            this.z.value = new_z.value;
		}


		/// <summary>
		/// 将vec投影到onNormal
		///       /|
		///      / |
		///     /  |
		///  v /   |
		///   /    |
		///  /_____|
		///     p(方向n)
		/// n▪v.normal = cosθ
		/// |v|*cosθ = |p|
		/// p = |p|*n
		/// </summary>
		/// <param name="vec"></param>
		/// <param name="onNormal"></param>
		/// <returns></returns>
		public static zVector3 Project(zVector3 vector, zVector3 onNormal)
		{
			//我的写法
			/* zfloat num = vector.magnitude;
			 if (num == zfloat.Zero)
			 {
				 return zVector3.zero;
			 }
			 return onNormal * (zVector3.Dot(vector.normalized, onNormal.normalized) * num);*/

			//官方的反编译写法，公式和自己写的不太一样，猜测应该是一种优化，绕开了向量规范化。没有推导过。
			long lNorDotNor = (onNormal.x.value * onNormal.x.value + onNormal.y.value * onNormal.y.value + onNormal.z.value * onNormal.z.value) / zfloat.SCALE_10000;
			//zfloat num = zVector3.Dot(ref onNormal, ref onNormal);
			if (lNorDotNor == 0)
			{
				return zVector3.zero;
			}
			long lVecDotNor = (vector.x.value * onNormal.x.value + vector.y.value * onNormal.y.value + vector.z.value * onNormal.z.value) / zfloat.SCALE_10000;
			onNormal.x.value = onNormal.x.value * lVecDotNor / lNorDotNor;
			onNormal.y.value = onNormal.y.value * lVecDotNor / lNorDotNor;
			onNormal.z.value = onNormal.z.value * lVecDotNor / lNorDotNor;
			return onNormal;

		}

		/// <summary>
		/// 两向量的夹角
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <returns></returns>
		public static zfloat Angle(zVector3 from, zVector3 to)
		{
            //from.Normalize();
            //to.Normalize();
            //return zMathf.Acos(zMathf.Clamp(zVector3.Dot(ref from, ref to), zfloat.NegativeOne, zfloat.One)) * zMathf.Rad2Deg;

            from.Normalize();
            to.Normalize();
            zfloat tempNum;
            tempNum.value = zMathf.Acos(zMathf.Clamp(zVector3.Dot(ref from, ref to), zfloat.NegativeOne, zfloat.One)).value * zMathf.Rad2Deg.value / zfloat.SCALE_10000;
            return tempNum;
        }

		/// <summary>
		/// 两点间距离
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static zfloat Distance(zVector3 a, zVector3 b)
		{
          //  zVector3 vector = a - b;
           // return zMathf.Sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);

            zVector3 vector;
            vector.x.value = a.x.value - b.x.value;
            vector.y.value = a.y.value - b.y.value;
            vector.z.value = a.z.value - b.z.value;
            long lNum = (vector.x.value * vector.x.value + vector.y.value * vector.y.value + vector.z.value * vector.z.value) / zfloat.SCALE_10000;

            lNum = zMathf.SqrtScale(lNum);

            //if (lNum > 100000000)
            //{
            //    lNum = zMathf.Sqrt(lNum) * zfloat.SCALE_100;
            //}
            //else
            //{
            //    lNum *= zfloat.SCALE_10000;
            //    lNum = zMathf.Sqrt(lNum);
            //}
            zfloat fNum;
            fNum.value = lNum;
            return fNum;
		}

		/// <summary>
		/// 限制向量长度，如果长度超过maxLength则截取，否则原样返回
		/// </summary>
		/// <param name="vector"></param>
		/// <param name="maxLength"></param>
		/// <returns></returns>
		public static zVector3 ClampMagnitude(zVector3 vector, zfloat maxLength)
		{
			if (vector.sqrMagnitude > maxLength * maxLength)
			{
				return vector.normalized * maxLength;
			}
			return vector;
		}

		public static zVector3 Min(zVector3 lhs, zVector3 rhs)
		{
			return new zVector3(zMathf.Min(lhs.x, rhs.x), zMathf.Min(lhs.y, rhs.y), zMathf.Min(lhs.z, rhs.z));
		}

		public static zVector3 Max(zVector3 lhs, zVector3 rhs)
		{
			return new zVector3(zMathf.Max(lhs.x, rhs.x), zMathf.Max(lhs.y, rhs.y), zMathf.Max(lhs.z, rhs.z));
		}

		#region 加法
		public static zVector3 operator +(zVector3 lhs, zVector3 rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.x.value + rhs.x.value;
            vec.y.value = lhs.y.value + rhs.y.value;
            vec.z.value = lhs.z.value + rhs.z.value;
			return vec;
		}

		public static zVector3 operator +(int lhs, zVector3 rhs)
		{
            zVector3 vec;
            vec.x.value = lhs * zfloat.SCALE_10000 + rhs.x.value;
            vec.y.value = lhs * zfloat.SCALE_10000 + rhs.y.value;
            vec.z.value = lhs * zfloat.SCALE_10000 + rhs.z.value;
            return vec;
			//return new zVector3(lhs + rhs.x, lhs + rhs.y, lhs + rhs.z);
		}

		public static zVector3 operator +(zVector3 lhs, int rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.x.value + rhs * zfloat.SCALE_10000;
            vec.y.value = lhs.y.value + rhs * zfloat.SCALE_10000;
            vec.z.value = lhs.z.value + rhs * zfloat.SCALE_10000;
            return vec;
			//return new zVector3(lhs.x + rhs, lhs.y + rhs, lhs.z + rhs);
		}

		public static zVector3 operator +(zfloat lhs, zVector3 rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.value + rhs.x.value;
            vec.y.value = lhs.value + rhs.y.value;
            vec.z.value = lhs.value + rhs.z.value;
            return vec;
			//return new zVector3(lhs + rhs.x, lhs + rhs.y, lhs + rhs.z);
		}

		public static zVector3 operator +(zVector3 lhs, zfloat rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.x.value + rhs.value;
            vec.y.value = lhs.y.value + rhs.value;
            vec.z.value = lhs.z.value + rhs.value;
            return vec;
			//return new zVector3(lhs.x + rhs, lhs.y + rhs, lhs.z + rhs);
		}
		#endregion

		#region 减法
		public static zVector3 operator -(zVector3 lhs, zVector3 rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.x.value - rhs.x.value;
            vec.y.value = lhs.y.value - rhs.y.value;
            vec.z.value = lhs.z.value - rhs.z.value;
            return vec;
			//return new zVector3(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);
		}
		public static zVector3 operator -(int lhs, zVector3 rhs)
		{
            zVector3 vec;
            vec.x.value = lhs * zfloat.SCALE_10000 - rhs.x.value;
            vec.y.value = lhs * zfloat.SCALE_10000 - rhs.y.value;
            vec.z.value = lhs * zfloat.SCALE_10000 - rhs.z.value;
            return vec;
			//return new zVector3(lhs - rhs.x, lhs - rhs.y, lhs - rhs.z);
		}
		public static zVector3 operator -(zVector3 lhs, int rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.x.value - rhs * zfloat.SCALE_10000;
            vec.y.value = lhs.y.value - rhs * zfloat.SCALE_10000;
            vec.z.value = lhs.z.value - rhs * zfloat.SCALE_10000;
            return vec;

			//return new zVector3(lhs.x - rhs, lhs.y - rhs, lhs.z - rhs);
		}
		public static zVector3 operator -(zfloat lhs, zVector3 rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.value - rhs.x.value;
            vec.y.value = lhs.value - rhs.y.value;
            vec.z.value = lhs.value - rhs.z.value;
            return vec;
			//return new zVector3(lhs - rhs.x, lhs - rhs.y, lhs - rhs.z);
		}
		public static zVector3 operator -(zVector3 lhs, zfloat rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.x.value - rhs.value;
            vec.y.value = lhs.y.value - rhs.value;
            vec.z.value = lhs.z.value - rhs.value;
            return vec;

			//return new zVector3(lhs.x - rhs, lhs.y - rhs, lhs.z - rhs);
		}
		#endregion

		#region 负号
		public static zVector3 operator -(zVector3 a)
		{
            zVector3 vec;
            vec.x.value = -a.x.value;
            vec.y.value = -a.y.value;
            vec.z.value = -a.z.value;
            return vec;
			//return new zVector3(-a.x, -a.y, -a.z);
		}

		#endregion

		#region 乘法
		public static zVector3 operator *(int lhs, zVector3 rhs)
		{
            zVector3 vec;
            vec.x.value = lhs * rhs.x.value;
            vec.y.value = lhs * rhs.y.value;
            vec.z.value = lhs * rhs.z.value;

            return vec;
            
            
            //return new zVector3(lhs * rhs.x, lhs * rhs.y, lhs * rhs.z);
		}
		public static zVector3 operator *(zVector3 lhs, int rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.x.value * rhs;
            vec.y.value = lhs.y.value * rhs;
            vec.z.value = lhs.z.value * rhs;

            return vec;

			//return new zVector3(lhs.x * rhs, lhs.y * rhs, lhs.z * rhs);
		}
		public static zVector3 operator *(zfloat lhs, zVector3 rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.value * rhs.x.value / zfloat.SCALE_10000;
            vec.y.value = lhs.value * rhs.y.value / zfloat.SCALE_10000;
            vec.z.value = lhs.value * rhs.z.value / zfloat.SCALE_10000;

            return vec;

			//return new zVector3(lhs * rhs.x, lhs * rhs.y, lhs * rhs.z);
		}
		public static zVector3 operator *(zVector3 lhs, zfloat rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.x.value * rhs.value / zfloat.SCALE_10000;
            vec.y.value = lhs.y.value * rhs.value / zfloat.SCALE_10000;
            vec.z.value = lhs.z.value * rhs.value / zfloat.SCALE_10000;

            return vec;

			//return new zVector3(lhs.x * rhs, lhs.y * rhs, lhs.z * rhs);
		}
		#endregion

		#region 除法
		public static zVector3 operator /(zVector3 lhs, int rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.x.value / rhs;
            vec.y.value = lhs.y.value / rhs;
            vec.z.value = lhs.z.value / rhs;
            return vec;

			//return new zVector3(lhs.x / rhs, lhs.y / rhs, lhs.z / rhs);
		}
		public static zVector3 operator /(zVector3 lhs, zfloat rhs)
		{
            zVector3 vec;
            vec.x.value = lhs.x.value * zfloat.SCALE_10000 / rhs.value;
            vec.y.value = lhs.y.value * zfloat.SCALE_10000 / rhs.value;
            vec.z.value = lhs.z.value * zfloat.SCALE_10000 / rhs.value;
            return vec;

			//return new zVector3(lhs.x / rhs, lhs.y / rhs, lhs.z / rhs);
		}
		#endregion

		#region 相等判定
		public static bool operator !=(zVector3 lhs, zVector3 rhs)
		{
			return (lhs.x.value != rhs.x.value) || (lhs.y.value != rhs.y.value) || (lhs.z.value != rhs.z.value);
		}

		public static bool operator ==(zVector3 lhs, zVector3 rhs)
		{
			return (lhs.x.value == rhs.x.value) && (lhs.y.value == rhs.y.value) && (lhs.z.value == rhs.z.value);
		}
		#endregion


		public override string ToString()
		{
			return ("(" + x + " , " + y + " , " + z + ")");
		}

        /// <summary>
        /// 实现ISerializable接口的序列化方法
        /// </summary>
        /// <param name="info">序列化信息</param>
        /// <param name="context">流上下文</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("x", x.value);
            info.AddValue("y", y.value);
            info.AddValue("z", z.value);
        }
        
        /// <summary>
        /// 反序列化构造函数
        /// </summary>
        /// <param name="info">序列化信息</param>
        /// <param name="context">流上下文</param>
        public zVector3(SerializationInfo info, StreamingContext context)
        {
            x = zfloat.CreateFloat(info.GetInt64("x"));
            y = zfloat.CreateFloat(info.GetInt64("y"));
            z = zfloat.CreateFloat(info.GetInt64("z"));
        }
    }
}