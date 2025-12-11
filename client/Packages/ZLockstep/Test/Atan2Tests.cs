using NUnit.Framework;
using UnityEngine;
using ZLockstep.View;
using zUnity;

namespace ZMath.Tests
{
    public class Atan2Tests
    {
        // 允许的最大误差（度）
        // 该多项式拟合算法在定点数下的理论最大误差约 0.3 度左右
        private const float ToleranceDegrees = 0.5f;

        [Test]
        public void Atan2_Zero_Zero_ReturnsZero()
        {
            // 特殊情况：(0,0) 应该返回 0
            var val = zMathf.Atan2(new zfloat(0), new zfloat(0));
            Assert.AreEqual(0, val.value);
        }

        [Test]
        public void Atan2_AxisAligned_ReturnsExactAngles()
        {
            // 测试四个轴向：0, 90, 180, -90
            AssertAngle(0, 100, 0f);      // 右
            AssertAngle(100, 0, 90f);     // 上
            AssertAngle(0, -100, 180f);   // 左
            AssertAngle(-100, 0, -90f);   // 下
        }

        [Test]
        public void Atan2_Diagonals_ReturnsExactAngles()
        {
            // 测试对角线：45, 135, -135, -45
            AssertAngle(100, 100, 45f);
            AssertAngle(100, -100, 135f);
            AssertAngle(-100, -100, -135f);
            AssertAngle(-100, 100, -45f);
        }

        [Test]
        public void Atan2_FullCircle_SweepTest()
        {
            // 360 度全圆扫描测试
            // 对比 Mathf.Atan2 (标准浮点) 和 MockMathf.Atan2 (定点)

            float maxError = 0f;
            int step = 5; // 每隔5度测一次

            for (int i = -180; i <= 180; i += step)
            {
                if (i == 0) continue; // 0度已单独测试

                // 1. 构造标准向量
                float rad = i * Mathf.Deg2Rad;
                float vecX = Mathf.Cos(rad) * 1000f;
                float vecY = Mathf.Sin(rad) * 1000f;

                // 2. 转为定点数计算
                zfloat zx = zfloat.CreateFloat((long)vecX * zfloat.SCALE_10000);
                zfloat zy = zfloat.CreateFloat((long)vecY * zfloat.SCALE_10000);

                // 3. 获取定点数结果并转回 float 角度
                zfloat result = zMathf.Atan2(zy, zx);
                // Atan2 返回的是弧度，需要转换为度数
                float resultDeg = result.ToFloat() * Mathf.Rad2Deg;

                // 4. 计算误差
                float diff = Mathf.Abs(i - resultDeg);
                if (diff > 180f) diff = 360f - diff; // 处理 -180 和 180 的接缝

                // 记录最大误差
                if (diff > maxError) maxError = diff;

                // 断言
                Assert.Less(diff, ToleranceDegrees,
                    $"角度 {i}° 失败。期望: {i}, 实际: {resultDeg:F2}, 误差: {diff:F4}");
            }

            // 输出统计信息到 Test Runner 的控制台
            Debug.Log($"<color=green>全圆扫描测试通过！最大误差: {maxError:F4} 度</color>");
        }

        [Test]
        public void Atan2_SpecialAngles()
        {
            // 测试特殊角度：30°, 60°, 120°, 150°, -30°, -60°, -120°, -150°
            AssertAngle(500, 866, 30f);      // sin(30°)=0.5, cos(30°)=0.866
            AssertAngle(866, 500, 60f);      // sin(60°)=0.866, cos(60°)=0.5
            AssertAngle(866, -500, 120f);    // 第二象限
            AssertAngle(500, -866, 150f);    // 第二象限
            AssertAngle(-500, 866, -30f);    // 第四象限
            AssertAngle(-866, 500, -60f);    // 第四象限
            AssertAngle(-866, -500, -120f);  // 第三象限
            AssertAngle(-500, -866, -150f);  // 第三象限
        }

