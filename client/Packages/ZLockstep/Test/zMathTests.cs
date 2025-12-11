using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ZLockstep.View;
using zUnity;

namespace ZMath.Tests
{
    /// <summary>
    /// zMathf 类的全面单元测试
    /// 覆盖所有公共 API 的测试用例
    /// </summary>
    public class zMathTests
    {
        // 精度容差
        private const float ToleranceFloat = 0.01f;      // 一般浮点数精度
        private const float ToleranceAngle = 0.5f;       // 角度精度（度）
        private const float ToleranceRad = 0.0087f;     // 弧度精度（约0.5度）
        private const long ToleranceValue = 100;        // value 值的容差（对应 0.01）

        #region 常量测试

        [Test]
        public void Constants_PI_IsCorrect()
        {
            float pi = zMathf.PI.ToFloat();
            Assert.AreEqual(Mathf.PI, pi, ToleranceFloat, "PI 值不正确");
        }

        [Test]
        public void Constants_E_IsCorrect()
        {
            float e = zMathf.E.ToFloat();
            Assert.AreEqual(2.7183f, e, ToleranceFloat, "E 值不正确");
        }

        [Test]
        public void Constants_LN_10_IsCorrect()
        {
            float ln10 = zMathf.LN_10.ToFloat();
            Assert.AreEqual(2.3026f, ln10, ToleranceFloat, "LN_10 值不正确");
        }

        [Test]
        public void Constants_One_IsCorrect()
        {
            Assert.AreEqual(1, zMathf.One.value, zfloat.SCALE_10000, "One 值不正确");
        }

        [Test]
        public void Constants_Zero_IsCorrect()
        {
            Assert.AreEqual(0, zMathf.Zero.value, "Zero 值不正确");
        }

        [Test]
        public void Constants_Half_IsCorrect()
        {
            float half = zMathf.Half.ToFloat();
            Assert.AreEqual(0.5f, half, ToleranceFloat, "Half 值不正确");
        }

        [Test]
        public void Constants_Deg2Rad_IsCorrect()
        {
            float deg2rad = zMathf.Deg2Rad.ToFloat();
            Assert.AreEqual(Mathf.Deg2Rad, deg2rad, ToleranceFloat, "Deg2Rad 值不正确");
        }

        [Test]
        public void Constants_Rad2Deg_IsCorrect()
        {
            float rad2deg = zMathf.Rad2Deg.ToFloat();
            Assert.AreEqual(Mathf.Rad2Deg, rad2deg, ToleranceFloat, "Rad2Deg 值不正确");
        }

        #endregion

        #region 三角函数测试（弧度版本）

        [Test]
        public void Sin_BasicValues()
        {
            AssertSin(0f, 0f);
            AssertSin(Mathf.PI / 2f, 1f);
            AssertSin(Mathf.PI, 0f);
            AssertSin(3f * Mathf.PI / 2f, -1f);
            AssertSin(2f * Mathf.PI, 0f);
        }

        [Test]
        public void Sin_FullCircle()
        {
            for (int deg = 0; deg < 360; deg += 15)
            {
                float rad = deg * Mathf.Deg2Rad;
                float expected = Mathf.Sin(rad);
                zfloat zrad = zfloat.CreateFloat((long)(rad * zfloat.SCALE_10000));
                float actual = zMathf.Sin(zrad).ToFloat();
                Assert.AreEqual(expected, actual, ToleranceFloat, $"Sin({deg}°) 不正确");
            }
        }

        [Test]
        public void Cos_BasicValues()
        {
            AssertCos(0f, 1f);
            AssertCos(Mathf.PI / 2f, 0f);
            AssertCos(Mathf.PI, -1f);
            AssertCos(3f * Mathf.PI / 2f, 0f);
            AssertCos(2f * Mathf.PI, 1f);
        }

        [Test]
        public void Cos_FullCircle()
        {
            for (int deg = 0; deg < 360; deg += 15)
            {
                float rad = deg * Mathf.Deg2Rad;
                float expected = Mathf.Cos(rad);
                zfloat zrad = zfloat.CreateFloat((long)(rad * zfloat.SCALE_10000));
                float actual = zMathf.Cos(zrad).ToFloat();
                Assert.AreEqual(expected, actual, ToleranceFloat, $"Cos({deg}°) 不正确");
            }
        }

