using System;

namespace zUnity
{
	public struct zVector4
	{
		public static readonly zVector4 zero = new zVector4((zfloat)0, (zfloat)0, (zfloat)0, (zfloat)0);
		public static readonly zVector4 one = new zVector4((zfloat)1, (zfloat)1, (zfloat)1, (zfloat)1);
		public static readonly zVector4 forward = new zVector4((zfloat)0, (zfloat)0, (zfloat)1, (zfloat)0);
		public static readonly zVector4 back = new zVector4((zfloat)0, (zfloat)0, (zfloat)(-1), (zfloat)0);
		public static readonly zVector4 up = new zVector4((zfloat)0, (zfloat)1, (zfloat)0, (zfloat)0);
		public static readonly zVector4 down = new zVector4((zfloat)0, (zfloat)(-1), (zfloat)0, (zfloat)0);
		public static readonly zVector4 left = new zVector4((zfloat)(-1), (zfloat)0, (zfloat)0, (zfloat)0);
		public static readonly zVector4 right = new zVector4((zfloat)1, (zfloat)0, (zfloat)0, (zfloat)0);

		public zVector4(zfloat x, zfloat y, zfloat z, zfloat w)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.w = w;
		}

		public zVector4(int x, int y, int z, int w)
		{
			this.x = (zfloat)x;
			this.y = (zfloat)y;
			this.z = (zfloat)z;
			this.w = (zfloat)w;
		}

		public zVector4(zVector4 vec)
		{
			this.x = vec.x;
			this.y = vec.y;
			this.z = vec.z;
			this.w = vec.w;
		}

