using System;
using UnityEngine;

namespace zUnity
{
	public struct zQuaternion
	{
		// public const zfloat kEpsilon = zfloat.Zero;
		public zfloat x;
		public zfloat y;
		public zfloat z;
		public zfloat w;
		public zfloat this[int index]
		{
			get
			{
				switch (index)
				{
					case 0:
						return this.x;
					case 1:
						return this.y;
					case 2:
						return this.z;
					case 3:
						return this.w;
					default:
						throw new IndexOutOfRangeException("Invalid zQuaternion index!");
				}
			}
			set
			{
				switch (index)
				{
					case 0:
                        this.x.value = value.value;
						break;
					case 1:
                        this.y.value = value.value;
						break;
					case 2:
                        this.z.value = value.value;
						break;
					case 3:
                        this.w.value = value.value;
						break;
					default:
						throw new IndexOutOfRangeException("Invalid zQuaternion index!");
				}
			}
		}

        public static readonly zQuaternion identity = new zQuaternion(zfloat.Zero, zfloat.Zero, zfloat.Zero, zfloat.One);
        //public static zQuaternion identity
        //{
        //    get
        //    {
        //        return _identity;
        //    }
        //}

		/// <summary>
		/// 返回欧拉角
		/// </summary>
		public zVector3 eulerAngles
		{
			get
			{
                return ToEuler(this);
			}
			set
			{
                this = Euler(value);
			}
		}

		//TODO 实现可能有问题
		/// <summary>
		/// 
		/// </summary>
		/// <param name="q"></param>
		/// <returns></returns>
		public static zVector3 ToEuler(zQuaternion zq)
		{
			zVector3 euler;

            //euler.x = zMathf.Asin(2 * (-q.y * q.z + q.x * q.w)) * zMathf.Rad2Deg;
            //euler.y = zMathf.Atan2(2 * (q.y * q.w + q.z * q.x), (1 - 2 * (q.y * q.y + q.x * q.x))) * zMathf.Rad2Deg;
            //euler.z = -zMathf.Atan2(2 * (-q.z * q.w - q.y * q.x), (1 - 2 * (q.z * q.z + q.x * q.x))) * zMathf.Rad2Deg;

            zfloat sqw = zq.w * zq.w;
            zfloat sqx = zq.x * zq.x;
            zfloat sqy = zq.y * zq.y;
            zfloat sqz = zq.z * zq.z;
            zfloat unit = sqx + sqy + sqz + sqw; // if normalised is one, otherwise is correction factor 
            zfloat test = zq.x * zq.w - zq.y * zq.z;
            zVector3 v;

            zfloat tv = zfloat.CreateFloat(4995);

            if (test > tv * unit)
            { // singularity at north pole 
                v.y = 2 * zMathf.Atan2(zq.y, zq.x);
                v.x = zMathf.PI / 2;
                v.z = zfloat.Zero;
                return NormalizeAngles(v * zMathf.Rad2Deg);
            }
            if (test < -tv * unit)
            { // singularity at south pole 
                v.y = -2 * zMathf.Atan2(zq.y, zq.x);
                v.x = -zMathf.PI / 2;
                v.z = zfloat.Zero;
                return NormalizeAngles(v * zMathf.Rad2Deg);
            }

            zQuaternion q = new zQuaternion(zq.w, zq.z, zq.x, zq.y);

            euler.x = zMathf.Asin(2 * (q.x * q.z - q.w * q.y)) * zMathf.Rad2Deg;                             // Pitch 
            euler.y = zMathf.Atan2(2 * q.x * q.w + 2 * q.y * q.z, 1 - 2 * (q.z * q.z + q.w * q.w)) * zMathf.Rad2Deg;     // Yaw 
            euler.z = zMathf.Atan2(2 * q.x * q.y + 2 * q.z * q.w, 1 - 2 * (q.y * q.y + q.z * q.z)) * zMathf.Rad2Deg;      // Roll 
            
            //euler.x = zMathf.Asin(2 * (q.x * q.z - q.w * q.y)) * zMathf.Rad2Deg;                             // Pitch 
            //euler.y = zMathf.Atan2(2 * q.x * q.w + 2 * q.y * q.z, 1 - 2 * (q.z * q.z + q.w * q.w)) * zMathf.Rad2Deg;     // Yaw 
            //euler.z = zMathf.Atan2(2 * q.x * q.y + 2 * q.z * q.w, 1 - 2 * (q.y * q.y + q.z * q.z)) * zMathf.Rad2Deg;      // Roll 

            return euler;
		}