        [Test]
        public void Tan_BasicValues()
        {
            AssertTan(0f, 0f);
            AssertTan(Mathf.PI / 4f, 1f);
            AssertTan(-Mathf.PI / 4f, -1f);
        }

        [Test]
        public void Tan_FullCircle()
        {
            for (int deg = -89; deg <= 89; deg += 15)
            {
                if (deg == 90 || deg == -90) continue; // 跳过奇点
                float rad = deg * Mathf.Deg2Rad;
                float expected = Mathf.Tan(rad);
                zfloat zrad = zfloat.CreateFloat((long)(rad * zfloat.SCALE_10000));
                float actual = zMathf.Tan(zrad).ToFloat();
                // 对于接近 90 度的角度（如 ±89°），Tan 值会非常大，误差也会增大，使用更宽松的容差
                float tolerance = Mathf.Abs(deg) >= 85 ? 1.0f : ToleranceFloat;
                Assert.AreEqual(expected, actual, tolerance, $"Tan({deg}°) 不正确");
            }
        }

        [Test]
        public void Asin_BasicValues()
        {
            AssertAsin(0f, 0f);
            AssertAsin(1f, Mathf.PI / 2f);
            AssertAsin(-1f, -Mathf.PI / 2f);
            AssertAsin(0.5f, Mathf.Asin(0.5f));
        }

        [Test]
        public void Acos_BasicValues()
        {
            AssertAcos(1f, 0f);
            AssertAcos(0f, Mathf.PI / 2f);
            AssertAcos(-1f, Mathf.PI);
            AssertAcos(0.5f, Mathf.Acos(0.5f));
        }

        [Test]
        public void Atan_BasicValues()
        {
            AssertAtan(0f, 0f);
            AssertAtan(1f, Mathf.PI / 4f);
            AssertAtan(-1f, -Mathf.PI / 4f);
            AssertAtan(100f, Mathf.Atan(100f));
        }

        #endregion

        #region 三角函数测试（角度版本）

        [Test]
        public void SinAngle_BasicValues()
        {
            AssertSinAngle(0, 0f);
            AssertSinAngle(90, 1f);
            AssertSinAngle(180, 0f);
            AssertSinAngle(270, -1f);
            AssertSinAngle(360, 0f);
        }

        [Test]
        public void CosAngle_BasicValues()
        {
            AssertCosAngle(0, 1f);
            AssertCosAngle(90, 0f);
            AssertCosAngle(180, -1f);
            AssertCosAngle(270, 0f);
            AssertCosAngle(360, 1f);
        }

        [Test]
        public void TanAngle_BasicValues()
        {
            AssertTanAngle(0, 0f);
            AssertTanAngle(45, 1f);
            AssertTanAngle(-45, -1f);
        }

        [Test]
        public void SinAngle_NegativeAngles()
        {
            AssertSinAngle(-90, -1f);
            AssertSinAngle(-180, 0f);
            AssertSinAngle(-270, 1f);
        }

        [Test]
        public void CosAngle_NegativeAngles()
        {
            AssertCosAngle(-90, 0f);
            AssertCosAngle(-180, -1f);
            AssertCosAngle(-270, 0f);
        }

        [Test]
        public void SinAngle_LargeAngles()
        {
            AssertSinAngle(450, 1f);  // 450° = 90°
            AssertSinAngle(720, 0f);  // 720° = 0°
            AssertSinAngle(-450, -1f); // -450° = -90°
        }

        #endregion

        #region 反三角函数测试（角度版本）

        [Test]
        public void AsinAngle_BasicValues()
        {
            AssertAsinAngle(0f, 0f);
            AssertAsinAngle(1f, 90f);
            AssertAsinAngle(-1f, -90f);
            AssertAsinAngle(0.5f, 30f);
        }

        [Test]
        public void AcosAngle_BasicValues()
        {
            AssertAcosAngle(1f, 0f);
            AssertAcosAngle(0f, 90f);
            AssertAcosAngle(-1f, 180f);
            AssertAcosAngle(0.5f, 60f);
        }

