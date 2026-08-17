using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using System.Threading.Tasks;

namespace Hanging_Solver
{

    public class r
    {
        //输入函数
        public static double f(double t, double v)
        {
            if (t <= 0.1 / v)
            {
                return v * t / 2;
            }
            else if (t > 0.1 / v && t <= 0.2 / v)
            {
                return 0.05;

            }
            else if (t > 0.2 / v && t <= 0.3 / v)
            {
                return 0.05 - (v * t - 0.2) / 2;
            }
            else
            {
                return 0;
            }
        }
        public static string name = "输入函数";
        public static string unit = "米";
        public static string shortname = "y = r(x)";
        public static string info()
        {
            return "该函数表示一个物体以速度v（米每秒）在0.3秒内移动的位移变化情况。函数分为四个阶段：\n" +
                   "1. 在前0.1秒内，位移线性增加。\n" +
                   "2. 在0.1秒到0.2秒之间，位移保持恒定。\n" +
                   "3. 在0.2秒到0.3秒之间，位移线性减少。\n" +
                   "4. 在0.3秒之后，位移保持为零。";
        }
    }
        public class derta
        {

            public static string name = "弹簧阻尼系统的冲激响应函数";
            public static string unit = "米/秒";
            public static string shortname = "y = fderta(c,k,t,m)";
        //经过反拉变的弹簧阻尼系统的冲激响应
        public static double fderta(double c, double k, double t, double m)
        {
            double omigan = Math.Sqrt(k / m);
            double kesi = c / (2D * Math.Sqrt(k * m));
            double omigad = omigan * Math.Sqrt(1 - Math.Pow(kesi, 2));
            if (t < 0)
            {
                return 0;
            }
            if (kesi < 0)
            {
                // 负阻尼（系统可能发散，可根据需求处理，例如抛出警告或返回特定值）
                return omigan * Math.Cosh(Math.Abs(kesi) * omigan * t) * Math.Cos(omigan * t); // 发散振荡
            }
            //else if (0D < kesi && kesi < 1D)
            //{
            //    double sqrtTerm = Math.Sqrt(Math.Max(1 - kesi * kesi, 0));
            //    double midodate1 = ((2 * kesi / sqrtTerm) * Math.Sin(omigad * t) + Math.Cos(omigad * t));
            //    return omigan * Math.Pow(Math.E, -1 * kesi * omigan * t) * midodate1;
            //}
            //else if (Math.Abs(kesi - 1) < 1e-9)
            //{
            //    return (Math.Pow(omigan, 2) * t + 2 * kesi * omigan) * Math.Pow(Math.E, -1 * omigan * t);
            //}
            else if (kesi > 0)
            {
                double midodate1 = Math.Pow(omigan, 2) / (2 * Math.Sqrt(kesi * kesi - 1));
                double midodate3 = -1 * (kesi - Math.Sqrt(kesi * kesi - 1));
                double midodate4 = -1 * (kesi + Math.Sqrt(kesi * kesi - 1));
                double midodate2 = Math.Pow(Math.E, midodate3 * omigan * t);
                double midodate5 = Math.Pow(Math.E, midodate4 * omigan * t);
                double midodatea1 = 2 * kesi * Math.Sqrt(kesi * kesi - 1);
                double midodatea2 = (2 * kesi * kesi - 1);
                return midodate1 * ((midodatea1 - midodatea2) * midodate2 + (midodatea1 + midodatea2) * midodate5);
            }
            else
            {
                return omigan * Math.Cos(omigan * t);
            }
        }

        //public static double fderta(double c, double k, double t, double m)
        //{
        //    // 计算共同的指数部分：e^(-αt/(2m))，这里假设公式中的α为c（根据符号推测，若实际α是其他参数需调整）
        //   double l1 = Math.Cos(t*Math.Sqrt((-1*Math.Pow(c,2)/4)+k*m)/m);
        //    double l2 = c * Math.Pow(Math.E, -1 * c * t / (2 * m));
        //    double r1 = 2 * (-1 * Math.Pow(c, 2) / (2 * Math.Pow(m, 2)) + k / m);
        //    double r2 = Math.Pow(Math.E,-1*c*t / (2 * m));
        //    double r3 = Math.Sin(t * Math.Sqrt((-1*Math.Pow(c,2)+4*k*m)/Math.Pow(m,2))/2);
        //    double r4 = Math.Sqrt((-1 * Math.Pow(c, 2) + 4 * k * m) / Math.Pow(m, 2));
        //    double f = l1*l2/m+r1*r2*r3/r4;
        //    return f;
        //}
    }