        static zVector3 NormalizeAngles(zVector3 angles)
        {
            angles.x = NormalizeAngle(angles.x);
            angles.y = NormalizeAngle(angles.y);
            angles.z = NormalizeAngle(angles.z);
            return angles;
        }

        static zfloat NormalizeAngle(zfloat angle)
        {
            return angle % 360;
        }

        /// <summary>
        /// 直接构建四元数，一般不建议这样使用
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="w"></param>
        public zQuaternion(zfloat x, zfloat y, zfloat z, zfloat w)
		{
            this.x.value = x.value;
            this.y.value = y.value;
            this.z.value = z.value;
            this.w.value = w.value;
		}

		/// <summary>
		/// 设置四元数的值，一般不建议外部调用
		/// </summary>
		/// <param name="new_x"></param>
		/// <param name="new_y"></param>
		/// <param name="new_z"></param>
		/// <param name="new_w"></param>
		public void Set(zfloat new_x, zfloat new_y, zfloat new_z, zfloat new_w)
		{
            this.x.value = new_x.value;
            this.y.value = new_y.value;
            this.z.value = new_z.value;
            this.w.value = new_w.value;
		}

		/// <summary>
		/// 四元数点乘
		/// 点乘范围在[-1~1]
		/// 点乘结果越大，代表两个四元数所代表的旋转越接近
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static zfloat Dot(zQuaternion a, zQuaternion b)
		{
            zfloat result;
            result.value = (a.x.value * b.x.value + a.y.value * b.y.value + a.z.value * b.z.value + a.w.value * b.w.value) / zfloat.SCALE_10000;
            return result;
			//return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
		}

		/// <summary>
		/// 根据轴向旋转创建四元数
		/// </summary>
		/// <param name="angle"></param>
		/// <param name="axis"></param>
		/// <returns></returns>
		public static zQuaternion AngleAxis(zfloat angle, zVector3 axis)
		{
			zQuaternion ans;// = new zQuaternion();
			if(axis.sqrMagnitude.value != zfloat.One.value)
			{
				axis.Normalize();
			}
			
			zfloat halfAngle;
            halfAngle.value = angle.value * zfloat.Half.value / zfloat.SCALE_10000;
			zfloat s;
            s.value = zMathf.SinAngle(halfAngle).value;
			zfloat c;
            c.value = zMathf.CosAngle(halfAngle).value;

			ans.x.value = axis.x.value * s.value / zfloat.SCALE_10000;
			ans.y.value = axis.y.value * s.value / zfloat.SCALE_10000;
			ans.z.value = axis.z.value * s.value / zfloat.SCALE_10000;
			ans.w.value = c.value;

			return ans;
		}