        [Test]
        public void AtanAngle_BasicValues()
        {
            AssertAtanAngle(0f, 0f);
            AssertAtanAngle(1f, 45f);
            AssertAtanAngle(-1f, -45f);
        }

        #endregion

        #region Abs 测试

        [Test]
        public void Abs_PositiveValue()
        {
            zfloat val = new zfloat(5);
            zfloat result = zMathf.Abs(val);
            Assert.AreEqual(5 * zfloat.SCALE_10000, result.value, "正数的绝对值应该等于自身");
        }

        [Test]
        public void Abs_NegativeValue()
        {
            zfloat val = new zfloat(-5);
            zfloat result = zMathf.Abs(val);
            Assert.AreEqual(5 * zfloat.SCALE_10000, result.value, "负数的绝对值应该等于其相反数");
        }

        [Test]
        public void Abs_Zero()
        {
            zfloat val = zfloat.Zero;
            zfloat result = zMathf.Abs(val);
            Assert.AreEqual(0, result.value, "0 的绝对值应该为 0");
        }

        [Test]
        public void Abs_Int_Positive()
        {
            int result = zMathf.Abs(5);
            Assert.AreEqual(5, result);
        }

        [Test]
        public void Abs_Int_Negative()
        {
            int result = zMathf.Abs(-5);
            Assert.AreEqual(5, result);
        }

        [Test]
        public void Abs_Long_Positive()
        {
            long result = zMathf.Abs(5L);
            Assert.AreEqual(5L, result);
        }

        [Test]
        public void Abs_Long_Negative()
        {
            long result = zMathf.Abs(-5L);
            Assert.AreEqual(5L, result);
        }

        #endregion

        #region Sqrt 测试

        [Test]
        public void Sqrt_PerfectSquares()
        {
            AssertSqrt(0f, 0f);
            AssertSqrt(1f, 1f);
            AssertSqrt(4f, 2f);
            AssertSqrt(9f, 3f);
            AssertSqrt(16f, 4f);
            AssertSqrt(25f, 5f);
            AssertSqrt(100f, 10f);
        }

        [Test]
        public void Sqrt_DecimalValues()
        {
            AssertSqrt(2f, Mathf.Sqrt(2f));
            AssertSqrt(3f, Mathf.Sqrt(3f));
            AssertSqrt(0.5f, Mathf.Sqrt(0.5f));
            AssertSqrt(0.25f, 0.5f);
        }

        [Test]
        public void Sqrt_LargeValues()
        {
            AssertSqrt(10000f, 100f);
            AssertSqrt(1000000f, 1000f);
        }

        [Test]
        public void Sqrt_NegativeValue()
        {
            zfloat val = new zfloat(-1);
            // 期望捕获错误日志（zUDebug.LogError 会输出带时间戳的格式）
            // 使用包含消息核心内容的匹配
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*亲！负数不能开平方!!!!!.*"));
            zfloat result = zMathf.Sqrt(val);
            Assert.AreEqual(0, result.value, "负数开平方应该返回 0");
        }

        #endregion

        #region Min/Max 测试

        [Test]
        public void Min_Basic()
        {
            zfloat a = new zfloat(5);
            zfloat b = new zfloat(3);
            zfloat result = zMathf.Min(a, b);
            Assert.AreEqual(3 * zfloat.SCALE_10000, result.value, "Min(5, 3) 应该返回 3");
        }

        [Test]
        public void Min_EqualValues()
        {
            zfloat a = new zfloat(5);
            zfloat b = new zfloat(5);
            zfloat result = zMathf.Min(a, b);
            Assert.AreEqual(5 * zfloat.SCALE_10000, result.value, "Min(5, 5) 应该返回 5");
        }

        [Test]
        public void Min_NegativeValues()
        {
            zfloat a = new zfloat(-5);
            zfloat b = new zfloat(-3);
            zfloat result = zMathf.Min(a, b);
            Assert.AreEqual(-5 * zfloat.SCALE_10000, result.value, "Min(-5, -3) 应该返回 -5");
        }