        [Test]
        public void Atan2_QuadrantBoundaries()
        {
            // 测试象限边界
            // 对于接近90度的角度，由于 tan(接近90度) 值非常大，定点数精度限制会导致较大误差
            // 使用更宽松的容差（0.5度）
            float boundaryTolerance = 0.5f;
            
            // 第一象限边界：接近 0° 和 90°
            AssertAngle(1, 1000, 0.057f, 0.1f);      // 接近 0°
            AssertAngle(1000, 1, 89.943f, boundaryTolerance);     // 接近 90°
            
            // 第二象限边界：接近 90° 和 180°
            AssertAngle(1000, -1, 90.057f, boundaryTolerance);    // 接近 90°
            AssertAngle(1, -1000, 179.943f, 0.1f);   // 接近 180°
            
            // 第三象限边界：接近 -180° 和 -90°
            AssertAngle(-1, -1000, -179.943f, 0.1f); // 接近 -180°
            AssertAngle(-1000, -1, -90.057f, boundaryTolerance);  // 接近 -90°
            
            // 第四象限边界：接近 -90° 和 0°
            AssertAngle(-1000, 1, -89.943f, boundaryTolerance);   // 接近 -90°
            AssertAngle(-1, 1000, -0.057f, 0.1f);   // 接近 0°
        }

        [Test]
        public void Atan2_ZeroCases()
        {
            // 测试各种零值情况
            AssertAngle(0, 0, 0f);           // (0,0)
            AssertAngle(0, 100, 0f);         // y=0, x>0
            AssertAngle(0, -100, 180f);      // y=0, x<0
            AssertAngle(100, 0, 90f);       // y>0, x=0
            AssertAngle(-100, 0, -90f);     // y<0, x=0
        }

        [Test]
        public void Atan2_VerySmallValues()
        {
            // 测试非常小的值
            // 对于接近90度的角度，使用更宽松的容差（0.5度）
            float boundaryTolerance = 0.5f;
            
            AssertAngle(1, 10000, 0.0057f, 0.1f);    // 非常小的角度
            AssertAngle(10000, 1, 89.9943f, boundaryTolerance);   // 接近 90°
            AssertAngle(-1, 10000, -0.0057f, 0.1f);  // 非常小的负角度
            AssertAngle(-10000, 1, -89.9943f, boundaryTolerance); // 接近 -90°
        }

        [Test]
        public void Atan2_VeryLargeValues()
        {
            // 测试非常大的值（应该归一化到相同角度）
            AssertAngle(100000, 100000, 45f);        // 大值对角线
            AssertAngle(1000000, 1000000, 45f);      // 更大值对角线
            AssertAngle(-100000, -100000, -135f);   // 大值第三象限
            AssertAngle(100000, -100000, 135f);     // 大值第二象限
        }

        [Test]
        public void Atan2_DifferentMagnitudes()
        {
            // 测试不同大小的向量（相同角度应该得到相同结果）
            float[] magnitudes = { 10f, 100f, 1000f, 10000f };
            float[] angles = { 30f, 45f, 60f, 120f, -30f, -45f };
            
            foreach (float angle in angles)
            {
                foreach (float mag in magnitudes)
                {
                    float rad = angle * Mathf.Deg2Rad;
                    float x = Mathf.Cos(rad) * mag;
                    float y = Mathf.Sin(rad) * mag;
                    
                    zfloat zx = zfloat.CreateFloat((long)x * zfloat.SCALE_10000);
                    zfloat zy = zfloat.CreateFloat((long)y * zfloat.SCALE_10000);
                    
                    zfloat result = zMathf.Atan2(zy, zx);
                    float resultDeg = result.ToFloat() * Mathf.Rad2Deg;
                    
                    float diff = Mathf.Abs(angle - resultDeg);
                    if (diff > 180f) diff = 360f - diff;
                    
                    // 对于小值向量，使用更宽松的容差（定点数精度限制）
                    // 对于接近90度的角度，也需要更宽松的容差
                    float tolerance = ToleranceDegrees;
                    if (mag < 50f)
                    {
                        tolerance = 3.0f; // 非常小的向量（<50）容差放宽到3度
                    }
                    else if (mag < 100f)
                    {
                        tolerance = 2.0f; // 小值向量（50-100）容差放宽到2度
                    }
                    else if (Mathf.Abs(Mathf.Abs(angle) - 90f) < 10f)
                    {
                        tolerance = 1.0f; // 接近90度的角度容差放宽到1度
                    }
                    
                    Assert.Less(diff, tolerance,
                        $"角度 {angle}°, 大小 {mag} 失败。期望: {angle}, 实际: {resultDeg:F2}, 误差: {diff:F4}");
                }
            }
        }