		/// <summary>
		/// 根据指定的旋转矩阵，创建四元数
		/// 摘抄自微软System.Numerics  Quaternion.CreateFromRotationMatrix
		/// http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/index.htm
		/// </summary>
		/// <param name="m"></param>
		/// <returns></returns>
		public static zQuaternion FromMatrix(zMatrix4x4 m)
		{
            //zQuaternion q = new zQuaternion();
            //zfloat tr = m.m00 + m.m11 + m.m22;

            //if (tr > 0)
            //{
            //    zfloat S = zMathf.Sqrt(tr + zfloat.One) * 2; // S=4*qw 
            //    q.w = zfloat.Quarter * S;
            //    q.x = (m.m21 - m.m12) / S;
            //    q.y = (m.m02 - m.m20) / S;
            //    q.z = (m.m10 - m.m01) / S;
            //}
            //else if ((m.m00 > m.m11) & (m.m00 > m.m22))
            //{
            //    zfloat S = zMathf.Sqrt(zfloat.One + m.m00 - m.m11 - m.m22) * 2; // S=4*qx 
            //    q.w = (m.m21 - m.m12) / S;
            //    q.x = zfloat.Quarter * S;
            //    q.y = (m.m01 + m.m10) / S;
            //    q.z = (m.m02 + m.m20) / S;
            //}
            //else if (m.m11 > m.m22)
            //{
            //    zfloat S = zMathf.Sqrt(zfloat.One + m.m11 - m.m00 - m.m22) * 2; // S=4*qy
            //    q.w = (m.m02 - m.m20) / S;
            //    q.x = (m.m01 + m.m10) / S;
            //    q.y = zfloat.Quarter * S;
            //    q.z = (m.m12 + m.m21) / S;
            //}
            //else
            //{
            //    zfloat S = zMathf.Sqrt(zfloat.One + m.m22 - m.m00 - m.m11) * 2; // S=4*qz
            //    q.w = (m.m10 - m.m01) / S;
            //    q.x = (m.m02 + m.m20) / S;
            //    q.y = (m.m12 + m.m21) / S;
            //    q.z = zfloat.Quarter * S;
            //}
            //return q;

            zQuaternion q;// = new zQuaternion();
            zfloat tr;
            tr.value = m.m00.value + m.m11.value + m.m22.value;

            if (tr.value > 0)
            {
                zfloat S;
                S.value = zMathf.SqrtScale(tr.value + zfloat.One.value) * 2; // S=4*qw 
                q.w.value = zfloat.Quarter.value * S.value / zfloat.SCALE_10000;
                q.x.value = (m.m21.value - m.m12.value) * zfloat.SCALE_10000 / S.value;
                q.y.value = (m.m02.value - m.m20.value) * zfloat.SCALE_10000 / S.value;
                q.z.value = (m.m10.value - m.m01.value) * zfloat.SCALE_10000 / S.value;
            }
            else if ((m.m00.value > m.m11.value) && (m.m00.value > m.m22.value))
            {
                zfloat S;
                S.value = zMathf.SqrtScale(zfloat.One.value + m.m00.value - m.m11.value - m.m22.value) * 2; // S=4*qx 
                q.w.value = (m.m21.value - m.m12.value) * zfloat.SCALE_10000 / S.value;
                q.x.value = zfloat.Quarter.value * S.value / zfloat.SCALE_10000;
                q.y.value = (m.m01.value + m.m10.value) * zfloat.SCALE_10000 / S.value;
                q.z.value = (m.m02.value + m.m20.value) * zfloat.SCALE_10000 / S.value;
            }
            else if (m.m11.value > m.m22.value)
            {
                zfloat S;
                S.value = zMathf.SqrtScale(zfloat.One.value + m.m11.value - m.m00.value - m.m22.value) * 2; // S=4*qy
                q.w.value = (m.m02.value - m.m20.value) * zfloat.SCALE_10000 / S.value;
                q.x.value = (m.m01.value + m.m10.value) * zfloat.SCALE_10000 / S.value;
                q.y.value = zfloat.Quarter.value * S.value / zfloat.SCALE_10000;
                q.z.value = (m.m12.value + m.m21.value) * zfloat.SCALE_10000 / S.value;
            }
            else
            {
                zfloat S;
                S.value = zMathf.SqrtScale(zfloat.One.value + m.m22.value - m.m00.value - m.m11.value) * 2; // S=4*qz
                q.w.value = (m.m10.value - m.m01.value) * zfloat.SCALE_10000 / S.value;
                q.x.value = (m.m02.value + m.m20.value) * zfloat.SCALE_10000 / S.value;
                q.y.value = (m.m12.value + m.m21.value) * zfloat.SCALE_10000 / S.value;
                q.z.value = zfloat.Quarter.value * S.value / zfloat.SCALE_10000;
            }
            return q;
		}

