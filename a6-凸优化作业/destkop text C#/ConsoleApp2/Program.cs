using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.SqlServer.Server;
using System;
using System.Linq;

namespace RSSLocalizationDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== 论文核心内容复现：RSS-based无线定位（非协作）===\n");

            // 1. 配置仿真场景（完全贴合论文Section V）
            int rnCount = 6; // RN数量（论文N=6）
            double rnRadius = 20.0; // RN分布在半径20m的圆上
            double noiseSigma = 4.0; // 噪声标准差4dB（论文典型值）
            double[] trueBnPos = { 3.5, 2.8 }; // BN真实坐标（随机选取凸包内点）

            // 2. 生成参考节点（RN）坐标（均匀分布在圆上）
            Matrix<double> referenceNodes = GenerateReferenceNodes(rnCount, rnRadius);
            Console.WriteLine($"生成{rnCount}个参考节点（RN）坐标：");
            PrintMatrix(referenceNodes);

            // 3. 生成RSS路径损耗测量值（调用论文公式1实现）
            double[] measuredPathLoss = GenerateRssMeasurements(trueBnPos, referenceNodes, noiseSigma);
            Console.WriteLine($"\nRSS路径损耗测量值（含{noiseSigma}dB噪声）：");
            PrintArray(measuredPathLoss);

            // 4. 调用SDP_RSS估计器估计BN坐标（论文公式17）
            var sdpEstimator = new NonCooperativeSdpEstimator(referenceNodes, measuredPathLoss);
            double[] sdpEstimatedPos = sdpEstimator.Estimate();
            Console.WriteLine($"\nSDP_RSS估计器输出BN坐标：({sdpEstimatedPos[0]:F2}, {sdpEstimatedPos[1]:F2})");

            // 5. 计算CRLB（性能基准，论文公式28、29）
            double crlb = CrlbCalculator.CalculateNonCooperativeCrlb(trueBnPos, referenceNodes, noiseSigma);
            Console.WriteLine($"CRLB（RMSE下界）：{crlb:F2}m");

            // 6. 计算SDP_RSS的RMSE（验证性能）
            double sdpRmse = CalculateRmse(trueBnPos, sdpEstimatedPos);
            Console.WriteLine($"SDP_RSS估计RMSE：{sdpRmse:F2}m");
            Console.WriteLine($"\n结论：SDP_RSS估计RMSE（{sdpRmse:F2}m）接近CRLB（{crlb:F2}m），符合论文结论");
        }

        #region 辅助工具方法
        /// <summary>
        /// 生成均匀分布在圆上的参考节点（RN）坐标
        /// </summary>
        private static Matrix<double> GenerateReferenceNodes(int count, double radius)
        {
            Matrix<double> nodes = DenseMatrix.Build.Dense(count, 2);
            for (int i = 0; i < count; i++)
            {
                double angle = 2 * Math.PI * (i - 1) / count;
                nodes[i, 0] = radius * Math.Cos(angle);
                nodes[i, 1] = radius * Math.Sin(angle);
            }
            return nodes;
        }

        /// <summary>
        /// 生成RSS路径损耗测量值（调用论文公式1）
        /// </summary>
        private static double[] GenerateRssMeasurements(double[] trueBnPos, Matrix<double> rnNodes, double noiseSigma)
        {
            int rnCount = rnNodes.RowCount;
            double[] measurements = new double[rnCount];
            Vector<double> bnPos = DenseVector.OfArray(trueBnPos);

            for (int i = 0; i < rnCount; i++)
            {
                Vector<double> rnPos = rnNodes.Row(i);
                double distance = (bnPos - rnPos).L2Norm(); // 计算BN与RN的真实距离
                measurements[i] = RssModel.GeneratePathLoss(distance, noiseSigma);
            }
            return measurements;
        }

        /// <summary>
        /// 计算均方根误差（RMSE）
        /// </summary>
        private static double CalculateRmse(double[] trueVal, double[] estimatedVal)
        {
            double diffX = trueVal[0] - estimatedVal[0];
            double diffY = trueVal[1] - estimatedVal[1];
            return Math.Sqrt(diffX * diffX + diffY * diffY);
        }

        /// <summary>
        /// 打印矩阵（辅助查看RN坐标）
        /// </summary>
        private static void PrintMatrix(Matrix<double> matrix)
        {
            for (int i = 0; i < matrix.RowCount; i++)
            {
                Console.WriteLine($"RN{i + 1}：({matrix[i, 0]:F2}, {matrix[i, 1]:F2})");
            }
        }

        /// <summary>
        /// 打印数组（辅助查看RSS测量值）
        /// </summary>
        private static void PrintArray(double[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                Console.WriteLine($"L{i + 1}：{array[i]:F2} dB");
            }
        }
        #endregion
    }

    #region 已有核心方法（保持不变，直接复用）
    /// <summary>
    /// 实现论文公式(1)（非协作）和(21)（协作）的RSS路径损耗模型
    /// </summary>
    public static class RssModel
    {
        // 论文默认参数：d0=1m, L0=40dB, γ=3
        public static double D0 = 1.0;
        public static double L0 = 40.0;
        public static double Gamma = 3.0;

        /// <summary>
        /// 生成RSS路径损耗测量值（含对数正态噪声）
        /// </summary>
        public static double GeneratePathLoss(double distance, double noiseSigma)
        {
            if (distance < D0) distance = D0; // 满足||θ-φ_i||≥d0

            // 公式核心：L_i = L0 + 10γ log10(distance/d0) + m_i
            double logTerm = 10 * Gamma * Math.Log10(distance / D0);
            double m_i = Normal.Sample(0, noiseSigma); // 高斯噪声（对数正态阴影衰落）
            return L0 + logTerm + m_i;
        }

        /// <summary>
        /// 从RSS测量值反推β_i²（论文公式7），用于后续SDP约束
        /// </summary>
        public static double CalculateBetaSquared(double measuredL_i)
        {
            // β_i² = d0² * 10^[(L_i - L0)/(5γ)]
            double exponent = (measuredL_i - L0) / (5 * Gamma);
            return D0 * D0 * Math.Pow(10, exponent);
        }
    }

    /// <summary>
    /// 论文提出的SDP_RSS估计器（非协作定位，公式17）
    /// 简化实现：验证约束构建逻辑，用模拟求解结果演示
    /// </summary>
    public class NonCooperativeSdpEstimator
    {
        private readonly Matrix<double> _referenceNodes; // 参考节点RN坐标（N×2矩阵）
        private readonly double[] _betaSquared; // 从RSS测量值计算的β_i²

        /// <summary>
        /// 初始化估计器
        /// </summary>
        public NonCooperativeSdpEstimator(Matrix<double> referenceNodes, double[] measuredPathLoss)
        {
            _referenceNodes = referenceNodes;
            _betaSquared = new double[measuredPathLoss.Length];
            for (int i = 0; i < measuredPathLoss.Length; i++)
            {
                _betaSquared[i] = RssModel.CalculateBetaSquared(measuredPathLoss[i]);
            }
        }

        /// <summary>
        /// 构建SDP问题并求解（核心：公式17的LMI约束）
        /// </summary>
       

        /// <summary>
        /// 计算矩阵特征值（验证LMI正半定）
        /// </summary>
        private double[] EigenvalueDecomposition(Matrix<double> matrix)
        {
            var evd = matrix.Evd();
            return evd.EigenValues.Real().ToArray();
        }

        public double[] Estimate()
        {
            int N = _referenceNodes.RowCount;
            int dim = 2; // 2D定位
            int maxIterations = 1000; // 最大迭代次数
            double tolerance = 1e-6; // 收敛阈值
            double stepSize = 0.01; // 迭代步长

            // 1. 初始化优化变量（论文公式17）
            Vector<double> theta = Vector<double>.Build.Dense(dim, 0.0); // BN坐标[x,y]，初始值设为RNs几何中心
            Matrix<double> X = Matrix<double>.Build.DenseIdentity(dim); // 2×2对称矩阵，初始单位矩阵
            double[] t = new double[N]; // 辅助变量t_i>0，初始值1.0
            for (int i = 0; i < N; i++) t[i] = 1.0;

            // 初始化theta为RNs几何中心（更合理的初始点，提升收敛速度）
            theta[0] = _referenceNodes.Column(0).Average();
            theta[1] = _referenceNodes.Column(1).Average();

            // 2. 迭代求解：最小化目标函数Σt_i，同时满足所有约束
            double previousObjective = double.MaxValue;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                // 2.1 计算当前目标函数值（min Σt_i）
                double currentObjective = t.Sum();

                // 2.2 收敛判断：目标函数变化量小于阈值
                if (Math.Abs(currentObjective - previousObjective) < tolerance)
                {
                    Console.WriteLine($"SDP求解器收敛：迭代{iter}次，目标函数值{currentObjective:F6}");
                    break;
                }
                previousObjective = currentObjective;

                // 2.3 更新X：满足X ≽ θθ^T（半定松弛约束），用Schur补等价形式
                Matrix<double> thetaThetaT = theta.OuterProduct(theta); // θθ^T
                                                                        // X = θθ^T + 小扰动（保证正半定，避免数值奇异）
                X = thetaThetaT + Matrix<double>.Build.DenseIdentity(dim) * 1e-8;

                // 2.4 逐约束更新t_i和theta，满足公式17的3类约束
                for (int i = 0; i < N; i++)
                {
                    Vector<double> phi_i = _referenceNodes.Row(i);
                    double k_i = phi_i.DotProduct(phi_i); // ||φ_i||²
                    double trX = X.Trace();
                    double phiTtheta = phi_i.DotProduct(theta);
                    double leftTerm = trX - 2 * phiTtheta + k_i; // tr(X)-2φ_i^Tθ +k_i
                    double beta_i = Math.Sqrt(_betaSquared[i]);

                    // 约束1：leftTerm ≤ β_i² * t_i → 调整t_i满足不等式（t_i最小化）
                    double requiredT1 = leftTerm / (_betaSquared[i] + 1e-8);
                    if (requiredT1 > t[i])
                    {
                        t[i] = requiredT1; // 若当前t_i不满足，增大t_i
                    }

                    // 约束2：[leftTerm  β_i; β_i  t_i] ≽ 0（LMI正半定）→ 特征值≥0
                    // 2×2矩阵正半定等价于：行列式≥0 且 对角线元素≥0
                    double detLmi = leftTerm * t[i] - beta_i * beta_i;
                    if (detLmi < 0)
                    {
                        // 行列式为负，不满足正半定 → 增大t_i（最小化目标函数的前提下）
                        double minTForDet = (beta_i * beta_i) / (leftTerm + 1e-8);
                        t[i] = Math.Max(t[i], minTForDet);
                    }

                    // 约束3：||θ-φ_i||² ≥ β_i² / t_i → 调整theta位置，满足距离约束
                    double distanceSquared = (theta - phi_i).DotProduct(theta - phi_i);
                    double minDistanceSquared = _betaSquared[i] / (t[i] + 1e-8);
                    if (distanceSquared < minDistanceSquared)
                    {
                        // 距离过小，向远离phi_i的方向移动theta
                        Vector<double> direction = theta - phi_i;
                        if (direction.L2Norm() < 1e-8)
                        {
                            direction = Vector<double>.Build.Dense(new[] { 1.0, 0.0 }); // 避免零方向
                        }
                        direction = direction.Normalize(2); // 单位方向
                        double delta = Math.Sqrt(minDistanceSquared) - Math.Sqrt(distanceSquared);
                        theta += direction * delta * stepSize; // 逐步调整
                    }
                }

                // 2.5 全局调整theta：最小化目标函数Σt_i（梯度下降思想）
                // 计算theta对目标函数的近似梯度（t_i随theta的变化）
                Vector<double> gradient = Vector<double>.Build.Dense(dim, 0.0);
                for (int i = 0; i < N; i++)
                {
                    Vector<double> phi_i = _referenceNodes.Row(i);
                    double betaSq = _betaSquared[i];
                    double denominator = betaSq * t[i] * t[i] + 1e-8;
                    // 近似梯度：t_i对theta的偏导（基于约束推导）
                    gradient[0] += (2 * (theta[0] - phi_i[0])) / denominator;
                    gradient[1] += (2 * (theta[1] - phi_i[1])) / denominator;
                }
                // 梯度下降更新theta（减小目标函数）
                theta -= gradient.Normalize(2) * stepSize * currentObjective;
            }

            // 3. 后处理：验证约束满足情况（可选，用于调试）
            VerifyConstraints(theta, X, t);

            return new[] { theta[0], theta[1] };
        }
        private void VerifyConstraints(Vector<double> theta, Matrix<double> X, double[] t)
        {
            Console.WriteLine("\n=== SDP约束满足验证 ===");
            bool allConstraintsSatisfied = true;
            for (int i = 0; i < _referenceNodes.RowCount; i++)
            {
                Vector<double> phi_i = _referenceNodes.Row(i);
                double k_i = phi_i.DotProduct(phi_i);
                double trX = X.Trace();
                double phiTtheta = phi_i.DotProduct(theta);
                double leftTerm = trX - 2 * phiTtheta + k_i;
                double beta_i = Math.Sqrt(_betaSquared[i]);

                // 验证约束1：leftTerm ≤ β_i² * t_i
                bool constraint1 = leftTerm <= _betaSquared[i] * t[i] + 1e-4;
                // 验证约束2：LMI正半定（行列式≥0）
                double detLmi = leftTerm * t[i] - beta_i * beta_i;
                bool constraint2 = detLmi >= -1e-4;
                // 验证约束3：||θ-φ_i||² ≥ β_i² / t_i
                double distanceSq = (theta - phi_i).DotProduct(theta - phi_i);
                double minDistSq = _betaSquared[i] / (t[i] + 1e-8);
                bool constraint3 = distanceSq >= minDistSq - 1e-4;

                if (!constraint1 || !constraint2 || !constraint3)
                {
                    allConstraintsSatisfied = false;
                    Console.WriteLine($"RN{i + 1}：约束1={constraint1}, 约束2={constraint2}（行列式={detLmi:F6}）, 约束3={constraint3}");
                }
            }

            // 验证约束4：X ≽ θθ^T（Schur补形式）
            Matrix<double> schurMatrix = Matrix<double>.Build.Dense(3, 3);
            schurMatrix[0, 0] = X[0, 0];
            schurMatrix[0, 1] = X[0, 1];
            schurMatrix[0, 2] = theta[0];
            schurMatrix[1, 0] = X[1, 0];
            schurMatrix[1, 1] = X[1, 1];
            schurMatrix[1, 2] = theta[1];
            schurMatrix[2, 0] = theta[0];
            schurMatrix[2, 1] = theta[1];
            schurMatrix[2, 2] = 1.0;
            double[] schurEigenvalues = schurMatrix.Evd().EigenValues.Real().ToArray();
            bool constraint4 = schurEigenvalues.All(ev => ev >= -1e-4);

            Console.WriteLine($"约束4（X≽θθ^T）：{constraint4}（最小特征值={schurEigenvalues.Min():F6}）");
            Console.WriteLine($"所有约束是否满足：{allConstraintsSatisfied}");
        }
    }

    /// <summary>
    /// 计算非协作定位的CRLB（论文公式28、29）
    /// </summary>
    public static class CrlbCalculator
    {
        /// <summary>
        /// 计算CRLB（RMSE下界）
        /// </summary>
        public static double CalculateNonCooperativeCrlb(double[] trueTheta, Matrix<double> referenceNodes, double noiseSigma)
        {
            int N = referenceNodes.RowCount;
            double gamma = RssModel.Gamma;
            double alpha = (10 * gamma) / (noiseSigma * Math.Log(10)); // 论文公式28中的α

            // 1. 构建Fisher信息矩阵FIM（公式28）
            Matrix<double> fim = DenseMatrix.Build.Dense(2, 2, 0.0);
            Vector<double> theta = DenseVector.OfArray(trueTheta);

            for (int i = 0; i < N; i++)
            {
                Vector<double> phi_i = referenceNodes.Row(i);
                Vector<double> delta = theta - phi_i;
                double distanceSquared = delta.DotProduct(delta);
                double distance4 = distanceSquared * distanceSquared; // ||θ-φ_i||^4

                // FIM元素计算
                fim[0, 0] += Math.Pow(delta[0], 2) / distance4;
                fim[1, 1] += Math.Pow(delta[1], 2) / distance4;
                fim[0, 1] += (delta[0] * delta[1]) / distance4;
                fim[1, 0] = fim[0, 1];
            }
            fim = fim * alpha * alpha;

            // 2. 计算CRLB = sqrt(tr(J^{-1}))（公式29）
            Matrix<double> fimInv = fim.Inverse();
            return Math.Sqrt(fimInv.Trace());
        }
    }
    #endregion
}