        [Test]
        public void Atan2_FirstQuadrant()
        {
            // 第一象限详细测试：0° < θ < 90°
            for (int deg = 1; deg < 90; deg += 10)
            {
                float rad = deg * Mathf.Deg2Rad;
                float x = Mathf.Cos(rad) * 1000f;
                float y = Mathf.Sin(rad) * 1000f;
                
                zfloat zx = zfloat.CreateFloat((long)x * zfloat.SCALE_10000);
                zfloat zy = zfloat.CreateFloat((long)y * zfloat.SCALE_10000);
                
                zfloat result = zMathf.Atan2(zy, zx);
                float resultDeg = result.ToFloat() * Mathf.Rad2Deg;
                
                float diff = Mathf.Abs(deg - resultDeg);
                Assert.Less(diff, ToleranceDegrees,
                    $"第一象限 {deg}° 失败。期望: {deg}, 实际: {resultDeg:F2}, 误差: {diff:F4}");
            }
        }

        [Test]
        public void Atan2_SecondQuadrant()
        {
            // 第二象限详细测试：90° < θ < 180°
            for (int deg = 91; deg < 180; deg += 10)
            {
                float rad = deg * Mathf.Deg2Rad;
                float x = Mathf.Cos(rad) * 1000f;
                float y = Mathf.Sin(rad) * 1000f;
                
                zfloat zx = zfloat.CreateFloat((long)x * zfloat.SCALE_10000);
                zfloat zy = zfloat.CreateFloat((long)y * zfloat.SCALE_10000);
                
                zfloat result = zMathf.Atan2(zy, zx);
                float resultDeg = result.ToFloat() * Mathf.Rad2Deg;
                
                float diff = Mathf.Abs(deg - resultDeg);
                Assert.Less(diff, ToleranceDegrees,
                    $"第二象限 {deg}° 失败。期望: {deg}, 实际: {resultDeg:F2}, 误差: {diff:F4}");
            }
        }

        [Test]
        public void Atan2_ThirdQuadrant()
        {
            // 第三象限详细测试：-180° < θ < -90°
            for (int deg = -179; deg < -90; deg += 10)
            {
                float rad = deg * Mathf.Deg2Rad;
                float x = Mathf.Cos(rad) * 1000f;
                float y = Mathf.Sin(rad) * 1000f;
                
                zfloat zx = zfloat.CreateFloat((long)x * zfloat.SCALE_10000);
                zfloat zy = zfloat.CreateFloat((long)y * zfloat.SCALE_10000);
                
                zfloat result = zMathf.Atan2(zy, zx);
                float resultDeg = result.ToFloat() * Mathf.Rad2Deg;
                
                float diff = Mathf.Abs(deg - resultDeg);
                if (diff > 180f) diff = 360f - diff;
                
                Assert.Less(diff, ToleranceDegrees,
                    $"第三象限 {deg}° 失败。期望: {deg}, 实际: {resultDeg:F2}, 误差: {diff:F4}");
            }
        }

        [Test]
        public void Atan2_FourthQuadrant()
        {
            // 第四象限详细测试：-90° < θ < 0°
            for (int deg = -89; deg < 0; deg += 10)
            {
                float rad = deg * Mathf.Deg2Rad;
                float x = Mathf.Cos(rad) * 1000f;
                float y = Mathf.Sin(rad) * 1000f;
                
                zfloat zx = zfloat.CreateFloat((long)x * zfloat.SCALE_10000);
                zfloat zy = zfloat.CreateFloat((long)y * zfloat.SCALE_10000);
                
                zfloat result = zMathf.Atan2(zy, zx);
                float resultDeg = result.ToFloat() * Mathf.Rad2Deg;
                
                float diff = Mathf.Abs(deg - resultDeg);
                Assert.Less(diff, ToleranceDegrees,
                    $"第四象限 {deg}° 失败。期望: {deg}, 实际: {resultDeg:F2}, 误差: {diff:F4}");
            }
        }

