namespace zUnity
{
	public class zMathTool
	{
		/// <summary>
		/// 点在线段上的投影
		/// 如果没有求出投影点，则返回NULL
		/// </summary>
		/// <param name="__point"></param>
		/// <param name="__lineStart"></param>
		/// <param name="__lineEnd"></param>
		/// <param name="__haveRange">是否要求投影点在线段范围内</param>
		/// <returns></returns>
		public static zVector3 Projection(zVector3 __point, zVector3 __lineStart, zVector3 __lineEnd, bool __haveRange = true)
		{
			zVector3 pointVec = __point - __lineStart;
			zVector3 lineVec = __lineEnd - __lineStart;
			zVector3 projPoint = zVector3.Project(pointVec, lineVec);
			projPoint += __lineStart;
			
			if(__haveRange)
			{
				if(PointInSegment(__lineStart,__lineEnd,projPoint))
				{
					return projPoint;
				}
				else
				{
					return zVector3.NULL;
				}
				/*zfloat lineSqrMagn = lineVec.sqrMagnitude;
				if ((projPoint - __lineStart).sqrMagnitude > lineSqrMagn
					|| (projPoint - __lineEnd).sqrMagnitude > lineSqrMagn)
				{
					return zVector3.NULL;
				}
				else
				{
					return projPoint;
				}*/
			}
			else
			{
				return projPoint;
			}
		}

		/// <summary>
		/// 点是否在线段上
		/// 该函数假设点一定在两点所组成的直线上，来判断是否在两点所组成的线段上
		/// </summary>
		/// <param name="__lineStart"></param>
		/// <param name="__lineEnd"></param>
		/// <param name="__point"></param>
		/// <returns></returns>
		protected static bool PointInSegment(zVector3 __lineStart, zVector3 __lineEnd, zVector3 __point)
		{
			zVector3 lineVec = __lineEnd - __lineStart;
			zfloat lineSqrMagn = lineVec.sqrMagnitude;
			if ((__point - __lineStart).sqrMagnitude > lineSqrMagn
				|| (__point - __lineEnd).sqrMagnitude > lineSqrMagn)
			{
				return false;
			}
			else
			{
				return true;
			}
		}

        public static bool LineCrossCircle(out zVector3 __p1, out zVector3 __p2, zVector3 __lineStart, zVector3 __lineEnd, zVector3 __Center, zfloat __radius)
        {
            __lineStart.y = zfloat.Zero;
            __lineEnd.y = zfloat.Zero;
            __Center.y = zfloat.Zero;
            if (__lineStart.x == __lineEnd.x)
            {
                __lineEnd.x += zfloat.CreateFloat(100);// 0.0001f;
            }
            zfloat k = (__lineStart.z - __lineEnd.z) / (__lineStart.x - __lineEnd.x);
            /*if(k > 100)
            {
                k = new zfloat(100);
            }
            if(k < -100)
            {
                k = new zfloat(-100);
            }*/
            zfloat c = __lineStart.z - k * __lineStart.x;
            zfloat a = __Center.x;
            zfloat b = __Center.z;

            zfloat ta = 1 + k * k;
            zfloat tb = 2 * (k * c - k * b - a);
            zfloat tc = a * a + (c - b) * (c - b) - __radius * __radius;

            zfloat delta = tb * tb - 4 * ta * tc;
            if (delta >= 0)
            {
                zfloat sqrtDelta = zMathf.Sqrt(delta);
                __p1.x = (-tb + sqrtDelta) / (2 * ta);
                __p1.y = zfloat.Zero;
                __p1.z = k * __p1.x + c;
                __p2.x = (-tb - sqrtDelta) / (2 * ta);
                __p2.y = zfloat.Zero;
                __p2.z = k * __p2.x + c;
                return true;
            }
            else
            {
                __p1 = zVector3.zero;
                __p2 = zVector3.zero;
                return false;
            }

        }

        /// <summary>
        /// 快速Vector赋值
        /// </summary>
        /// <param name="__dst">目标向量</param>
        /// <param name="__src">源向量</param>
        /// <param name="__ignoreY">是否忽略Y值,默认不忽略。如果忽略Y，将会置为0</param>
        public static void FastVectorAssgin(ref zVector3 __dst, ref zVector3 __src, bool __ignoreY = false)
        {
            __dst.x.value = __src.x.value;
            if(!__ignoreY)
            {
                __dst.y.value = 0;
            }
            else
            {
                __dst.y.value = __src.y.value;
            }
            __dst.z.value = __src.z.value;
        }


        public static void FastVectorAssgin(ref zVector3 __dst, ref zfloat __srcX, ref zfloat __srcY, ref zfloat __srcZ, bool __ignoreY = false)
        {
            __dst.x.value = __srcX.value;
            if (!__ignoreY)
            {
                __dst.y.value = 0;
            }
            else
            {
                __dst.y.value = __srcY.value;
            }
            __dst.z.value = __srcY.value;
        }

