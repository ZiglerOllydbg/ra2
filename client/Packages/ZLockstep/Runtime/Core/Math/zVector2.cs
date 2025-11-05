using System;
using System.Runtime.Serialization;
using Newtonsoft.Json; // 添加序列化命名空间

namespace zUnity
{
	public struct zVector2 : ISerializable // 实现ISerializable接口
	{
		public static readonly zVector2 zero = new zVector2((zfloat)0, (zfloat)0);
		public static readonly zVector2 one = new zVector2((zfloat)1, (zfloat)1);
		public static readonly zVector2 up = new zVector2((zfloat)0, (zfloat)1);
		public static readonly zVector2 down = new zVector2((zfloat)0, (zfloat)(-1));
		public static readonly zVector2 left = new zVector2((zfloat)(-1), (zfloat)0);
		public static readonly zVector2 right = new zVector2((zfloat)1, (zfloat)0);

		public zVector2(zfloat x, zfloat y)
		{
			this.x = x;
			this.y = y;
		}

		public zVector2(int x, int y)
		{
			this.x = (zfloat)x;
			this.y = (zfloat)y;
		}

		public zVector2(zVector2 vec)
		{
			this.x = vec.x;
			this.y = vec.y;
		}
		
		// 添加反序列化构造函数
        public zVector2(SerializationInfo info, StreamingContext context)
        {
            x = zfloat.CreateFloat(info.GetInt64("x"));
            y = zfloat.CreateFloat(info.GetInt64("y"));
        }

		public zfloat x;
		public zfloat y;


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
				else
				{
					throw new IndexOutOfRangeException("zVector2 Only Contains x,y，so the index must use 0 or 1 !");
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
				else
				{
					throw new IndexOutOfRangeException("zVector2 Only Contains x,y，so the index must use 0 or 1 !");
				}
			}
		}


		//	private zVector2 normalized;
		[JsonIgnore]
		public zVector2 normalized
		{
			get
			{
				return zVector2.Normalize(ref this);
			}
		}

		public zfloat sqrMagnitude
		{
			get
			{
				return zVector2.SqrMagnitude(ref this);
			}
		}


		public zfloat magnitude
		{
			get { return zVector2.Magnitude(ref this); }

		}

		public void Normalize()
		{
			zfloat num = zVector2.Magnitude(ref this);
			if (num > 0)
			{
				x /= num;
				y /= num;
			}
			else
			{
				x = zfloat.Zero;
				y = zfloat.Zero;
			}

		}

		public static zVector2 Normalize(ref zVector2 vec)
		{
			zfloat num = zVector2.Magnitude(ref vec);
			if (num > 0)
			{
				return vec / num;
			}
			return zVector2.zero;

		}

		public static zfloat Magnitude(ref zVector2 a)
		{
			return zMathf.Sqrt(a.x * a.x + a.y * a.y);
		}

		public void Scale(zVector2 scale)
		{
			x *= scale.x;
			y *= scale.y;
		}

		public static zfloat SqrMagnitude(ref zVector2 a)
		{
			return a.x * a.x + a.y * a.y;
		}

		public static zVector2 Lerp(zVector2 from, zVector2 to, zfloat t)
		{
			t = zMathf.Clamp01(t);
			return new zVector2(from.x + (to.x - from.x) * t, from.y + (to.y - from.y) * t);
		}

		public static zVector2 MoveTowards(zVector2 current, zVector2 target, zfloat maxDistanceDelta)
		{
			zVector2 a = target - current;
			zfloat magnitude = a.magnitude;
			if (magnitude <= maxDistanceDelta || magnitude == zfloat.Zero)
			{
				return target;
			}
			return current + a / magnitude * maxDistanceDelta;
		}

		/// <summary>
		/// 两个Vector对应相乘
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static zVector2 Scale(zVector2 a, zVector2 b)
		{
			return new zVector2(a.x * b.x, a.y * b.y);
		}

		public static zVector3 Cross(zVector2 lhs, zVector2 rhs)
		{
			return new zVector3(zfloat.Zero, zfloat.Zero, lhs.x * rhs.y - lhs.y * rhs.x);
		}

		public static zfloat Dot(zVector2 lhs, zVector2 rhs)
		{
			return lhs.x * rhs.x + lhs.y * rhs.y;
		}

		public static zVector2 Project(zVector2 vector, zVector2 onNormal)
		{
			zfloat num = zVector2.Dot(onNormal, onNormal);
			if (num == zfloat.Zero)
			{
				return zVector2.zero;
			}
			return onNormal * zVector2.Dot(vector, onNormal) / num;
		}

