using Hanging_Solver;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Linq;

namespace Hanging_Solver
{


    public partial class Formhtderta : Formht
    {

        public Formhtderta(double c,double k) : base()
        {
            InitPlotContainer("冲激响应图表","t时间","冲激响应");
            // 可以在这里添加额外的初始化代码
            double tMin = 0d;
            double tMax = 0.1d;
            double tStep = 0.001d;
            // 将 bass.r 包装为只接受一个参数的函数并采样
            double[] caiyan = handle.Sample(x => derta.fderta(c,k,x,300), tMin, tMax, tStep);
            double[] caiyant = handle.Samplet(x => derta.fderta(c, k, x, 300), tMin, tMax, tStep);
            // 绘制简单图形
            DrawPlot(caiyan, caiyant, derta.name, derta.shortname);
        }
        public Formhtderta() : base()
        {
            InitPlotContainer("冲激响应图表", "t时间", "冲激响应");
            // 循环参数
            double tMin = 0d;
            double tMax = 0.1d;
            double tStep = 0.001d;
            for (double c = 6000d; c <= 12000; c += 1000)
            {
                for (double k = 20000d; k <= 40000; k += 1000)
                {
                    // 计算数据
                    double[] caiyan = handle.Sample(x => derta.fderta(c, k, x, 400), tMin, tMax, tStep);
                    double[] caiyant = handle.Samplet(x => derta.fderta(c, k, x, 400), tMin, tMax, tStep); // 时间轴数据

                    // 向全局图表添加新曲线
                    AddCurveToPlot(caiyan, caiyant, "c=" + c.ToString(), "k=" + k.ToString());
                }
            }
        }
    }

    public partial class Formhty : Formht
    {
        public Formhty() : base()
        {
            InitPlotContainer("系统响应图表","t时间","系统响应值");
            // 可以在这里添加额外的初始化代码
            double tMin = 0d;
            double tMax = 0.1d;
            double tStep = 0.001d;
            // 将 bass.r 包装为只接受一个参数的函数并采样
            for (double c = 5790d; c <= 5810; c += 0.5)
            {
                for (double k = 27930d; k <= 27980; k += 10)
                {
                    // 计算数据
                    double[] caiyan = handle.Sample(x => derta.fderta(c, k, x, 300), tMin, tMax, tStep);
                    double[] caiyant = handle.Samplet(x => derta.fderta(c, k, x, 300), tMin, tMax, tStep); // 时间轴数据
                    double[] caiyanr = handle.Sample(x => r.f(x, 10), tMin, tMax, tStep);
                    double[] caiyanfinal = handle.Convoluting(caiyanr, caiyan, caiyant);//离散卷积
                    for (int i = 0; i < caiyanfinal.Length; i += 1)
                    {
                        caiyanfinal[i] = caiyanfinal[i] + 300 * 10 / k;
                            }
                    // 向全局图表添加新曲线
                    AddCurveToPlot(caiyanfinal, caiyant, "c="+c.ToString(), "k="+k.ToString());
                }
            }
        }
    }
    public partial class Formhty1 : Formht
    {
        public Formhty1(double c, double k) : base()
        {
            InitPlotContainer("系统响应图表", "t时间", "系统响应值");
            // 可以在这里添加额外的初始化代码
            double tMin = 0d;
            double tMax = 0.1d;
            double tStep = 0.001d;
            // 将 bass.r 包装为只接受一个参数的函数并采样
            
                    // 计算数据
                    double[] caiyan = handle.Sample(x => derta.fderta(c, k, x, 400), tMin, tMax, tStep);
                    double[] caiyant = handle.Samplet(x => derta.fderta(c, k, x, 400), tMin, tMax, tStep); // 时间轴数据
                    double[] caiyanr = handle.Sample(x => r.f(x, 10), tMin, tMax, tStep);
                    double[] caiyanfinal = handle.Convoluting(caiyanr, caiyan, caiyant);
                    // 向全局图表添加新曲线
                    AddCurveToPlot(caiyanfinal, caiyant, derta.name, derta.shortname);

            
        }
    }
    public partial class Formhtr : Formht
    {
        public Formhtr() : base()
        {
            InitPlotContainer("输入函数图表","t时间","输入函数值");
            // 可以在这里添加额外的初始化代码
            double tMin = 0d;
            double tMax = 0.1d;
            double tStep = 0.001d;
            // 将 bass.r 包装为只接受一个参数的函数并采样
            double[] caiyan = handle.Sample(x => r.f(x, 10), tMin, tMax, tStep);
            double[] caiyant = handle.Samplet(x => r.f(x, 10), tMin, tMax, tStep);
            // 绘制简单图形
            DrawPlot(caiyan, caiyant, derta.name, derta.shortname);
        }
    }