		/// <summary>
		/// 获取当前四元数的旋转轴，和旋转角度
		/// 摘抄自OGRE
		/// </summary>
		/// <param name="angle"></param>
		/// <param name="axis"></param>
		public void ToAngleAxis(out zfloat angle, out zVector3 axis)
		{
            //zfloat fSqrLength = x * x + y * y + z * z;
            //if (fSqrLength > zfloat.Zero)
            //{
            //    angle = 2 * zMathf.AcosAngle(w);
            //    zfloat fInvLength = 1 / zMathf.Sqrt(fSqrLength);
            //    axis.x = x * fInvLength;
            //    axis.y = y * fInvLength;
            //    axis.z = z * fInvLength;
            //}
            //else
            //{
            //    // angle is 0 (mod 2*pi), so any axis will do
            //    angle = zfloat.Zero;
            //    axis.x = zfloat.One;
            //    axis.y = zfloat.Zero;
            //    axis.z = zfloat.Zero;
            //}

            zfloat fSqrLength;
            fSqrLength.value = (x.value * x.value + y.value * y.value + z.value * z.value) / zfloat.SCALE_10000;
            if (fSqrLength.value > zfloat.Zero.value)
            {
                angle.value = 2 * zMathf.AcosAngle(w).value;
                zfloat fInvLength;
                fInvLength.value = zfloat.One.value * zfloat.SCALE_10000 / zMathf.SqrtScale(fSqrLength.value);
                axis.x.value = x.value * fInvLength.value / zfloat.SCALE_10000;
                axis.y.value = y.value * fInvLength.value / zfloat.SCALE_10000;
                axis.z.value = z.value * fInvLength.value / zfloat.SCALE_10000;
            }
            else
            {
                // angle is 0 (mod 2*pi), so any axis will do
                angle.value = zfloat.Zero.value;
                axis.x.value = zfloat.One.value;
                axis.y.value = zfloat.Zero.value;
                axis.z.value = zfloat.Zero.value;
            }
		}

		/// <summary>
		/// 变换后的坐标轴朝向
		/// </summary>
		/// <param name="q"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		public static void ToAxis(zQuaternion q, ref zVector3 vx, ref zVector3 vy, ref zVector3 vz)
		{
			zMatrix4x4 rot = q.RotationMatrix();
			vx.x = rot.m00;
			vx.y = rot.m01;
			vx.z = rot.m02;

			vy.x = rot.m10;
			vy.y = rot.m11;
			vy.z = rot.m12;

			vz.x = rot.m20;
			vz.y = rot.m21;
			vz.z = rot.m22;
		}

		/// <summary>
		/// 计算一个向量，由fromDirection旋转到toDirection所需要的四元数
		/// </summary>
		/// <param name="fromDirection"></param>
		/// <param name="toDirection"></param>
		/// <returns></returns>
		public static zQuaternion FromToRotation(zVector3 fromDirection, zVector3 toDirection)
		{
			zVector3 n = zVector3.Cross(fromDirection, toDirection);//不需要归一化，因为AngleAxis内部会进行归一化
			fromDirection.Normalize();
			toDirection.Normalize();
			zfloat angle = zMathf.AcosAngle(zVector3.Dot(ref fromDirection,ref toDirection));
			return AngleAxis(angle, n);
		}

		public void SetFromToRotation(zVector3 fromDirection, zVector3 toDirection)
		{
			this = FromToRotation(fromDirection, toDirection);
		}

		/// <summary>
		/// 将四元数转换为旋转矩阵
		/// 摘抄自OGRE
		/// </summary>
		/// <returns></returns>
		public zMatrix4x4 RotationMatrix()
		{
			return zMatrix4x4.CreateFromQuaternion(this);
			/*
			zMatrix4x4 kRot = zMatrix4x4.identity;

			zfloat fTx = x + x;
			zfloat fTy = y + y;
			zfloat fTz = z + z;
			zfloat fTwx = fTx * w;
			zfloat fTwy = fTy * w;
			zfloat fTwz = fTz * w;
			zfloat fTxx = fTx * x;
			zfloat fTxy = fTy * x;
			zfloat fTxz = fTz * x;
			zfloat fTyy = fTy * y;
			zfloat fTyz = fTz * y;
			zfloat fTzz = fTz * z;

			kRot[0, 0] = 1 - (fTyy + fTzz);
			kRot[1, 0] = fTxy - fTwz;
			kRot[2, 0] = fTxz + fTwy;
			kRot[0, 1] = fTxy + fTwz;
			kRot[1, 1] = 1 - (fTxx + fTzz);
			kRot[2, 1] = fTyz - fTwx;
			kRot[0, 2] = fTxz - fTwy;
			kRot[1, 2] = fTyz + fTwx;
			kRot[2, 2] = 1 - (fTxx + fTyy);

			return kRot;*/
		}