		/// <summary>
		/// 用于距离判定预判 1.8
		/// 这个值约等于单位立方体的对角线 1.73205
		/// </summary>
		protected readonly static zfloat DIS_PRE_CHECK_VALUE = new zfloat(1, 8000);
      //  protected static zfloat tempPreDis;
        protected static zVector3 tempVec;
		/// <summary>
		/// 测试两个点的距离，是否小于指定值
		/// </summary>
		/// <param name="__p1"></param>
		/// <param name="__p2"></param>
		/// <param name="__dis"></param>
		/// <returns></returns>
		public static bool DistanceCheckRef(ref zVector3 __p1, ref zVector3 __p2, ref zfloat __dis)
		{
            long lx = __p1.x.value - __p2.x.value;
            long ly = __p1.y.value - __p2.y.value;
            long lz = __p1.z.value - __p2.z.value;

            long lSqrtMag = (lx * lx + ly * ly + lz * lz);

            if (lSqrtMag > __dis.value * __dis.value)
            {
                return false;
            }
            else
            {
                return true;
            }

            ////zVector3 vec = __p1 - __p2;
            //long lx = zMathf.Abs(__p1.x.value - __p2.x.value);
            //long ly = zMathf.Abs(__p1.y.value - __p2.y.value);
            //long lz = zMathf.Abs(__p1.z.value - __p2.z.value);
            ////
            //if (lx > __dis.value || ly > __dis.value || lz > __dis.value)
            //{
            //    return false;
            //}

            //long lPreDis = lx + ly + lz;

            //if (lPreDis> __dis.value * DIS_PRE_CHECK_VALUE.value / zfloat.SCALE_10000)
            //{
            //    return false;
            //}
            //else
            //{
            //   /* tempVec.x.value = lx;
            //    tempVec.y.value = ly;
            //    tempVec.z.value = lz;*/

            //    long lSqrtMag = (lx * lx + ly * ly + lz * lz) / zfloat.SCALE_10000;

            //    if (lSqrtMag > __dis.value * __dis.value / zfloat.SCALE_10000)
            //    {
            //        return false;
            //    }
            //    else
            //    {
            //        return true;
            //    }
            //}
		}

		public static bool DistanceCheck(zVector3 __p1, zVector3 __p2, zfloat __dis)
		{
			return DistanceCheckRef(ref __p1, ref __p2, ref __dis);
		}

		/// <summary>
		/// 直线和圆相交
		/// </summary>
		/// <param name="__p1">有解则返回解，无解则返回NULL</param>
		/// <param name="__p2"></param>
		/// <param name="__lineStart"></param>
		/// <param name="__lineEnd"></param>
		/// <param name="__Center">圆心</param>
		/// <param name="__radius">半径</param>
		/// <param name="__haveRange">是否要求交点在线段范围内</param>
		/*public static void LineCrossCircle(out zVector3 __p1, out zVector3 __p2, zVector3 __lineStart, zVector3 __lineEnd, zVector3 __Center, zfloat __radius, bool __haveRange = true)
		{
			__lineStart.y = zfloat.Zero;
			__lineEnd.y = zfloat.Zero;
			__Center.y = zfloat.Zero;
			if(__lineStart.x == __lineEnd.x)
			{
				__lineEnd.x += zfloat.CreateFloat(1);
			}
			zfloat k = (__lineStart.z - __lineEnd.z) / (__lineStart.x - __lineEnd.x);
			if(k > 100)
			{
				k = new zfloat(100);
			}
			if(k < -100)
			{
				k = new zfloat(-100);
			}
			zfloat c = __lineStart.z - k * __lineStart.x;
			zfloat a = __Center.x;
			zfloat b = __Center.z;

			zfloat ta = 1 + k * k;
			zfloat tb = 2 * (k * c - k * b - a);
			zfloat tc = a * a + (c - b) * (c - b) - __radius * __radius; 

			zfloat delta = tb * tb - 4 * ta * tc;
			if(delta >= 0)
			{
				zfloat sqrtDelta = zMathf.Sqrt(delta);
				__p1.x = (-tb + sqrtDelta) / (2 * ta);
				__p1.y = zfloat.Zero;
				__p1.z = k * __p1.x + c;
				__p2.x = (-tb - sqrtDelta) / (2 * ta);
				__p2.y = zfloat.Zero;
				__p2.z = k * __p2.x + c;
				if (__haveRange)
				{
					if (!PointInSegment(__lineStart, __lineEnd, __p1))
					{
						__p1 = zVector3.NULL;
					}
					if (!PointInSegment(__lineStart, __lineEnd, __p2))
					{
						__p2 = zVector3.NULL;
					}
				}
			}
			else
			{
				__p1 = zVector3.NULL;
				__p2 = zVector3.NULL;
			}
			
		}*/
	}
		
}