using System;
using System.Diagnostics;
using System.Threading;

namespace led小灯点亮测试
{
    public class Program
    {
        public static void Main()
        {
            Debug.WriteLine("Hello from nanoFramework on ESP32-S3!");

            int count = 0;
            while (true)
            {
                count++;
                Debug.WriteLine($"Running... count = {count}");
                Thread.Sleep(1000);
            }
        }
    }
}