		/// <summary>
		/// 根据指定的变换后的轴向，创建四元数
		/// TODO TODO 当forward 与 upward方向相反时，要特殊处理
		/// </summary>
		/// <param name="forward"></param>
		/// <param name="upwards"></param>
		/// <returns></returns>
		public static zQuaternion LookRotation(zVector3 forward, zVector3 upwards)
		{
			upwards.Normalize();
			forward.Normalize();
			/*zVector3 right = zVector3.Cross(upwards, forward).normalized;

			zVector3 n = forward + upwards + right;
			zVector3 baseN = zVector3.forward + zVector3.up + zVector3.right;

			return FromToRotation(baseN, n);*/
			if (forward.sqrMagnitude != 1)
				forward = forward.normalized;

			zVector3 right = zVector3.Cross(upwards, forward).normalized;
			upwards = zVector3.Cross(forward, right).normalized;
			zMatrix4x4 mat = zMatrix4x4.identity;
			mat.m00.value = right.x.value;
            mat.m10.value = right.y.value;
            mat.m20.value = right.z.value;

            mat.m01.value = upwards.x.value;
            mat.m11.value = upwards.y.value;
            mat.m21.value = upwards.z.value;

            mat.m02.value = forward.x.value;
            mat.m12.value = forward.y.value;
            mat.m22.value = forward.z.value;

			//zUDebug.Log(mat);

			return FromMatrix(mat);
		}

		public static zQuaternion LookRotation(zVector3 forward)
		{
			return LookRotation(forward, zVector3.up);
		}


		public void SetLookRotation(zVector3 view)
		{
			this = zQuaternion.LookRotation(view);
		}
		public void SetLookRotation(zVector3 view, zVector3 up)
		{
			this = zQuaternion.LookRotation(view, up);
		}

		/// <summary>
		/// 四元数标准化
		/// </summary>
		public void Normalize()
		{
			zfloat ls = this.x * this.x + this.y * this.y + this.z * this.z + this.w * this.w;

			zfloat invNorm = 1 / zMathf.Sqrt(ls);

			this.x = this.x * invNorm;
			this.y = this.y * invNorm;
			this.z = this.z * invNorm;
			this.w = this.w * invNorm;
		}

		public static zQuaternion Normalize(zQuaternion value)
		{
			zQuaternion ans;

			zfloat ls = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;

			zfloat invNorm = 1 / zMathf.Sqrt(ls);

			ans.x = value.x * invNorm;
			ans.y = value.y * invNorm;
			ans.z = value.z * invNorm;
			ans.w = value.w * invNorm;

			return ans;
		}