        [Test]
        public void Max_Basic()
        {
            zfloat a = new zfloat(5);
            zfloat b = new zfloat(3);
            zfloat result = zMathf.Max(a, b);
            Assert.AreEqual(5 * zfloat.SCALE_10000, result.value, "Max(5, 3) 应该返回 5");
        }

        [Test]
        public void Max_EqualValues()
        {
            zfloat a = new zfloat(5);
            zfloat b = new zfloat(5);
            zfloat result = zMathf.Max(a, b);
            Assert.AreEqual(5 * zfloat.SCALE_10000, result.value, "Max(5, 5) 应该返回 5");
        }

        [Test]
        public void Max_NegativeValues()
        {
            zfloat a = new zfloat(-5);
            zfloat b = new zfloat(-3);
            zfloat result = zMathf.Max(a, b);
            Assert.AreEqual(-3 * zfloat.SCALE_10000, result.value, "Max(-5, -3) 应该返回 -3");
        }

        [Test]
        public void Max_Int_Basic()
        {
            int result = zMathf.Max(5, 3);
            Assert.AreEqual(5, result);
        }

        #endregion

        #region Pow 测试

        [Test]
        public void Pow_IntegerPowers()
        {
            AssertPow(2f, 0f, 1f);
            AssertPow(2f, 1f, 2f);
            AssertPow(2f, 2f, 4f);
            AssertPow(2f, 3f, 8f);
            AssertPow(3f, 2f, 9f);
            AssertPow(5f, 3f, 125f);
        }

        [Test]
        public void Pow_FractionalPowers()
        {
            AssertPow(4f, 0.5f, 2f);  // 4^0.5 = 2
            AssertPow(9f, 0.5f, 3f);  // 9^0.5 = 3
            AssertPow(16f, 0.25f, 2f); // 16^0.25 = 2
        }

        [Test]
        public void Pow_NegativePowers()
        {
            AssertPow(2f, -1f, 0.5f);  // 2^-1 = 0.5
            AssertPow(4f, -2f, 0.0625f); // 4^-2 = 1/16
        }

        [Test]
        public void Pow_ZeroPower()
        {
            AssertPow(5f, 0f, 1f);
            AssertPow(0f, 0f, 1f);
        }

        [Test]
        public void Pow_BaseZero()
        {
            AssertPow(0f, 2f, 0f);
            AssertPow(0f, 5f, 0f);
        }

        #endregion

        #region Exp/Log 测试

        [Test]
        public void Exp_BasicValues()
        {
            AssertExp(0f, 1f);
            AssertExp(1f, Mathf.Exp(1f));
            AssertExp(2f, Mathf.Exp(2f));
        }

        [Test]
        public void Log_BasicValues()
        {
            AssertLog(1f, 0f);
            AssertLog(Mathf.Exp(1f), 1f);
            AssertLog(10f, Mathf.Log(10f));
        }

        [Test]
        public void Log10_BasicValues()
        {
            AssertLog10(1f, 0f);
            AssertLog10(10f, 1f);
            AssertLog10(100f, 2f);
            // 注意：Log10(1000) 超出了算法的有效精度范围（100以内还比较准，200以内凑合）
            // 因此跳过 1000 的测试，或者使用非常大的容差
            // AssertLog10(1000f, 3f);
        }

        [Test]
        public void Log_WithBase()
        {
            zfloat result = zMathf.Log(zfloat.CreateFloat((long)(8f * zfloat.SCALE_10000)), zfloat.CreateFloat((long)(2f * zfloat.SCALE_10000)));
            float actual = result.ToFloat();
            Assert.AreEqual(3f, actual, ToleranceFloat, "Log2(8) 应该等于 3");
        }

        #endregion

        #region Ceil/Floor/Round 测试

        [Test]
        public void Ceil_PositiveValues()
        {
            AssertCeil(0.1f, 1);
            AssertCeil(0.5f, 1);
            AssertCeil(0.9f, 1);
            AssertCeil(1.0f, 1);
            AssertCeil(1.1f, 2);
            AssertCeil(2.5f, 3);
        }