    public partial class Formht : Form
    {
        private System.ComponentModel.IContainer components = null;

        // 共享的图表模型和控件（所有曲线共用）
        protected PlotModel _plotModel;
        protected PlotView _plotView;

        public Formht()
        {
            InitializeComponent();
        }

        // 初始化共享图表容器（仅执行一次）
        protected void InitPlotContainer(string plotTitle,string xTitle,string yTitle)
        {
            // 1. 初始化图表模型（全局唯一）
            _plotModel = new PlotModel { Title = plotTitle };

            // 2. 添加坐标轴（全局唯一）
            _plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = xTitle,
                Key = "xAxis"
            });
            _plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = yTitle,
                Key = "yAxis"
            });

            // 3. 初始化绘图控件（全局唯一）
            _plotView = new PlotView
            {
                Dock = DockStyle.Fill,
                Model = _plotModel
            };
            this.Controls.Add(_plotView);
        }

        // 核心方法：向共享图表添加曲线（多曲线叠加）
        protected void AddCurveToPlot(double[] ds, double[] t, string seriesName, string shortName)
        {
            // 校验数据
            if (ds == null || t == null)
                throw new ArgumentNullException("数据数组不能为null");
            if (ds.Length != t.Length)
                throw new ArgumentException("yData与xData长度必须一致");

            // 创建曲线并添加数据
            var series = new LineSeries
            {
                Title = string.IsNullOrEmpty(shortName) ? seriesName : $"{shortName} ({seriesName})",
                XAxisKey = "xAxis",
                YAxisKey = "yAxis",
                StrokeThickness = 1.2
            };
            for (int i = 0; i < ds.Length; i++)
            {
                series.Points.Add(new DataPoint(t[i], ds[i]));
            }

            // 加入共享图表并刷新
            _plotModel.Series.Add(series);
            _plotModel.InvalidatePlot(true);
            _plotView.Refresh();
        }

        // 修改DrawPlot：复用AddCurveToPlot，避免重复创建图表
        public void DrawPlot(double[] ds, double[] t, string name, string shortname)
        {
            // 直接调用添加曲线的方法（使用共享图表）
            AddCurveToPlot(ds, t, name, shortname);
        }

        // 释放资源
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        // 初始化窗体（仅基础属性，图表初始化移至InitPlotContainer）
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 窗体基本属性
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Text = "多曲线绘图工具";
            this.ResumeLayout(false);
        }
    }

    // 将 Program 放到命名空间下顶层，确保项目只有一个可见入口点（Main）
    static class Program
    {
        public static double maxy = 0d;
        public static double miniy = 0d;
        public static double maxyp = 0d;
        public static double miniyp = 0d;
        public static void GetSecondAndThirdIndices(double[] array, out int secondIndex, out int thirdIndex)
        {
            // 边界检查
            if (array == null)
                throw new ArgumentNullException(nameof(array), "数组不能为null");
            if (array.Length < 3)
                throw new ArgumentException("数组长度必须至少为3", nameof(array));

            // 初始化：值为负无穷，索引为-1（无效索引）
            double firstVal = double.NegativeInfinity;
            double secondVal = double.NegativeInfinity;
            double thirdVal = double.NegativeInfinity;
            int firstIdx = -1;
            secondIndex = -1;
            thirdIndex = -1;

            for (int i = 0; i < array.Length; i++)
            {
                double num = array[i];

                if (num > firstVal)
                {
                    // 当前值大于第一大：依次后移
                    thirdVal = secondVal;
                    thirdIndex = secondIndex;

                    secondVal = firstVal;
                    secondIndex = firstIdx;

                    firstVal = num;
                    firstIdx = i; // 更新第一大索引为当前位置（首次出现）
                }
                else if (num > secondVal && num != firstVal)
                {
                    // 当前值小于第一大但大于第二大
                    thirdVal = secondVal;
                    thirdIndex = secondIndex;

                    secondVal = num;
                    secondIndex = i; // 更新第二大索引为当前位置（首次出现）
                }
                else if (num > thirdVal && num != secondVal && num != firstVal)
                {
                    // 当前值小于第二大但大于第三大
                    thirdVal = num;
                    thirdIndex = i; // 更新第三大索引为当前位置（首次出现）
                }
            }

            // 处理极端情况（如存在大量重复值）
            if (thirdIndex == -1) thirdIndex = 0;
            if (secondIndex == -1) secondIndex = 2;
        }
        public static void GetSecondAndThirdMinIndices(double[] array, out int secondMinIndex, out int thirdMinIndex)
        {
            // 边界检查
            if (array == null)
                throw new ArgumentNullException(nameof(array), "数组不能为null");
            if (array.Length < 3)
                throw new ArgumentException("数组长度必须至少为3", nameof(array));

            // 初始化：值为正无穷，索引为-1（无效索引）
            double firstMin = double.PositiveInfinity;
            double secondMin = double.PositiveInfinity;
            double thirdMin = double.PositiveInfinity;
            int firstIdx = -1;
            secondMinIndex = -1;
            thirdMinIndex = -1;

            foreach (double num in array)
            {
                if (num < firstMin)
                {
                    // 当前值小于第一小，依次后移
                    thirdMin = secondMin;
                    thirdMinIndex = secondMinIndex;

                    secondMin = firstMin;
                    secondMinIndex = firstIdx;

                    firstMin = num;
                    firstIdx = Array.IndexOf(array, num); // 获取首次出现的索引
                }
                else if (num > firstMin && num < secondMin)
                {
                    // 当前值大于第一小但小于第二小
                    thirdMin = secondMin;
                    thirdMinIndex = secondMinIndex;

                    secondMin = num;
                    secondMinIndex = Array.IndexOf(array, num); // 获取首次出现的索引
                }
                else if (num > secondMin && num < thirdMin)
                {
                    // 当前值大于第二小但小于第三小
                    thirdMin = num;
                    thirdMinIndex = Array.IndexOf(array, num); // 获取首次出现的索引
                }
            }

            // 处理极端情况（如存在大量重复值）
            if (thirdMinIndex == -1) thirdMinIndex = 0;
            if (secondMinIndex == -1) secondMinIndex = 2;
        }
        static T[] MergeArrays<T>(T[] arr1, T[] arr2)
        {
            // 处理null：将null视为空数组（长度0）
            int length1 = arr1 == null ? 0 : arr1.Length;
            int length2 = arr2 == null ? 0 : arr2.Length;

            // 初始化结果数组（总长度 = 两个数组有效长度之和）
            T[] result = new T[length1 + length2];

            // 复制第一个数组（若不为null）
            if (arr1 != null && length1 > 0)
            {
                Array.Copy(arr1, result, length1);
            }

            // 复制第二个数组到结果数组的尾部（若不为null）
            if (arr2 != null && length2 > 0)
            {
                Array.Copy(arr2, 0, result, length1, length2);
            }

            return result;
        }

        public static T[] TakeFirstN<T>(T[] array, int n)
        {
            // 处理null：将null转换为空序列；Take(n)会自动处理n超出长度的情况
            return array?.Take(n).ToArray() ?? Array.Empty<T>();
        }
        //使用再离散化方法嵌套计算系统响应
        public static double[] caiyanfinal(double c,double k,int i, double tMin,double tMax,double[] caiyanten,double[] caiyanren,double[] caiyanen, out double[] caiyant,out double[] caiyanr,out double[] caiyan)
        // c阻尼系数 k弹簧刚度 i采样点数 tMin最小时间 tMax最大时间 返回系统响应数据
        {
            double tStep = (tMax-tMin)/i;
            double[] mid = handle.Sample(x => derta.fderta(c, k, x, 400), tMin, tMax, tStep);
            caiyan = MergeArrays(caiyanen , mid);
            caiyant = MergeArrays(caiyanten,handle.Samplet(x => derta.fderta(c, k, x, 400), tMin, tMax, tStep)); // 时间轴数据
            caiyanr = MergeArrays(caiyanren,handle.Sample(x => r.f(x, 10), tMin, tMax, tStep));
            double[] caiyanfinal = handle.Convoluting(caiyanr, caiyan, caiyant);
            return caiyanfinal;
        }
        public static void getpmax(int ent,double c,double k,int i, double tMin, double tMax, double[] caiyanten, double[] caiyanren, double[] caiyanen, out double[] caiyantot, out double[] caiyanrot, out double[] caiyanot)
        // c阻尼系数 k弹簧刚度 i采样点数 tMin最小时间 tMax最大时间 ent递归层数 返回系统响应精确最大值
        {
            if (ent < 0)
            {
                Console.WriteLine("递归层数ent不能为负数");
                maxyp= -1d;
                caiyantot = null;
                caiyanrot = null;
                caiyanot = null;
            }

            else if (ent > 0)
            {
                
                double[] caiyanfina1 = caiyanfinal(c, k, i, tMin,tMax, caiyanten, caiyanren, caiyanen, out double[]caiyantot1,out double[]caiyanrot1,out double[]caiyanot1);//先返回系统响应数据
                caiyantot = caiyantot1;
                caiyanrot = caiyanrot1;
                caiyanot= caiyanot1;
                //Console.WriteLine("递归层数："+ent.ToString());//输出当前递归层数
                GetSecondAndThirdMinIndices(caiyanot1, out int secondIndex, out int thirdIndex);//获取第二大和第三大索引
                double tStep = (tMax - tMin) / i;//算步长
                int righIndex = 0;
                int leftIndex= 0;
                Console.WriteLine("第二大索引:" + secondIndex+"第三大索引"+ thirdIndex);
                if (caiyantot1[secondIndex]> caiyantot1[thirdIndex])

                {
                    righIndex =secondIndex;
                    leftIndex =thirdIndex;
                }
                else
                {
                    righIndex = thirdIndex;
                    leftIndex = secondIndex;
                }
                caiyanen = TakeFirstN(caiyanot, leftIndex);
                caiyanten = TakeFirstN(caiyantot, leftIndex);
                caiyanren = TakeFirstN(caiyanrot, leftIndex);
                //取得新的时间范围
                getpmax(ent - 1, c, k, i, caiyantot1[leftIndex], caiyantot1[righIndex], caiyanten, caiyanren, caiyanen, out double[] caiyantot2, out double[] caiyanrot2, out double[] caiyanot2);//递归调用
            }
            else
            {
                caiyantot = caiyanten;
                caiyanrot = caiyanren;
                caiyanot = caiyanen;
                double[] caiyanfina1 = caiyanfinal(c, k, i, tMin, tMax, caiyanten, caiyanren, caiyanen, out double[] caiyantot2, out double[] caiyanrot2, out double[] caiyanot2);//先返回系统响应数据
                maxyp= caiyanfina1.Max();//返回最大值
            }

        }
        //public static void getpmini(int ent, double c, double k, int i, double tMin, double tMax, double[] caiyanten, double[] caiyanren, double[] caiyanen, out double[] caiyantot, out double[] caiyanrot, out double[] caiyanot)
        //// c阻尼系数 k弹簧刚度 i采样点数 tMin最小时间 tMax最大时间 ent递归层数 返回系统响应精确最小值
        //{
        //    if (ent < 0)
        //    {
        //        Console.WriteLine("递归层数ent不能为负数");
        //        maxyp = -1d;
        //        caiyantot = null;
        //        caiyanrot = null;
        //        caiyanot = null;
        //    }

        //    else if (ent > 0)
        //    {
        //        double[] caiyanfina1 = caiyanfinal(c, k, i, tMin, tMax, caiyanten, caiyanren, caiyanen, out double[] caiyantot1, out double[] caiyanrot1, out double[] caiyanot1);//先返回系统响应数据
        //        caiyantot = caiyantot1;
        //        caiyanrot = caiyanrot1;
        //        caiyanot = caiyanot1;
        //        //Console.WriteLine("递归层数："+ent.ToString());//输出当前递归层数
        //        GetSecondAndThirdIndices(caiyanot1, out int secondIndex, out int thirdIndex);//获取第二大和第三大索引
        //        double tStep = (tMax - tMin) / i;//算步长
        //        double[] caiyant = handle.Samplet(x => derta.fderta(c, k, x, 400), tMin, tMax, tStep); // 时间轴数据
        //        int righIndex = 0;
        //        int leftIndex = 0;
        //        //Console.WriteLine("第二小索引:" + secondIndex + "第三小索引" + thirdIndex);
        //        if (caiyantot1[secondIndex] > caiyantot1[thirdIndex])

        //        {
        //            righIndex = secondIndex;
        //            leftIndex = thirdIndex;
        //        }
        //        else
        //        {
        //            righIndex = thirdIndex;
        //            leftIndex = secondIndex;
        //        }

        //        caiyanen = TakeFirstN(caiyanot, leftIndex);
        //        caiyanten = TakeFirstN(caiyantot, leftIndex);
        //        caiyanren = TakeFirstN(caiyanrot, leftIndex);
        //        //取得新的时间范围
        //        getpmini(ent - 1, c, k, i, caiyantot1[leftIndex], caiyantot1[righIndex], caiyanten, caiyanren, caiyanen, out double[] caiyantot2, out double[] caiyanrot2, out double[] caiyanot2);//递归调用
        //    }
        //    else
        //    {
        //        caiyantot = caiyanten;
        //        caiyanrot = caiyanren;
        //        caiyanot = caiyanen;
        //        double[] caiyanfina1 = caiyanfinal(c, k, i, tMin, tMax, caiyanten, caiyanren, caiyanen, out double[] caiyantot2, out double[] caiyanrot2, out double[] caiyanot2);//先返回系统响应数据
        //        miniyp = caiyanfina1.Min();//返回最小值
        //    }

        //}
        public static double getdertap(int ent, double c, double k, int i, double tMin, double tMax)
        // c阻尼系数 k弹簧刚度 i采样点数 tMin最小时间 tMax最大时间 ent递归层数 返回系统响应精确最大最小差值
        {
            getpmax(ent - 1, c, k, i, tMin, tMax, null,null,null, out double[] caiyantot2, out double[] caiyanrot2, out double[] caiyanot2);//递归调用
            double maxyp1 = maxyp + 400*10 / k;
            Console.WriteLine("最大值" + maxyp1);
            return maxyp1;
        }
        public static double bestc = 0;
        public static double bestk = 0;
        public static double minidertap = 100;
        public static void bestc_and_k(int ent, int i, double tMin, double tMax)
        {
            for (double c = 1000d; c <= 10000; c += 1000)
            {
                for (double k = 1000d; k <= 50000; k += 1000)
                {
                    double currentdertap = getdertap(ent, c, k, i, tMin, tMax);
                    Console.WriteLine("当前阻尼系数" + c.ToString() + "__当前弹簧刚度" + k.ToString() + "__此时车轮的震动最高值为" + currentdertap.ToString());
                    if (currentdertap < minidertap)
                    {
                        minidertap = currentdertap;
                        bestc = c;
                        bestk = k;
                    }
                }
            }
        }

        public static string runformname = "Formhtr";
        
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
            Console.WriteLine("提示指令getdertap(int ent, double c, double k, int i, double tMin, double tMax)");
            Console.WriteLine("c阻尼系数 k弹簧刚度 i采样点数 tMin最小时间 tMax最大时间 ent递归层数 返回系统响应精确最小值");
            Console.WriteLine("提示指令bestc_and_k(int ent, int i, double tMin, double tMax)");
            Console.WriteLine("c阻尼系数 k弹簧刚度 i采样点数 tMin最小时间 tMax最大时间 ent递归层数 返回系统响应精确最大最小差值");
            while (true)
            {
                    string[] commands = input();
                    if (commands.Length == 0)
                    {
                        continue; // 如果没有输入命令，继续下一次循环
                    }
                    else if (commands[0] == "getr")
                    {
                        runformname = "Formhtr";
                    }
                    else if (commands[0] == "getderta")
                    {
                        runformname = "Formhtderta";
                    }
                    else if (commands[0] == "gety")
                    {
                        runformname = "Formy";
                    }
                    else if (commands[0] == "gety1")
                    {
                        runformname = "Formy1";
                    }

                    else if(commands[0] == "getdertap")
                    {
                        getdertap(int.Parse(commands[1]),double.Parse( commands[2]),double .Parse( commands[3]),int.Parse( commands[4]),double.Parse( commands[5]),double.Parse( commands[6]));
                        Console.Out.WriteLine("此时系统响应的最大最小差值为"+(maxyp - miniyp).ToString());
                    }
                    else if (commands[0] == "bestc_and_k")
                    {
                        bestc_and_k(int.Parse(commands[1]), int.Parse(commands[2]), double.Parse(commands[3]), double.Parse(commands[4]));
                        Console.WriteLine("最佳的阻尼系数为" + bestc.ToString()+ "__最佳的弹簧刚度为"+bestk.ToString()+"此时车轮的震动最高值为"+minidertap.ToString());
                    }
                    else if (commands[0] == "exit")
                    {
                        return; // 退出程序
                    }
                    else
                    {
                        Console.WriteLine("未知命令，请重试。");
                    }
                    RunForm();
            }
        }

        static void RunForm()
        {
                if (runformname == "Formhtr")
                {
                    Application.Run(new Formhtr());
                }
                else if (runformname == "Formhtderta")
                {
                    Application.Run(new Formhtderta());
                }
                else if (runformname == "Formy")
                {
                    Application.Run(new Formhty());
                }
                 else if (runformname == "Formy1")
                {
                    Application.Run(new Formhty1(30000d, 10000));
                 }


        }


    }
}