using System;
namespace zUnity
{
	[Serializable]
	public class zRandom
	{

		private const long multiplier = 0x5DEECE66DL;
		private const long addend = 0xBL;
		private const long mask = (1L << 48) - 1;

		private static long seedUniquifier = 8682522807148012L;

		//public System.Random random;
		//private UInt64 seed;

		private long sseed;

		public zRandom()
			: this(SeedUniquifier() ^ Environment.TickCount)
		{
		}

		public zRandom(long seed)
		{
			this.sseed = InitialScramble(seed);

			//zUDebug.LogError("Seed:" + seed + " SSeed:" + this.sseed);
		}

		private static long SeedUniquifier()
		{
			while (true)
			{
				long current = seedUniquifier;
				long next = current * 181783497276652981L;
				if (current != next)
				{
					seedUniquifier = next;
					return next;
				}
			}
		}

		private static long InitialScramble(long seed)
		{
			return (seed ^ multiplier) & mask;
		}


		public void SetSeed(long seed)
		{
			this.sseed = InitialScramble(seed);
		}

		protected int Next(int bits)
		{
			long oldseed, nextseed;

			long seed = this.sseed;

			oldseed = seed;
			nextseed = (oldseed * multiplier + addend) & mask;
			sseed = nextseed;
			var returnvalue = (int)(nextseed >> (48 - bits));
			//ConsoleToFile.WriteLine(returnvalue.ToString());
			return returnvalue;
		}

		public void NextBytes(byte[] bytes)
		{
			for (int i = 0, len = bytes.Length; i < len; i++)
			{
				for (int rnd = NextInt(), n = Math.Min(len - i, sizeof(int) / sizeof(byte)); n-- > 0; rnd >>= sizeof(byte))
				{
					bytes[i++] = (byte)rnd;
				}
			}
		}

		public int NextInt()
		{
			return Next(32);
		}

		public int NextInt(int n)
		{
			if (n == 0)
				return 0;
			if (n < 0)
				throw new Exception("n must be positive");

			if ((n & -n) == n)  // i.e., n is a power of 2
				return (int)((n * (long)Next(31)) >> 31);

			int bits, val;
			do
			{
				bits = Next(31);
				val = bits % n;
			} while (bits - val + (n - 1) < 0);
			return val;
		}


		public long NextLong()
		{
			return (long)(Next(32) << 32) + Next(32);
		}

		//public zRandom(UInt64 seed)
		//{
		//	random = new Random((int)seed);

		//	this.seed = (seed ^ 0x5DEECE66DUL) & ((1UL << 48) - 1);
		//}


		//public int NextInt(int n)
		//{
		//	return random.Next();
		//if (n <= 0) throw new ArgumentException("n must be positive");

		//if ((n & -n) == n)  // i.e., n is a power of 2
		//	return (int)((n * (long)Next(31)) >> 31);

		//long bits, val;
		//do
		//{
		//	bits = Next(31);
		//	val = bits % (UInt32)n;
		//}
		//while (bits - val + (n - 1) < 0);

		//return (int)val;
		//}

		//public long NextLong()
		//{
		//	throw new NotImplementedException();
		//	//return ((long)(Next(32)) << 32) + Next(32);
		//}
		//protected UInt32 Next(int bits)
		//{
		//	//random.NextBytes();
		//	seed = (seed * 0x5DEECE66DL + 0xBL) & ((1L << 48) - 1);

		//	return (UInt32)(seed >> (48 - bits));
		//}

		/// <summary>
		/// 设置随机种子
		/// </summary>
		/// <param name="seed"></param>
		//public void SetSeed(ulong seed)
		//{
		//	this.seed = seed;
		//}

		/// <summary>
		/// 获取一个在圆上的随机点
		/// </summary>
		public zVector2 onUnitCircle
		{
			get
			{
				var angle = Range(0, 360);

				return new zVector2(zMathf.CosAngle(angle), zMathf.SinAngle(angle));
			}
		}

		public zVector2 inUnitCircle
		{
			get
			{
				var angle = Range(0, 360);

				var radius = Range(0, 100);

				return (new zVector2(zMathf.CosAngle(angle), zMathf.SinAngle(angle)) * radius) / (zfloat.One * 100);
			}
		}

		/// <summary>
		/// 范围随机
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <returns></returns>
		public int Range(int minValue, int maxValue)
		{
			var returnValue = NextInt(maxValue - minValue) + minValue;

			//zUDebug.LogError("RandomValue" + returnValue);

			return returnValue;
			//return random.Next(minValue, maxValue);
			//return (int)Range((long)minValue, (long)maxValue);
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="minValue"></param>
		/// <param name="maxValue"></param>
		/// <returns></returns>
		public long Range(long minValue, long maxValue)
		{
			//return NextLong(maxValue - minValue) + minValue;
			return Range((int)minValue, (int)maxValue);
			//throw new NotImplementedException();
			//if (minValue == maxValue)
			//{
			//	return minValue;
			//}
			//if (minValue > maxValue)
			//	throw new ArgumentOutOfRangeException("minValue不能大于MaxValue");

			//var range = (maxValue - minValue);

			//if (range < (long)Int32.MaxValue)
			//{
			//	return (NextLong() % range + minValue);
			//}
			//else
			//{
			//	throw new ArgumentOutOfRangeException("范围不能超过Int的最大值");
			//}
		}

		public zfloat Range(zfloat minValue, zfloat maxValue)
		{
			zfloat v;
			v.value = Range(minValue.value, maxValue.value);
			return v;
			//throw new NotImplementedException();
			//return new zfloat(Range(minValue.value, maxValue.value));
		}
	}
}