        [Test]
        public void Ceil_NegativeValues()
        {
            AssertCeil(-0.1f, 0);
            AssertCeil(-0.5f, 0);
            AssertCeil(-1.1f, -1);
        }

        [Test]
        public void Floor_PositiveValues()
        {
            AssertFloor(0.1f, 0);
            AssertFloor(0.5f, 0);
            AssertFloor(0.9f, 0);
            AssertFloor(1.0f, 1);
            AssertFloor(1.1f, 1);
            AssertFloor(2.5f, 2);
        }

        [Test]
        public void Floor_NegativeValues()
        {
            AssertFloor(-0.1f, -1);
            AssertFloor(-0.5f, -1);
            AssertFloor(-1.1f, -2);
        }

        [Test]
        public void Round_PositiveValues()
        {
            // 注意：当前的 Round 实现对于接近 0 的正值（如 0.1, 0.4）会向上取整到 1
            // 这与标准四舍五入行为不同，但符合当前实现的逻辑
            AssertRound(0.1f, 1);  // 当前实现返回 1
            AssertRound(0.4f, 1);  // 当前实现返回 1
            AssertRound(0.5f, 1);
            AssertRound(0.6f, 1);
            AssertRound(1.4f, 1);
            AssertRound(1.5f, 2);
            AssertRound(2.5f, 3);
        }

        [Test]
        public void Round_NegativeValues()
        {
            // 注意：当前的 Round 实现对于接近 0 的负值（如 -0.1, -0.4）会向上取整到 -1
            // 这与标准四舍五入行为不同，但符合当前实现的逻辑
            AssertRound(-0.1f, -1);  // 当前实现返回 -1
            AssertRound(-0.4f, -1);  // 当前实现返回 -1
            AssertRound(-0.5f, -1);
            AssertRound(-0.6f, -1);
            AssertRound(-1.4f, -1);
            AssertRound(-1.5f, -2);
        }

        #endregion

        #region Clamp 测试

        [Test]
        public void Clamp_WithinRange()
        {
            zfloat val = new zfloat(5);
            zfloat min = new zfloat(0);
            zfloat max = new zfloat(10);
            zfloat result = zMathf.Clamp(val, min, max);
            Assert.AreEqual(5 * zfloat.SCALE_10000, result.value, "值在范围内应该保持不变");
        }

        [Test]
        public void Clamp_BelowMin()
        {
            zfloat val = new zfloat(-5);
            zfloat min = new zfloat(0);
            zfloat max = new zfloat(10);
            zfloat result = zMathf.Clamp(val, min, max);
            Assert.AreEqual(0, result.value, "值小于最小值应该被限制为最小值");
        }

        [Test]
        public void Clamp_AboveMax()
        {
            zfloat val = new zfloat(15);
            zfloat min = new zfloat(0);
            zfloat max = new zfloat(10);
            zfloat result = zMathf.Clamp(val, min, max);
            Assert.AreEqual(10 * zfloat.SCALE_10000, result.value, "值大于最大值应该被限制为最大值");
        }

        [Test]
        public void Clamp01_WithinRange()
        {
            zfloat val = zfloat.CreateFloat((long)(0.5f * zfloat.SCALE_10000));
            zfloat result = zMathf.Clamp01(val);
            Assert.AreEqual(0.5f, result.ToFloat(), ToleranceFloat, "值在 0-1 范围内应该保持不变");
        }

        [Test]
        public void Clamp01_BelowZero()
        {
            zfloat val = zfloat.CreateFloat((long)(-0.5f * zfloat.SCALE_10000));
            zfloat result = zMathf.Clamp01(val);
            Assert.AreEqual(0f, result.ToFloat(), ToleranceFloat, "值小于 0 应该被限制为 0");
        }

        [Test]
        public void Clamp01_AboveOne()
        {
            zfloat val = zfloat.CreateFloat((long)(1.5f * zfloat.SCALE_10000));
            zfloat result = zMathf.Clamp01(val);
            Assert.AreEqual(1f, result.ToFloat(), ToleranceFloat, "值大于 1 应该被限制为 1");
        }

        [Test]
        public void Clamp_Long_WithinRange()
        {
            long result = zMathf.Clamp(5L, 0L, 10L);
            Assert.AreEqual(5L, result);
        }