		/// <summary>
		/// 球面插值
		/// 摘抄自xenko Quaternion.Slerp
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="amount"></param>
		/// <returns></returns>
		public static zQuaternion Slerp(zQuaternion from, zQuaternion to, zfloat amount)
		{
			zfloat epsilon = zfloat.Epsilon;


			zfloat t = amount;


			zfloat cosOmega = from.x * to.x + from.y * to.y + from.z * to.z + from.w * to.w;


			bool flip = false;


			if (cosOmega < zfloat.Zero)
			{
				flip = true;
				cosOmega = -cosOmega;
			}


			zfloat s1, s2;


			if (cosOmega > (zfloat.One - epsilon))
			{
				// Too close, do straight linear interpolation. 
				s1 = zfloat.One - t;
				s2 = (flip) ? -t : t;
			}
			else
			{
				zfloat omega = zMathf.Acos(cosOmega);
				zfloat invSinOmega = (1 / zMathf.Sin(omega));


				s1 = zMathf.Sin((zfloat.One - t) * omega) * invSinOmega;
				s2 = (flip)
					? -zMathf.Sin(t * omega) * invSinOmega
					: zMathf.Sin(t * omega) * invSinOmega;
			}


			zQuaternion ans;


			ans.x = s1 * from.x + s2 * to.x;
			ans.y = s1 * from.y + s2 * to.y;
			ans.z = s1 * from.z + s2 * to.z;
			ans.w = s1 * from.w + s2 * to.w;


			return ans;


			/*zQuaternion result = new zQuaternion();

			zfloat opposite;
			zfloat inverse;
			zfloat dot = Dot(from, to);

			if (zMathf.Abs(dot).Approximate(zfloat.One))
			{
				inverse = zfloat.One - amount;
				opposite = amount * zMathf.Sign(dot);
			}
			else
			{
				zfloat acos = zMathf.Acos(zMathf.Abs(dot));
				zfloat invSin = (zfloat.One / zMathf.Sin(acos));

				inverse = zMathf.Sin((zfloat.One - amount) * acos) * invSin;
				opposite = zMathf.Sin(amount * acos) * invSin * zMathf.Sign(dot);
			}

			result.x = (inverse * from.x) + (opposite * to.x);
			result.y = (inverse * from.y) + (opposite * to.y);
			result.z = (inverse * from.z) + (opposite * to.z);
			result.w = (inverse * from.w) + (opposite * to.w);

			return result;*/
		}

		/// <summary>
		/// 四元数插值
		/// 摘抄自xenko Quaternion.Lerp
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="amount"></param>
		/// <returns></returns>
		public static zQuaternion Lerp(zQuaternion from, zQuaternion to, zfloat amount)
		{
			zQuaternion result = new zQuaternion();
			zfloat inverse = 1 - amount;

			if (Dot(from, to) >= 0)
			{
				result.x = (inverse * from.x) + (amount * to.x);
				result.y = (inverse * from.y) + (amount * to.y);
				result.z = (inverse * from.z) + (amount * to.z);
				result.w = (inverse * from.w) + (amount * to.w);
			}
			else
			{
				result.x = (inverse * from.x) - (amount * to.x);
				result.y = (inverse * from.y) - (amount * to.y);
				result.z = (inverse * from.z) - (amount * to.z);
				result.w = (inverse * from.w) - (amount * to.w);
			}

			result.Normalize();

			return result;
		}

		/* public static zQuaternion RotateTowards(zQuaternion from, zQuaternion to, zfloat maxDegreesDelta)
		 {
			 return identity;
		 }
		 private static zQuaternion UnclampedSlerp(zQuaternion from, zQuaternion to, zfloat t)
		 {
			 return identity;
			 //return zQuaternion.INTERNAL_CALL_UnclampedSlerp(ref from, ref to, t);
		 }*/

		/// <summary>
		/// 反向旋转
		/// </summary>
		/// <param name="rotation"></param>
		/// <returns></returns>
		public static zQuaternion Inverse(zQuaternion rotation)
		{
			//  -1   (       a              -v       )
			// q   = ( -------------   ------------- )
			//       (  a^2 + |v|^2  ,  a^2 + |v|^2  )

			zQuaternion ans = new zQuaternion();

			zfloat ls = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
			zfloat invNorm = 1 / ls;

			ans.x = -rotation.x * invNorm;
			ans.y = -rotation.y * invNorm;
			ans.z = -rotation.z * invNorm;
			ans.w = rotation.w * invNorm;

			return ans;
		}

		public override string ToString()
		{
			return (x + " , " + y + " , " + z + " , " + w);
		}

		/* public static zfloat Angle(zQuaternion a, zQuaternion b)
		 {
			 return zfloat.Zero;
			// zfloat f = zQuaternion.Dot(a, b);
			// return zMath.Acos(zMath.Min(zMath.Abs(f), 1f)) * 2f * 57.29578f;
		 }*/

