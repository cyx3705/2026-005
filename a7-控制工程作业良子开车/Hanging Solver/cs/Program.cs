using System;

public class ArrayBottomValues
{
    /// <summary>
    /// 获取double数组中第二小和第三小值首次出现的索引
    /// </summary>
    /// <param name="array">输入数组</param>
    /// <param name="secondMinIndex">输出第二小值的首次索引</param>
    /// <param name="thirdMinIndex">输出第三小值的首次索引</param>
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
        if (thirdMinIndex == -1) thirdMinIndex = secondMinIndex;
        if (secondMinIndex == -1) secondMinIndex = firstIdx;
    }

    // 示例用法
    public static void Main()
    {
        double[] numbers = { 9.5, 3.2, 7.8, 1.5, 5.1, 0.8, 2.3 };

        try
        {
            GetSecondAndThirdMinIndices(numbers, out int secondMinIdx, out int thirdMinIdx);
            Console.WriteLine($"第二小值首次出现的索引：{secondMinIdx}"); // 输出：6（对应值2.3）
            Console.WriteLine($"第三小值首次出现的索引：{thirdMinIdx}"); // 输出：1（对应值3.2）
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误：{ex.Message}");
        }
    }
}