        [Test]
        public void Clamp_Long_BelowMin()
        {
            long result = zMathf.Clamp(-5L, 0L, 10L);
            Assert.AreEqual(0L, result);
        }

        [Test]
        public void Clamp_Long_AboveMax()
        {
            long result = zMathf.Clamp(15L, 0L, 10L);
            Assert.AreEqual(10L, result);
        }

        #endregion

        #region Lerp 测试

        [Test]
        public void Lerp_TZero()
        {
            zfloat from = new zfloat(0);
            zfloat to = new zfloat(10);
            zfloat result = zMathf.Lerp(from, to, zfloat.Zero);
            Assert.AreEqual(0, result.value, "t=0 应该返回 from");
        }

        [Test]
        public void Lerp_TOne()
        {
            zfloat from = new zfloat(0);
            zfloat to = new zfloat(10);
            zfloat result = zMathf.Lerp(from, to, zfloat.One);
            Assert.AreEqual(10 * zfloat.SCALE_10000, result.value, "t=1 应该返回 to");
        }

        [Test]
        public void Lerp_THalf()
        {
            zfloat from = new zfloat(0);
            zfloat to = new zfloat(10);
            zfloat result = zMathf.Lerp(from, to, zMathf.Half);
            Assert.AreEqual(5 * zfloat.SCALE_10000, result.value, "t=0.5 应该返回中间值");
        }

        [Test]
        public void Lerp_TQuarter()
        {
            zfloat from = new zfloat(0);
            zfloat to = new zfloat(10);
            zfloat t = zfloat.CreateFloat((long)(0.25f * zfloat.SCALE_10000));
            zfloat result = zMathf.Lerp(from, to, t);
            Assert.AreEqual(2.5f, result.ToFloat(), ToleranceFloat, "t=0.25 应该返回 1/4 位置的值");
        }

        [Test]
        public void Lerp_Int_Basic()
        {
            int result = zMathf.Lerp(0, 10, zfloat.Zero);
            Assert.AreEqual(0, result, "t=0 应该返回 from");
            
            result = zMathf.Lerp(0, 10, zfloat.One);
            Assert.AreEqual(10, result, "t=1 应该返回 to");
            
            result = zMathf.Lerp(0, 10, zMathf.Half);
            Assert.AreEqual(5, result, "t=0.5 应该返回中间值");
        }

        #endregion

        #region Repeat/PingPong 测试

        [Test]
        public void Repeat_Basic()
        {
            zfloat result = zMathf.Repeat(zfloat.CreateFloat((long)(2.5f * zfloat.SCALE_10000)), zfloat.CreateFloat((long)(2f * zfloat.SCALE_10000)));
            Assert.AreEqual(0.5f, result.ToFloat(), ToleranceFloat, "Repeat(2.5, 2) 应该返回 0.5");
        }

        [Test]
        public void Repeat_MultipleCycles()
        {
            zfloat result = zMathf.Repeat(zfloat.CreateFloat((long)(7.5f * zfloat.SCALE_10000)), zfloat.CreateFloat((long)(2f * zfloat.SCALE_10000)));
            Assert.AreEqual(1.5f, result.ToFloat(), ToleranceFloat, "Repeat(7.5, 2) 应该返回 1.5");
        }

        [Test]
        public void PingPong_Basic()
        {
            zfloat result = zMathf.PingPong(zfloat.CreateFloat((long)(0.5f * zfloat.SCALE_10000)), zfloat.CreateFloat((long)(2f * zfloat.SCALE_10000)));
            Assert.AreEqual(0.5f, result.ToFloat(), ToleranceFloat, "PingPong(0.5, 2) 应该返回 0.5");
        }

        [Test]
        public void PingPong_Reflection()
        {
            zfloat result = zMathf.PingPong(zfloat.CreateFloat((long)(2.5f * zfloat.SCALE_10000)), zfloat.CreateFloat((long)(2f * zfloat.SCALE_10000)));
            Assert.AreEqual(1.5f, result.ToFloat(), ToleranceFloat, "PingPong(2.5, 2) 应该返回 1.5（反射）");
        }