		public zfloat x;
		public zfloat y;
		public zfloat z;
		public zfloat w;


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
				else if (index == 3)
				{
					return w;
				}
				else
				{
					throw new IndexOutOfRangeException("zVector4 Only Contains x,y,z,w，so the index must use 0,1,2 or 3 !");
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
				else if (index == 3)
				{
					w = value;
				}
				else
				{
					throw new IndexOutOfRangeException("zVector4 Only Contains x,y,z,w，so the index must use 0,1,2 or 3 !");
				}
			}
		}


		//	private zVector4 normalized;

		public zVector4 normalized
		{
			get
			{
				return zVector4.Normalize(ref this);
			}
		}

		public zfloat sqrMagnitude
		{
			get
			{
				return zVector4.SqrMagnitude(ref this);
			}
		}


		public zfloat magnitude
		{
			get { return zVector4.Magnitude(ref this); }

		}

		public void Normalize()
		{
			zfloat num = zVector4.Magnitude(ref this);
			if (num > 0)
			{
				x /= num;
				y /= num;
				z /= num;
				w /= num;
			}
			else
			{
				x = zfloat.Zero;
				y = zfloat.Zero;
				z = zfloat.Zero;
				w = zfloat.Zero;
			}

		}

		public static zVector4 Normalize(ref zVector4 vec)
		{
			zfloat num = zVector4.Magnitude(ref vec);
			if (num > 0)
			{
				return vec / num;
			}
			return zVector4.zero;

		}

		/// <summary>
		/// 向量长度
		/// </summary>
		/// <param name="a"></param>
		/// <returns></returns>
		public static zfloat Magnitude(ref zVector4 a)
		{
			return zMathf.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z + a.w * a.w);
		}


		public void Scale(zVector4 scale)
		{
			x *= scale.x;
			y *= scale.y;
			z *= scale.z;
			w *= scale.w;
		}

		/// <summary>
		/// 长度的平方
		/// </summary>
		/// <param name="a"></param>
		/// <returns></returns>
		public static zfloat SqrMagnitude(ref zVector4 a)
		{
			return a.x * a.x + a.y * a.y + a.z * a.z + a.w * a.w;
		}


		/// <summary>
		/// 线性插值
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static zVector4 Lerp(zVector4 from, zVector4 to, zfloat t)
		{
			t = zMathf.Clamp01(t);
			return new zVector4(from.x + (to.x - from.x) * t, from.y + (to.y - from.y) * t, from.z + (to.z - from.z) * t, from.w + (to.w - from.w) * t);
		}

		public static zVector4 MoveTowards(zVector4 current, zVector4 target, zfloat maxDistanceDelta)
		{
			zVector4 a = target - current;
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
		public static zVector4 Scale(zVector4 a, zVector4 b)
		{
			return new zVector4(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);
		}

		public static zfloat Dot(zVector4 a, zVector4 b)
		{
			return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
		}

		public static zVector4 Project(zVector4 vec, zVector4 onNormal)
		{
			return onNormal * zVector4.Dot(vec, onNormal) / zVector4.Dot(onNormal, onNormal);
		}

		public static zfloat Distance(zVector4 a, zVector4 b)
		{
			a = a - b;
			return a.x * a.x + a.y * a.y + a.z * a.z + a.w * a.w;
		}

		public static zVector4 Min(zVector4 lhs, zVector4 rhs)
		{
			return new zVector4(zMathf.Min(lhs.x, rhs.x), zMathf.Min(lhs.y, rhs.y), zMathf.Min(lhs.z, rhs.z), zMathf.Min(lhs.w, rhs.w));
		}

		public static zVector4 Max(zVector4 lhs, zVector4 rhs)
		{
			return new zVector4(zMathf.Max(lhs.x, rhs.x), zMathf.Max(lhs.y, rhs.y), zMathf.Max(lhs.z, rhs.z), zMathf.Min(lhs.w, rhs.w));
		}

		#region 加法
		public static zVector4 operator +(zVector4 lhs, zVector4 rhs)
		{
			return new zVector4(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z, lhs.w + rhs.w);
		}

		public static zVector4 operator +(int lhs, zVector4 rhs)
		{
			return new zVector4(lhs + rhs.x, lhs + rhs.y, lhs + rhs.z, lhs + rhs.w);
		}

		public static zVector4 operator +(zVector4 lhs, int rhs)
		{
			return new zVector4(lhs.x + rhs, lhs.y + rhs, lhs.z + rhs, lhs.w + rhs);
		}

		public static zVector4 operator +(zfloat lhs, zVector4 rhs)
		{
			return new zVector4(lhs + rhs.x, lhs + rhs.y, lhs + rhs.z, lhs + rhs.w);
		}

		public static zVector4 operator +(zVector4 lhs, zfloat rhs)
		{
			return new zVector4(lhs.x + rhs, lhs.y + rhs, lhs.z + rhs, lhs.w + rhs);
		}
		#endregion

		#region 减法
		public static zVector4 operator -(zVector4 lhs, zVector4 rhs)
		{
			return new zVector4(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z, lhs.w - rhs.w);
		}
		public static zVector4 operator -(int lhs, zVector4 rhs)
		{
			return new zVector4(lhs - rhs.x, lhs - rhs.y, lhs - rhs.z, lhs - rhs.w);
		}
		public static zVector4 operator -(zVector4 lhs, int rhs)
		{
			return new zVector4(lhs.x - rhs, lhs.y - rhs, lhs.z - rhs, lhs.w - rhs);
		}
		public static zVector4 operator -(zfloat lhs, zVector4 rhs)
		{
			return new zVector4(lhs - rhs.x, lhs - rhs.y, lhs - rhs.z, lhs - rhs.w);
		}
		public static zVector4 operator -(zVector4 lhs, zfloat rhs)
		{
			return new zVector4(lhs.x - rhs, lhs.y - rhs, lhs.z - rhs, lhs.w - rhs);
		}
		#endregion

		#region 符号
		public static zVector4 operator -(zVector4 a)
		{
			return new zVector4(-a.x, -a.y, -a.z, -a.w);
		}

		#endregion

		#region 乘法
		public static zVector4 operator *(int lhs, zVector4 rhs)
		{
			return new zVector4(lhs * rhs.x, lhs * rhs.y, lhs * rhs.z, lhs * rhs.w);
		}
		public static zVector4 operator *(zVector4 lhs, int rhs)
		{
			return new zVector4(lhs.x * rhs, lhs.y * rhs, lhs.z * rhs, lhs.w * rhs);
		}
		public static zVector4 operator *(zfloat lhs, zVector4 rhs)
		{
			return new zVector4(lhs * rhs.x, lhs * rhs.y, lhs * rhs.z, lhs * rhs.w);
		}
		public static zVector4 operator *(zVector4 lhs, zfloat rhs)
		{
			return new zVector4(lhs.x * rhs, lhs.y * rhs, lhs.z * rhs, lhs.w * rhs);
		}
		#endregion

		#region 除法
		public static zVector4 operator /(zVector4 lhs, int rhs)
		{
			return new zVector4(lhs.x / rhs, lhs.y / rhs, lhs.z / rhs, lhs.w / rhs);
		}

		public static zVector4 operator /(zVector4 lhs, zfloat rhs)
		{
			return new zVector4(lhs.x / rhs, lhs.y / rhs, lhs.z / rhs, lhs.w / rhs);
		}
		#endregion

		#region 相等判定
		public static bool operator !=(zVector4 lhs, zVector4 rhs)
		{
			return (lhs.x.value != rhs.x.value) || (lhs.y.value != rhs.y.value) || (lhs.z.value != rhs.z.value) || (lhs.w.value != rhs.w.value);
		}

		public static bool operator ==(zVector4 lhs, zVector4 rhs)
		{
			return (lhs.x.value == rhs.x.value) && (lhs.y.value == rhs.y.value) && (lhs.z.value == rhs.z.value) && (lhs.w.value == rhs.w.value);
		}
		#endregion


		#region 类型转换
		public static implicit operator zVector4(zVector3 v)
		{
			return new zVector4(v.x, v.y, v.z, zfloat.Zero);
		}
		public static implicit operator zVector3(zVector4 v)
		{
			return new zVector3(v.x, v.y, v.z);
		}
		public static implicit operator zVector4(zVector2 v)
		{
			return new zVector4(v.x, v.y, zfloat.Zero, zfloat.Zero);
		}
		public static implicit operator zVector2(zVector4 v)
		{
			return new zVector2(v.x, v.y);
		}
		#endregion

		public override string ToString()
		{
			return (x + " , " + y + " , " + z + " , " + w);
		}
	}
}