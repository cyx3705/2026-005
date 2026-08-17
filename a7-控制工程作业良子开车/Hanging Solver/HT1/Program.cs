using Hanging_Solver;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SimplePlotExample
{
    public partial class oForm : Form
    {
        // 必须的组件容器
        private System.ComponentModel.IContainer components = null;

        public oForm()
        {
            // 初始化窗体基础组件（必须调用）
            InitializeComponent();
            double tMin = 0d;
            double tMax = 0.1d;
            double tStep = 0.001d;
            // 将 bass.r 包装为只接受一个参数的函数并采样
            double[] caiyan = handle.Sample(x => r.f(x, 5), tMin, tMax, tStep);
            double[] caiyant = handle.Samplet(x => r.f(x, 5), tMin, tMax, tStep);
            // 绘制简单图形
            DrawPlot(caiyan,caiyant,r.name,r.shortname);
        }

        // 绘制简单正弦曲线
        public void DrawPlot(double[] ds, double[] t,string name,string shortname)
        {
            // 1. 生成数据点（正弦曲线）
            List<DataPoint> points = new List<DataPoint>();

            // 正确使用数组索引：i < caiyan.Length
            // 计算每个点的实际 t 坐标：t = tMin + i * tStep
            for (int i = 0; i < ds.Length; i++)
            {
                double t1 = t[i];
                double y = ds[i];
                points.Add(new DataPoint(t1, y));
            }

            // 2. 创建绘图模型
            PlotModel plotModel = new PlotModel { Title = name };

            // 3. 添加曲线
            LineSeries series = new LineSeries { Title = shortname };
            series.Points.AddRange(points);  // 添加数据点
            plotModel.Series.Add(series);

            // 4. 添加坐标轴
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "r轴" });
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "t轴" });

            // 5. 创建绘图控件并显示
            PlotView plotView = new PlotView();
            plotView.Dock = DockStyle.Fill;  // 填满整个窗体
            plotView.Model = plotModel;      // 绑定绘图模型
            this.Controls.Add(plotView);     // 添加到窗体

            // 强制刷新绘图
            plotView.InvalidatePlot(true);
        }

        // 窗体资源释放
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        // 窗体基础初始化（设计器自动生成的核心代码）
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 设置窗体大小和标题
            this.ClientSize = new System.Drawing.Size(800, 600);  // 窗体大小
            this.Text = "简单绘图示例";                            // 窗体标题
            this.ResumeLayout(false);
        }
    }

    // 将 Program 放到命名空间下顶层，确保项目只有一个可见入口点（Main）
    static class Program
    {
        [STAThread]
        public static string[] input()
        {
            Console.Write("请输入命令: ");
            string input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<string>();
            }
            string[] commandParts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return commandParts;
        }

        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("欢迎使用悬挂弹簧阻尼系统配置求解器！<由于不知道怎么通过命令行传入函数，当前传入函数需要在代码中修改！！>");
            Console.WriteLine("当前传入函数为");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new oForm()); // 启动窗体

            while (true)
            {
                try
                {
                    string[] commands = input();
                    if (commands.Length == 0)
                    {
                        continue; // 如果没有输入命令，继续下一次循环
                    }
                    switch (commands[0].ToLower())
                    {
                        case "exit":
                            return; // 退出程序
                        default:
                            Console.WriteLine("未知命令，请重试。");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"发生错误: {ex.Message}");
                }
            }
        }
    }
}