    public class handle
    {
        /// <summary>
        /// 基于时间序列ft的卷积（fr、fderta与ft严格对齐，考虑实际时间间隔）
        /// 注意！！此类卷积步长非均匀，结果含物理意义（积分近似）！！
        /// </summary>
        /// <param name="fr">输入序列（与ft对齐）</param>
        /// <param name="fderta">冲激响应序列（与ft对齐）</param>
        /// <param name="ft">时间序列（与fr、fderta长度相同）</param>
        /// <returns>卷积结果（与ft时间点对齐，含物理意义）</returns>
        public static double[] Convoluting(double[] fr, double[] fderta, double[] ft)
        {
            // 1. 验证输入对齐性（确保ft长度为n）
            if (fr.Length != fderta.Length || fr.Length != ft.Length)
                throw new ArgumentException("fr、fderta、ft必须长度相同（严格对齐）");
            int n = fr.Length; // n为ft的固定长度
            if (n == 0)
            {
                return Array.Empty<double>();
            }

            // 2. 计算时间间隔（Δt）
            double[] dt = new double[n];
            dt[0] = 0; // 第一个点间隔设为0
            for (int i = 1; i < n; i++)
            {
                dt[i] = ft[i] - ft[i - 1];
                if (dt[i] <= 0)
                    throw new ArgumentException("时间序列ft必须严格递增");
            }

            // 3. 计算完整卷积结果（长度2n-1）
            int fullLength = 2 * n - 1;
            double[] fullResult = new double[fullLength];
            for (int i = 0; i < fullLength; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    int k = i - j;
                    if (k >= 0 && k < n)
                    {
                        fullResult[i] += fr[j] * fderta[k] * dt[j]; // 保留物理意义
                    }
                }
            }

            // 4. 强制截断为前n个元素（与ft长度一致）
            double[] truncatedResult = new double[n];
            Array.Copy(fullResult, truncatedResult, n); // 只取前n个点

            return truncatedResult;
        }

        /// <summary>
        /// 计算卷积结果第i个点对应的时间（ft[a] + ft[b]，其中a + b = i）
        /// </summary>
        private static double CalculateResultTime(int i, double[] ft, int n)
        {
            double time = 0;
            int count = 0;
            for (int j = 0; j < n; j++)
            {
                int k = i - j;
                if (k >= 0 && k < n)
                {
                    time += ft[j] + ft[k]; // 时间叠加
                    count++;
                }
            }
            return count == 0 ? 0 : time / count; // 平均时间（或取首个有效时间）
        }

        //采样方法
        public static double[] Sample(Func<double, double> f, double tMin, double tMax, double tStep)
        {
            // 验证参数合法性
            if (tStep <= 0)
                throw new ArgumentException("采样步长必须大于0", nameof(tStep));
            if (tMin > tMax)
                throw new ArgumentException("起始位置不能大于结束位置", nameof(tMin));
            if (tMin == tMax)
                return new[] { f(tMin) }; // 单点采样

            // 高精度计算采样点数量：确保覆盖[tMin, tMax]，且步长尽可能接近tStep
            // 1. 计算理论步数（浮点数）
            double totalLength = tMax - tMin;
            double idealSteps = totalLength / tStep;

            // 2. 取整步数（向上取整，确保不遗漏tMax）
            int steps = (int)Math.Ceiling(idealSteps);

            // 3. 修正实际步长：确保最后一个点严格等于tMax（消除累积误差）
            double actualStep = steps == 0 ? 0 : totalLength / steps;

            // 4. 确定采样点数量（步数+1）
            int sampleCount = steps + 1;

            // 初始化结果数组（整数长度，无索引风险）
            double[] results = new double[sampleCount];

            // 执行高精度采样
            for (int i = 0; i < sampleCount; i++)
            {
                // 精确计算当前采样时间：tMin + i * 实际步长
                // 避免使用累积t += step导致的浮点误差
                double t = tMin + i * actualStep;

                // 最终点强制对齐tMax（双重保障）
                if (i == sampleCount - 1)
                    t = tMax;

                results[i] = f(t);
            }
            return results;
        }
        //采样方法只返回t值
        public static double[] Samplet(Func<double, double> f, double tMin, double tMax, double tStep)
        {
            // 验证参数合法性
            if (tStep <= 0)
                throw new ArgumentException("采样步长必须大于0", nameof(tStep));
            if (tMin > tMax)
                throw new ArgumentException("起始位置不能大于结束位置", nameof(tMin));
            if (tMin == tMax)
                return new[] { f(tMin) }; // 单点采样

            // 高精度计算采样点数量：确保覆盖[tMin, tMax]，且步长尽可能接近tStep
            // 1. 计算理论步数（浮点数）
            double totalLength = tMax - tMin;
            double idealSteps = totalLength / tStep;

            // 2. 取整步数（向上取整，确保不遗漏tMax）
            int steps = (int)Math.Ceiling(idealSteps);

            // 3. 修正实际步长：确保最后一个点严格等于tMax（消除累积误差）
            double actualStep = steps == 0 ? 0 : totalLength / steps;

            // 4. 确定采样点数量（步数+1）
            int sampleCount = steps + 1;

            // 初始化结果数组（整数长度，无索引风险）
            double[] results = new double[sampleCount];

            // 执行高精度采样
            for (int i = 0; i < sampleCount; i++)
            {
                // 精确计算当前采样时间：tMin + i * 实际步长
                // 避免使用累积t += step导致的浮点误差
                double t = tMin + i * actualStep;

                // 最终点强制对齐tMax（双重保障）
                if (i == sampleCount - 1)
                    t = tMax;

                results[i] = t;
            }

            return results;
        }




    }
}

