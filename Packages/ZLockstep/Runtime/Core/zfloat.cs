using System.Runtime.InteropServices;

[System.Serializable]
[System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]
[System.Runtime.InteropServices.ComVisible(true)]
public struct zfloat
//: IComparable, IFormattable, IConvertible
//, IComparable<zfloat>, IEquatable<zfloat>
{
	public static readonly zfloat One = new zfloat(1);
	public static readonly zfloat Zero = new zfloat(0);
	public static readonly zfloat Two = new zfloat(2);
	public static readonly zfloat Half = new zfloat(0, 5000);
	public static readonly zfloat Quarter = new zfloat(0, 2500);
	public static readonly zfloat NegativeOne = new zfloat(-1);
	public static readonly zfloat OneHalf = new zfloat(1, 5000);
	public static readonly zfloat SqrtTwo = new zfloat(1, 4142);
	public static readonly zfloat Hundred = new zfloat(100);

	/// <summary>
	/// 约等于
	/// </summary>
	/// <param name="value"></param>
	/// <param name="target"></param>
	/// <returns></returns>
	public static bool Approximate(zfloat value, zfloat target)
	{
		long t = value.value - target.value;

		t = t >= 0 ? t : -t;

		return t < 10;
	}

	public bool Approximate(zfloat target)
	{
		return Approximate(this, target);
	}

	/// <summary>
	/// 缩放值
	/// </summary>
	public const long SCALE_10000 = 10000;
	public const long SCALE_1000 = 1000;
	/// <summary>
	/// 缩放值的平方
	/// </summary>
	public const long SCALE_100000000 = 100000000;

	public static readonly zfloat Infinity = new zfloat(999999999999999999L);

	public static readonly zfloat Epsilon = new zfloat(0, 1);

	/// <summary>
	/// 缩放系数的平方根
	/// </summary>
	public const int SCALE_100 = 100;

	/// <summary>
	/// 四分之一
	/// </summary>
	public const int SCALE_QUARTER = 10;

	/// <summary>
	/// 一个扩大10000倍的数字
	/// </summary>
	public long value;


	public zfloat(int __value)
	{
		value = __value * SCALE_10000;
	}

	public zfloat(long __value)
	{
		value = __value * SCALE_10000;
	}

	/// <summary>
	/// 返回第一个带缩放的乘法结果
	/// 主要用于数学库内部优化使用
	/// </summary>
	/// <param name="n1"></param>
	/// <param name="n2"></param>
	/// <returns></returns>
	public static long Multiply_Scale(ref zfloat n1, ref zfloat n2)
	{
		return n1.value * n2.value;
	}

	/// <summary>
	/// 分别传入整数部分和小数部分
	/// 小数部分扩大10000倍
	/// </summary>
	/// <param name="intPart"></param>
	/// <param name="decimalsPart_10000"></param>
	public zfloat(int intPart, int decimalsPart_10000)
	{
		value = intPart * SCALE_10000 + decimalsPart_10000;
	}

	public zfloat(int intPart, long decimalsPart_10000)
	{
		value = intPart * SCALE_10000 + decimalsPart_10000;
	}

	public zfloat(zfloat __value)
	{
		value = __value.value;
	}

	/// <summary>
	/// 传入一个扩大10000倍的小数
	/// </summary>
	/// <param name="__value"></param>
	/// <returns></returns>
	public static zfloat CreateFloat(long __value)
	{
		zfloat zf;// = new zfloat();
		zf.value = __value;
		return zf;
	}

	/// <summary>
	/// 返回小数部分
	/// </summary>
	/// <param name="num"></param>
	/// <returns></returns>
	public zfloat GeDecimals()
	{
		return zfloat.CreateFloat(value % SCALE_10000);
	}

	/// <summary>
	/// 返回整数部分
	/// </summary>
	/// <returns></returns>
	public int GetInteger()
	{
		return (int)(value / SCALE_10000);
	}

	/// <summary>
	/// zfloat数组，转换到float数组
	/// </summary>
	/// <returns></returns>
	public static float[] ToFloatArray(zfloat[] values)
	{
		if (values == null)
		{
			return null;
		}
		float[] fs = new float[values.Length];
		for (int i = 0; i < fs.Length; ++i)
		{
			fs[i] = (float)values[i];
		}
		return fs;
	}

	#region 加法
	internal static void Addition(ref zfloat lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = lhs.value + rhs.value;
	}
	internal static void Addition(ref int lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = (lhs * SCALE_10000) + rhs.value;
	}
	internal static void Addition(ref zfloat lhs, ref int rhs, ref zfloat result)
	{
		result.value = lhs.value + (rhs * SCALE_10000);
	}
	internal static void Addition(ref long lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = (lhs * SCALE_10000) + rhs.value;
	}
	internal static void Addition(ref zfloat lhs, ref long rhs, ref zfloat result)
	{
		result.value = lhs.value + (rhs * SCALE_10000);
	}

	public static zfloat operator +(zfloat lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs.value + rhs.value;
		return result;
	}

	public static zfloat operator +(int lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs * SCALE_10000 + rhs.value;
		return result;
	}

	public static zfloat operator +(zfloat lhs, int rhs)
	{
		zfloat result;
		result.value = lhs.value + rhs * SCALE_10000;
		return result;
	}
	#endregion

	#region 减法
	internal static void Sub(ref zfloat lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = lhs.value - rhs.value;
	}
	internal static void Sub(ref int lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = (lhs * SCALE_10000) - rhs.value;
	}
	internal static void Sub(ref zfloat lhs, ref int rhs, ref zfloat result)
	{
		result.value = lhs.value - (rhs * SCALE_10000);
	}
	internal static void Sub(ref long lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = (lhs * SCALE_10000) - rhs.value;
	}
	internal static void Sub(ref zfloat lhs, ref long rhs, ref zfloat result)
	{
		result.value = lhs.value - (rhs * SCALE_10000);
	}
	public static zfloat operator -(zfloat lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs.value - rhs.value;
		return result;
	}

	public static zfloat operator -(int lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs * SCALE_10000 - rhs.value;
		return result;
	}

	public static zfloat operator -(zfloat lhs, int rhs)
	{
		zfloat result;
		result.value = lhs.value - rhs * SCALE_10000;
		return result;
	}
	#endregion

	#region 乘法
	internal static void Multiply(ref zfloat lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = lhs.value * rhs.value / SCALE_10000;
	}
	internal static void Multiply(ref int lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = lhs * rhs.value;
	}
	internal static void Multiply(ref zfloat lhs, ref int rhs, ref zfloat result)
	{
		result.value = lhs.value * rhs;
	}
	internal static void Multiply(ref long lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = lhs * rhs.value;
	}
	internal static void Multiply(ref zfloat lhs, ref long rhs, ref zfloat result)
	{
		result.value = lhs.value * rhs;
	}

	public static zfloat operator *(zfloat lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs.value * rhs.value / SCALE_10000;
		return result;
	}

	public static zfloat operator *(int lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs * rhs.value;
		return result;
	}

	public static zfloat operator *(zfloat lhs, int rhs)
	{
		zfloat result;
		result.value = lhs.value * rhs;
		return result;
	}

	public static zfloat operator *(long lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs * rhs.value;
		return result;
	}

	public static zfloat operator *(zfloat lhs, long rhs)
	{
		zfloat result;
		result.value = lhs.value * rhs;
		return result;
	}
	#endregion

	#region 除法
	internal static void Div(ref zfloat lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = lhs.value * SCALE_10000 / rhs.value;
	}
	internal static void Div(ref int lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = lhs * SCALE_100000000 / rhs.value;
	}
	internal static void Div(ref zfloat lhs, ref int rhs, ref zfloat result)
	{
		result.value = lhs.value / rhs;
	}
	internal static void Div(ref long lhs, ref zfloat rhs, ref zfloat result)
	{
		result.value = lhs * SCALE_100000000 / rhs.value;
	}
	internal static void Div(ref zfloat lhs, ref long rhs, ref zfloat result)
	{
		result.value = lhs.value / rhs;
	}

	public static zfloat operator /(zfloat lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs.value * SCALE_10000 / rhs.value;
		return result;
	}

	public static zfloat operator /(int lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs * SCALE_100000000 / rhs.value;
		return result;
	}

	public static zfloat operator /(zfloat lhs, int rhs)
	{
		zfloat result;
		result.value = lhs.value / rhs;
		return result;
	}

	public static zfloat operator /(long lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs * SCALE_100000000 / rhs.value;
		return result;
	}

	public static zfloat operator /(zfloat lhs, long rhs)
	{
		zfloat result;
		result.value = lhs.value / rhs;
		return result;
	}
	#endregion

	#region 求余
	public static zfloat operator %(zfloat lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs.value % rhs.value;
		return result;
	}

	public static zfloat operator %(int lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs * SCALE_10000 % rhs.value;
		return result;
	}

	public static zfloat operator %(zfloat lhs, int rhs)
	{
		zfloat result;
		result.value = lhs.value % (rhs * SCALE_10000);
		return result;
	}

	public static zfloat operator %(long lhs, zfloat rhs)
	{
		zfloat result;
		result.value = lhs * SCALE_10000 % rhs.value;
		return result;
	}

	public static zfloat operator %(zfloat lhs, long rhs)
	{
		zfloat result;
		result.value = lhs.value % (rhs * SCALE_10000);
		return result;
	}
	#endregion

	#region 逻辑运算符
	public static zfloat operator -(zfloat v)
	{
		return zfloat.CreateFloat(-v.value);
	}
	public static zfloat operator +(zfloat v)
	{
		return v;
	}
	public static bool operator ==(zfloat lhs, zfloat rhs)
	{
		return lhs.value == rhs.value;
	}
	public static bool operator ==(int lhs, zfloat rhs)
	{
		return lhs * SCALE_10000 == rhs.value;
	}
	public static bool operator ==(zfloat lhs, int rhs)
	{
		return lhs.value == rhs * SCALE_10000;
	}

	public static bool operator !=(zfloat lhs, zfloat rhs)
	{
		return lhs.value != rhs.value;
	}
	public static bool operator !=(int lhs, zfloat rhs)
	{
		return lhs * SCALE_10000 != rhs.value;
	}
	public static bool operator !=(zfloat lhs, int rhs)
	{
		return lhs.value != rhs * SCALE_10000;
	}

	public static bool operator >(zfloat lhs, zfloat rhs)
	{
		return lhs.value > rhs.value;
	}
	public static bool operator >(int lhs, zfloat rhs)
	{
		return lhs * SCALE_10000 > rhs.value;
	}
	public static bool operator >(zfloat lhs, int rhs)
	{
		return lhs.value > rhs * SCALE_10000;
	}

	public static bool operator >=(zfloat lhs, zfloat rhs)
	{
		return lhs.value >= rhs.value;
	}
	public static bool operator >=(int lhs, zfloat rhs)
	{
		return lhs * SCALE_10000 >= rhs.value;
	}
	public static bool operator >=(zfloat lhs, int rhs)
	{
		return lhs.value >= rhs * SCALE_10000;
	}

	public static bool operator <(zfloat lhs, zfloat rhs)
	{
		return lhs.value < rhs.value;
	}
	public static bool operator <(int lhs, zfloat rhs)
	{
		return lhs * SCALE_10000 >= rhs.value;
	}
	public static bool operator <(zfloat lhs, int rhs)
	{
		return lhs.value < rhs * SCALE_10000;
	}

	public static bool operator <=(zfloat lhs, zfloat rhs)
	{
		return lhs.value <= rhs.value;
	}
	public static bool operator <=(int lhs, zfloat rhs)
	{
		return lhs * SCALE_10000 <= rhs.value;
	}
	public static bool operator <=(zfloat lhs, int rhs)
	{
		return lhs.value <= rhs * SCALE_10000;
	}
	#endregion

	#region 自增 自减
	public static zfloat operator --(zfloat v)
	{
		v.value -= SCALE_10000;
		return v;
	}

	public static zfloat operator ++(zfloat v)
	{
		v.value += SCALE_10000;
		return v;
	}
	#endregion

	#region 类型转换
	public static explicit operator long(zfloat zf)
	{
		return zf.value / SCALE_10000;
	}

	public static explicit operator int(zfloat zf)
	{
		return (int)(zf.value / SCALE_10000);
	}

	public static explicit operator zfloat(int value)
	{
		return new zfloat(value);
	}

	public static explicit operator zfloat(long value)
	{
		return new zfloat(value);
	}

	public static explicit operator float(zfloat zf)
	{
		return zf.value / (float)SCALE_10000;
	}

	public readonly static char[] splitChars = new char[] { '.' };
	public static zfloat Parse(string s)
	{
		//var result = 1L;
		string[] nums = s.Split(splitChars);
		int intPart = 0;
		int decimalsPart_10000 = 0;
		if (nums.Length == 1)
		{
			int.TryParse(nums[0], out intPart);
		}
		else if (nums.Length == 2)
		{
			int.TryParse(nums[0], out intPart);
			int decCount = nums[1].ToCharArray().Length;
			if (decCount < 4)
			{
				decCount = 4 - decCount;
				for (int i = 0; i < decCount; ++i)
				{
					nums[1] += "0";
				}
			}
			int.TryParse(nums[1], out decimalsPart_10000);
		}

		return new zfloat(intPart, decimalsPart_10000);
	}

	/// <summary>
	/// 重载tostring
	/// 会在debug断点时，显示真实的小数，方便调试
	/// </summary>
	/// <returns></returns>
	public override string ToString()
	{
		/* long a = (value % 10);
		 long b = (value % 100) / 10;
		 long c = (value % 1000) / 100;
		 long d = (value % 10000) / 1000;

		 return value / SCALE + "." + zMathf.Abs(d) + "" + zMathf.Abs(c) + "" + zMathf.Abs(b) + "" + zMathf.Abs(a);*/

		return "" + (value / (double)SCALE_10000);
	}

	#endregion

	public static void TestNullCall()
	{

	}



	public int CompareTo(zfloat zfloat)
	{
		return value.CompareTo(zfloat.value);
	}

	public static bool TryParse(string s, out zfloat v)
	{
		string[] nums = s.Split(splitChars);
		int intPart = 0;
		int decimalsPart_10000 = 0;
		if (nums.Length == 1)
		{
			int.TryParse(nums[0], out intPart);
		}
		else if (nums.Length == 2)
		{
			int.TryParse(nums[0], out intPart);
			int decCount = nums[1].ToCharArray().Length;
			if (decCount < 4)
			{
				decCount = 4 - decCount;
				for (int i = 0; i < decCount; ++i)
				{
					nums[1] += "0";
				}
			}
			if (!int.TryParse(nums[1], out decimalsPart_10000))
			{
				v = zfloat.Zero;

				return false;
			}
		}

		v = new zfloat(intPart, decimalsPart_10000);

		return true;
	}
}