		public static zQuaternion Euler(zfloat x, zfloat y, zfloat z)
		{
			zfloat sr, cr, sp, cp, sy, cy;
			zfloat roll;
            zfloat pitch;
            zfloat yaw;
            roll.value = z.value;
            pitch.value = x.value;
            yaw.value = y.value;

			zfloat halfRoll;
            halfRoll.value = roll.value * zfloat.Half.value / zfloat.SCALE_10000;
			sr.value = zMathf.SinAngle(halfRoll).value;
			cr.value = zMathf.CosAngle(halfRoll).value;


			zfloat halfPitch;
            halfPitch.value = pitch.value * zfloat.Half.value / zfloat.SCALE_10000;
			sp = zMathf.SinAngle(halfPitch);
			cp = zMathf.CosAngle(halfPitch);


			zfloat halfYaw;
            halfYaw.value = yaw.value * zfloat.Half.value / zfloat.SCALE_10000;
			sy = zMathf.SinAngle(halfYaw);
			cy = zMathf.CosAngle(halfYaw);


			zQuaternion result;


			result.x.value = ((cy.value * sp.value / zfloat.SCALE_10000) * cr.value / zfloat.SCALE_10000) 
                + ((sy.value * cp.value / zfloat.SCALE_10000) * sr.value / zfloat.SCALE_10000);
            result.y.value = ((sy.value * cp.value / zfloat.SCALE_10000) * cr.value / zfloat.SCALE_10000)
                - ((cy.value * sp.value / zfloat.SCALE_10000) * sr.value / zfloat.SCALE_10000);
            result.z.value = ((cy.value * cp.value / zfloat.SCALE_10000) * sr.value / zfloat.SCALE_10000)
                - ((sy.value * sp.value / zfloat.SCALE_10000) * cr.value / zfloat.SCALE_10000);
            result.w.value = ((cy.value * cp.value / zfloat.SCALE_10000) * cr.value / zfloat.SCALE_10000)
                + ((sy.value * sp.value / zfloat.SCALE_10000) * sr.value / zfloat.SCALE_10000);


			return result;

		}
		public static zQuaternion Euler(zVector3 euler)
		{
			return Euler(euler.x, euler.y, euler.z);
		}


		/// <summary>
		/// 共轭四元数
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static zQuaternion Conjugate(zQuaternion value)
		{
			zQuaternion ans;

            ans.x.value = -value.x.value;
            ans.y.value = -value.y.value;
            ans.z.value = -value.z.value;
            ans.w.value = value.w.value;

			return ans;
		}


		/* public override int GetHashCode()
		 {
			 return this.x.GetHashCode() ^ this.y.GetHashCode() << 2 ^ this.z.GetHashCode() >> 2 ^ this.w.GetHashCode() >> 1;
		 }*/
		public override bool Equals(object other)
		{
			if (!(other is zQuaternion))
			{
				return false;
			}
			zQuaternion zQuaternion = (zQuaternion)other;
			return this.x.Equals(zQuaternion.x) && this.y.Equals(zQuaternion.y) && this.z.Equals(zQuaternion.z) && this.w.Equals(zQuaternion.w);
		}
		public static zQuaternion operator *(zQuaternion lhs, zQuaternion rhs)
		{
            zQuaternion zq;
			//return new zQuaternion(lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y, lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z, lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x, lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);
			long x = (lhs.w.value * rhs.x.value + lhs.x.value * rhs.w.value + lhs.y.value * rhs.z.value - lhs.z.value * rhs.y.value) / zfloat.SCALE_10000;
			long y = (lhs.w.value * rhs.y.value + lhs.y.value * rhs.w.value + lhs.z.value * rhs.x.value - lhs.x.value * rhs.z.value) / zfloat.SCALE_10000;
			long z = (lhs.w.value * rhs.z.value + lhs.z.value * rhs.w.value + lhs.x.value * rhs.y.value - lhs.y.value * rhs.x.value) / zfloat.SCALE_10000;
			long w = (lhs.w.value * rhs.w.value - lhs.x.value * rhs.x.value - lhs.y.value * rhs.y.value - lhs.z.value * rhs.z.value) / zfloat.SCALE_10000;

            zq.x.value = x;
            zq.y.value = y;
            zq.z.value = z;
            zq.w.value = w;

			return zq;

		}