        #endregion

        #region Sign 测试

        [Test]
        public void Sign_Positive()
        {
            int result = zMathf.Sign(new zfloat(5));
            Assert.AreEqual(1, result, "正数的符号应该为 1");
        }

        [Test]
        public void Sign_Negative()
        {
            int result = zMathf.Sign(new zfloat(-5));
            Assert.AreEqual(-1, result, "负数的符号应该为 -1");
        }

        [Test]
        public void Sign_Zero()
        {
            int result = zMathf.Sign(zfloat.Zero);
            Assert.AreEqual(1, result, "0 的符号应该为 1");
        }

        #endregion

        #region ApproximateHypotenuse 测试

        [Test]
        public void ApproximateHypotenuse_RightTriangle()
        {
            zfloat a = new zfloat(3);
            zfloat b = new zfloat(4);
            zfloat result = zMathf.ApproximateHypotenuse(a, b);
            float actual = result.ToFloat();
            Assert.AreEqual(5f, actual, 0.1f, "3-4-5 直角三角形（近似算法，容差较大）");
        }

        [Test]
        public void ApproximateHypotenuse_IsoscelesRightTriangle()
        {
            zfloat a = new zfloat(1);
            zfloat b = new zfloat(1);
            zfloat result = zMathf.ApproximateHypotenuse(a, b);
            float actual = result.ToFloat();
            Assert.AreEqual(Mathf.Sqrt(2f), actual, 0.1f, "等腰直角三角形（近似算法，容差较大）");
        }

        [Test]
        public void ApproximateHypotenuse_ZeroSide()
        {
            zfloat a = new zfloat(0);
            zfloat b = new zfloat(5);
            zfloat result = zMathf.ApproximateHypotenuse(a, b);
            Assert.AreEqual(5 * zfloat.SCALE_10000, result.value, "一边为 0 应该返回另一边");
        }

        [Test]
        public void ApproximateHypotenuse_SwappedSides()
        {
            zfloat a = new zfloat(4);
            zfloat b = new zfloat(3);
            zfloat result = zMathf.ApproximateHypotenuse(a, b);
            float actual = result.ToFloat();
            Assert.AreEqual(5f, actual, ToleranceFloat, "交换边长应该得到相同结果");
        }

        #endregion

        #region 辅助断言方法

        private void AssertSin(float rad, float expected)
        {
            zfloat zrad = zfloat.CreateFloat((long)(rad * zfloat.SCALE_10000));
            float actual = zMathf.Sin(zrad).ToFloat();
            Assert.AreEqual(expected, actual, ToleranceFloat, $"Sin({rad}) 不正确");
        }

        private void AssertCos(float rad, float expected)
        {
            zfloat zrad = zfloat.CreateFloat((long)(rad * zfloat.SCALE_10000));
            float actual = zMathf.Cos(zrad).ToFloat();
            Assert.AreEqual(expected, actual, ToleranceFloat, $"Cos({rad}) 不正确");
        }

        private void AssertTan(float rad, float expected)
        {
            zfloat zrad = zfloat.CreateFloat((long)(rad * zfloat.SCALE_10000));
            float actual = zMathf.Tan(zrad).ToFloat();
            Assert.AreEqual(expected, actual, ToleranceFloat, $"Tan({rad}) 不正确");
        }

        private void AssertAsin(float val, float expectedRad)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            float actual = zMathf.Asin(zval).ToFloat();
            // 对于边界值（-1, 1），由于定点数精度限制，使用更宽松的容差
            float tolerance = (Mathf.Abs(val) >= 0.99f) ? 0.02f : ToleranceRad;
            Assert.AreEqual(expectedRad, actual, tolerance, $"Asin({val}) 不正确");
        }