		public static zfloat Angle(zVector2 from, zVector2 to)
		{
			return zMathf.Acos(zMathf.Clamp(zVector2.Dot(from.normalized, to.normalized), zfloat.NegativeOne, zfloat.One));
		}

		public static zfloat Distance(zVector2 a, zVector2 b)
		{
			return (a - b).magnitude;
		}

		public static zVector2 ClampMagnitude(zVector2 vector, zfloat maxLength)
		{
			if (vector.sqrMagnitude > maxLength * maxLength)
			{
				return vector.normalized * maxLength;
			}
			return vector;
		}

		public static zVector2 Min(zVector2 lhs, zVector2 rhs)
		{
			return new zVector2(zMathf.Min(lhs.x, rhs.x), zMathf.Min(lhs.y, rhs.y));
		}

		public static zVector2 Max(zVector2 lhs, zVector2 rhs)
		{
			return new zVector2(zMathf.Max(lhs.x, rhs.x), zMathf.Max(lhs.y, rhs.y));
		}

		#region 加法
		public static zVector2 operator +(zVector2 lhs, zVector2 rhs)
		{
			return new zVector2(lhs.x + rhs.x, lhs.y + rhs.y);
		}

		public static zVector2 operator +(int lhs, zVector2 rhs)
		{
			return new zVector2(lhs + rhs.x, lhs + rhs.y);
		}

		public static zVector2 operator +(zVector2 lhs, int rhs)
		{
			return new zVector2(lhs.x + rhs, lhs.y + rhs);
		}

		public static zVector2 operator +(zfloat lhs, zVector2 rhs)
		{
			return new zVector2(lhs + rhs.x, lhs + rhs.y);
		}

		public static zVector2 operator +(zVector2 lhs, zfloat rhs)
		{
			return new zVector2(lhs.x + rhs, lhs.y + rhs);
		}
		#endregion

		#region 减法
		public static zVector2 operator -(zVector2 lhs, zVector2 rhs)
		{
			return new zVector2(lhs.x - rhs.x, lhs.y - rhs.y);
		}
		public static zVector2 operator -(int lhs, zVector2 rhs)
		{
			return new zVector2(lhs - rhs.x, lhs - rhs.y);
		}
		public static zVector2 operator -(zVector2 lhs, int rhs)
		{
			return new zVector2(lhs.x - rhs, lhs.y - rhs);
		}
		public static zVector2 operator -(zfloat lhs, zVector2 rhs)
		{
			return new zVector2(lhs - rhs.x, lhs - rhs.y);
		}
		public static zVector2 operator -(zVector2 lhs, zfloat rhs)
		{
			return new zVector2(lhs.x - rhs, lhs.y - rhs);
		}
		#endregion

		#region 符号
		public static zVector2 operator -(zVector2 a)
		{
			return new zVector2(-a.x, -a.y);
		}

		#endregion

		#region 乘法
		public static zVector2 operator *(int lhs, zVector2 rhs)
		{
			return new zVector2(lhs * rhs.x, lhs * rhs.y);
		}
		public static zVector2 operator *(zVector2 lhs, int rhs)
		{
			return new zVector2(lhs.x * rhs, lhs.y * rhs);
		}
		public static zVector2 operator *(zfloat lhs, zVector2 rhs)
		{
			return new zVector2(lhs * rhs.x, lhs * rhs.y);
		}
		public static zVector2 operator *(zVector2 lhs, zfloat rhs)
		{
			return new zVector2(lhs.x * rhs, lhs.y * rhs);
		}
		#endregion

		#region 除法
		public static zVector2 operator /(zVector2 lhs, int rhs)
		{
			return new zVector2(lhs.x / rhs, lhs.y / rhs);
		}
		public static zVector2 operator /(zVector2 lhs, zfloat rhs)
		{
			return new zVector2(lhs.x / rhs, lhs.y / rhs);
		}
		#endregion

		#region 相等判定
		public static bool operator !=(zVector2 lhs, zVector2 rhs)
		{
			return (lhs.x != rhs.x) || (lhs.y != rhs.y);
		}

		public static bool operator ==(zVector2 lhs, zVector2 rhs)
		{
			return (lhs.x == rhs.x) && (lhs.y == rhs.y);
		}
		#endregion


		#region 类型转换
		public static implicit operator zVector2(zVector3 v)
		{
			return new zVector2(v.x, v.y);
		}
		public static implicit operator zVector3(zVector2 v)
		{
			return new zVector3(v.x, v.y, zfloat.Zero);
		}

		#endregion

		public override string ToString()
		{
			return (x + " , " + y);
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
        }
	}
}