		public static zVector3 operator *(zQuaternion rotation, zVector3 point)
		{
			zfloat num = rotation.x * zfloat.Two;
			zfloat num2 = rotation.y * zfloat.Two;
			zfloat num3 = rotation.z * zfloat.Two;
			zfloat num4 = rotation.x * num;
			zfloat num5 = rotation.y * num2;
			zfloat num6 = rotation.z * num3;
			zfloat num7 = rotation.x * num2;
			zfloat num8 = rotation.x * num3;
			zfloat num9 = rotation.y * num3;
			zfloat num10 = rotation.w * num;
			zfloat num11 = rotation.w * num2;
			zfloat num12 = rotation.w * num3;
			zVector3 result;
			result.x = (zfloat.One - (num5 + num6)) * point.x + (num7 - num12) * point.y + (num8 + num11) * point.z;
			result.y = (num7 + num12) * point.x + (zfloat.One - (num4 + num6)) * point.y + (num9 - num10) * point.z;
			result.z = (num8 - num11) * point.x + (num9 + num10) * point.y + (zfloat.One - (num4 + num5)) * point.z;
			return result;
		}

		//public static zVector3 operator *(zQuaternion rotation, zVector3 point)
		//{
		//	zfloat num;
		//          num.value = rotation.x.value * zfloat.Two.value / zfloat.SCALE_10000;
		//	zfloat num2;
		//          num2.value = rotation.y.value * zfloat.Two.value / zfloat.SCALE_10000;
		//	zfloat num3;
		//          num3.value = rotation.z.value * zfloat.Two.value / zfloat.SCALE_10000;
		//	zfloat num4;
		//          num4.value = rotation.x.value * num.value / zfloat.SCALE_10000;
		//	zfloat num5;
		//          num5.value = rotation.y.value * num2.value / zfloat.SCALE_10000;
		//	zfloat num6;
		//          num6.value = rotation.z.value * num3.value / zfloat.SCALE_10000;
		//	zfloat num7;
		//          num7.value = rotation.x.value * num2.value / zfloat.SCALE_10000;
		//	zfloat num8;
		//          num8.value = rotation.x.value * num3.value / zfloat.SCALE_10000;
		//	zfloat num9;
		//          num9.value = rotation.y.value * num3.value / zfloat.SCALE_10000;
		//	zfloat num10;
		//          num10.value = rotation.w.value * num.value / zfloat.SCALE_10000;
		//	zfloat num11;
		//          num11.value = rotation.w.value * num2.value / zfloat.SCALE_10000;
		//	zfloat num12;
		//          num12.value = rotation.w.value * num3.value / zfloat.SCALE_10000;
		//	zVector3 result;
		//	result.x.value = (zfloat.One.value - (num5.value + num6.value)) * point.x.value / zfloat.SCALE_10000 
		//              + (num7.value - num12.value) * point.y.value / zfloat.SCALE_10000
		//              + (num8.value + num11.value) * point.z.value / zfloat.SCALE_1000;
		//	result.y.value = (num7.value + num12.value) * point.x.value / zfloat.SCALE_1000 
		//              + (zfloat.One.value - (num4.value + num6.value)) * point.y.value / zfloat.SCALE_1000 
		//              + (num9.value - num10.value) * point.z.value / zfloat.SCALE_1000;
		//	result.z.value = (num8.value - num11.value) * point.x.value / zfloat.SCALE_1000 
		//              + (num9.value + num10.value) * point.y.value / zfloat.SCALE_1000 
		//              + (zfloat.One.value - (num4.value + num5.value)) * point.z.value / zfloat.SCALE_1000;
		//	return result;
		//}
		public static bool operator ==(zQuaternion lhs, zQuaternion rhs)
		{
			return zQuaternion.Dot(lhs, rhs).Approximate(zfloat.One);
		}
		public static bool operator !=(zQuaternion lhs, zQuaternion rhs)
		{
			return !(lhs == rhs);
		}
	}
}