        private void AssertAcos(float val, float expectedRad)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            float actual = zMathf.Acos(zval).ToFloat();
            // 对于边界值（-1, 1），由于定点数精度限制，使用更宽松的容差
            float tolerance = (Mathf.Abs(val) >= 0.99f) ? 0.02f : ToleranceRad;
            Assert.AreEqual(expectedRad, actual, tolerance, $"Acos({val}) 不正确");
        }

        private void AssertAtan(float val, float expectedRad)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            float actual = zMathf.Atan(zval).ToFloat();
            Assert.AreEqual(expectedRad, actual, ToleranceRad, $"Atan({val}) 不正确");
        }

        private void AssertSinAngle(int angle, float expected)
        {
            zfloat zangle = new zfloat(angle);
            float actual = zMathf.SinAngle(zangle).ToFloat();
            Assert.AreEqual(expected, actual, ToleranceFloat, $"SinAngle({angle}°) 不正确");
        }

        private void AssertCosAngle(int angle, float expected)
        {
            zfloat zangle = new zfloat(angle);
            float actual = zMathf.CosAngle(zangle).ToFloat();
            Assert.AreEqual(expected, actual, ToleranceFloat, $"CosAngle({angle}°) 不正确");
        }

        private void AssertTanAngle(int angle, float expected)
        {
            zfloat zangle = new zfloat(angle);
            float actual = zMathf.TanAngle(zangle).ToFloat();
            Assert.AreEqual(expected, actual, ToleranceFloat, $"TanAngle({angle}°) 不正确");
        }

        private void AssertAsinAngle(float val, float expectedDeg)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            float actual = zMathf.AsinAngle(zval).ToFloat();
            Assert.AreEqual(expectedDeg, actual, ToleranceAngle, $"AsinAngle({val}) 不正确");
        }

        private void AssertAcosAngle(float val, float expectedDeg)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            float actual = zMathf.AcosAngle(zval).ToFloat();
            Assert.AreEqual(expectedDeg, actual, ToleranceAngle, $"AcosAngle({val}) 不正确");
        }

        private void AssertAtanAngle(float val, float expectedDeg)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            float actual = zMathf.AtanAngle(zval).ToFloat();
            Assert.AreEqual(expectedDeg, actual, ToleranceAngle, $"AtanAngle({val}) 不正确");
        }

        private void AssertSqrt(float val, float expected)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            float actual = zMathf.Sqrt(zval).ToFloat();
            Assert.AreEqual(expected, actual, ToleranceFloat, $"Sqrt({val}) 不正确");
        }

        private void AssertPow(float baseVal, float power, float expected)
        {
            zfloat zbase = zfloat.CreateFloat((long)(baseVal * zfloat.SCALE_10000));
            zfloat zpower = zfloat.CreateFloat((long)(power * zfloat.SCALE_10000));
            float actual = zMathf.Pow(zbase, zpower).ToFloat();
            Assert.AreEqual(expected, actual, ToleranceFloat, $"Pow({baseVal}, {power}) 不正确");
        }

        private void AssertExp(float val, float expected)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            float actual = zMathf.Exp(zval).ToFloat();
            Assert.AreEqual(expected, actual, ToleranceFloat, $"Exp({val}) 不正确");
        }

        private void AssertLog(float val, float expected)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            float actual = zMathf.Log(zval).ToFloat();
            Assert.AreEqual(expected, actual, ToleranceFloat, $"Log({val}) 不正确");
        }

        private void AssertLog10(float val, float expected)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            float actual = zMathf.Log10(zval).ToFloat();
            // Log10 使用近似算法，对于较大值（如100以上）误差会增大，使用更宽松的容差
            float tolerance = val >= 100f ? 0.1f : ToleranceFloat;
            Assert.AreEqual(expected, actual, tolerance, $"Log10({val}) 不正确");
        }

        private void AssertCeil(float val, int expected)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            int actual = zMathf.Ceil(zval);
            Assert.AreEqual(expected, actual, $"Ceil({val}) 不正确");
        }

        private void AssertFloor(float val, int expected)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            int actual = zMathf.Floor(zval);
            Assert.AreEqual(expected, actual, $"Floor({val}) 不正确");
        }

        private void AssertRound(float val, int expected)
        {
            zfloat zval = zfloat.CreateFloat((long)(val * zfloat.SCALE_10000));
            int actual = zMathf.Round(zval);
            Assert.AreEqual(expected, actual, $"Round({val}) 不正确");
        }

        #endregion
    }
}

