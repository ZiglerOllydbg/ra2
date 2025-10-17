using System;
using System.Diagnostics;
using UnityEngine;

public class zUDebug
{
	public static bool enable = true;

	[Conditional("ZLOGENABLE")]
	public static void Log(object obj)
	{
#if !ONLYCSHARP
		if (enable)
			UnityEngine.Debug.Log(obj);
#else
		Console.WriteLine(obj.ToString());
#endif
	}

	[Conditional("ZLOGENABLE")]
	public static void LogWarning(object s)
	{
#if !ONLYCSHARP
		if (enable)
			UnityEngine.Debug.LogWarning(s);
#else
		//Console.WriteLine(s.ToString());
#endif
	}

	public static void LogError(object s)
	{
#if !ONLYCSHARP
		if (enable)
			UnityEngine.Debug.LogError(s);
#else
		Console.WriteLine(s.ToString());
#endif
	}
}