        [Test]
        public void Atan2_EdgeCases()
        {
            // 测试边界情况
            // 对于接近90度的角度，使用更宽松的容差（0.5度）
            float boundaryTolerance = 0.5f;
            
            // 非常接近轴的值
            AssertAngle(1, 100000, 0.0006f, 0.1f);      // 几乎在 x 轴上
            AssertAngle(100000, 1, 89.9994f, boundaryTolerance);    // 几乎在 y 轴上
            AssertAngle(-1, 100000, -0.0006f, 0.1f);    // 几乎在 x 轴上（负）
            AssertAngle(-100000, 1, -89.9994f, boundaryTolerance);  // 几乎在 y 轴上（负）
        }

        [Test]
        public void Atan2_ConsistencyTest()
        {
            // 一致性测试：相同角度不同表示应该得到相同结果
            // 例如：(1,1) 和 (100,100) 应该得到相同的角度
            var testCases = new[]
            {
                (1L, 1L, 45f),
                (100L, 100L, 45f),
                (1000L, 1000L, 45f),
                (-1L, 1L, -45f),
                (-100L, 100L, -45f),
                (-1000L, 1000L, -45f),
            };
            
            foreach (var (y, x, expectedDeg) in testCases)
            {
                zfloat zy = new zfloat(y * 100);
                zfloat zx = new zfloat(x * 100);
                
                zfloat result = zMathf.Atan2(zy, zx);
                float resultDeg = result.ToFloat() * Mathf.Rad2Deg;
                
                float diff = Mathf.Abs(expectedDeg - resultDeg);
                if (diff > 180f) diff = 360f - diff;
                
                Assert.Less(diff, ToleranceDegrees,
                    $"一致性测试 ({x},{y}) 失败。期望: {expectedDeg}, 实际: {resultDeg:F2}, 误差: {diff:F4}");
            }
        }

        [Test]
        public void Atan2_HighPrecisionTest()
        {
            // 高精度测试：使用更小的步长
            float maxError = 0f;
            int step = 1; // 每隔1度测一次
            
            for (int i = -180; i <= 180; i += step)
            {
                if (i == 0) continue;
                
                float rad = i * Mathf.Deg2Rad;
                float vecX = Mathf.Cos(rad) * 1000f;
                float vecY = Mathf.Sin(rad) * 1000f;
                
                zfloat zx = zfloat.CreateFloat((long)vecX * zfloat.SCALE_10000);
                zfloat zy = zfloat.CreateFloat((long)vecY * zfloat.SCALE_10000);
                
                zfloat result = zMathf.Atan2(zy, zx);
                float resultDeg = result.ToFloat() * Mathf.Rad2Deg;
                
                float diff = Mathf.Abs(i - resultDeg);
                if (diff > 180f) diff = 360f - diff;
                
                if (diff > maxError) maxError = diff;
                
                // 对于高精度测试，使用稍宽松的容差
                Assert.Less(diff, ToleranceDegrees * 1.5f,
                    $"高精度测试 {i}° 失败。期望: {i}, 实际: {resultDeg:F2}, 误差: {diff:F4}");
            }
            
            Debug.Log($"<color=green>高精度测试通过！最大误差: {maxError:F4} 度</color>");
        }

        // --- 辅助断言函数 ---
        private void AssertAngle(long y, long x, float expectedDeg)
        {
            AssertAngle(y, x, expectedDeg, ToleranceDegrees);
        }

        private void AssertAngle(long y, long x, float expectedDeg, float tolerance)
        {
            // 为了避免精度丢失，输入乘以100
            zfloat zy = new zfloat(y * 100);
            zfloat zx = new zfloat(x * 100);

            zfloat result = zMathf.Atan2(zy, zx);
            // Atan2 返回的是弧度，需要转换为度数
            float resultDeg = result.ToFloat() * Mathf.Rad2Deg;

            float diff = Mathf.Abs(expectedDeg - resultDeg);
            if (diff > 180) diff = 360 - diff;

            Assert.Less(diff, tolerance,
                $"输入({x},{y}) 失败。期望: {expectedDeg}, 实际: {resultDeg:F2}, 误差: {diff:F4}");
        }
    }
}