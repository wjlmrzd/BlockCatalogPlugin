using System;
using System.Collections.Generic;
using System.Linq;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 排序引擎，支持多种排序规则及几何容差模糊排序
    /// </summary>
    public class SortEngine
    {
        public enum SortType
        {
            SelectionOrder,           // 选择顺序
            NumericOrder,            // 按属性值自然数排序
            LeftRight_TopBottom,    // 左右上下（Z形）
            TopBottom_LeftRight,      // 上下左右（S形）
        }

        /// <summary>
        /// 对属性块列表排序
        /// </summary>
        /// <param name="blocks">待排序的属性块列表</param>
        /// <param name="type">排序类型</param>
        /// <param name="tolerance">几何容差阈值（用于模糊判断同一排），默认500</param>
        /// <param name="isReverse">是否反序输出</param>
        /// <returns>排序后的列表</returns>
        public List<AttributeBlockData> Sort(List<AttributeBlockData> blocks, SortType type, double tolerance = 500.0, bool isReverse = false)
        {
            if (blocks == null || blocks.Count <= 1) return blocks ?? new List<AttributeBlockData>();

            List<AttributeBlockData> sorted;

            switch (type)
            {
                case SortType.SelectionOrder:
                    sorted = blocks.OrderBy(b => b.SelectionOrder).ToList();
                    break;
                case SortType.NumericOrder:
                    sorted = blocks.OrderBy(b => ExtractSortKey(b)).ToList();
                    break;
                case SortType.TopBottom_LeftRight:
                    // 上下左右：动态冒泡邻近容差分行法
                    sorted = SortByTopBottom_LeftRight(blocks, tolerance);
                    break;
                case SortType.LeftRight_TopBottom:
                    // 左右上下：动态冒泡邻近容差分列法
                    sorted = SortByLeftRight_TopBottom(blocks, tolerance);
                    break;
                default:
                    sorted = new List<AttributeBlockData>(blocks);
                    break;
            }

            // 如果需要反序（反向排列，但组内顺序保持）
            if (isReverse)
            {
                sorted.Reverse();
            }

            // 重新设置选择顺序
            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].SelectionOrder = i;
            }

            return sorted;
        }

        /// <summary>
        /// 上下左右排序 - 动态冒泡邻近容差分行法
        /// 1. 先按 Y 坐标从大到小排序（上方先出现）
        /// 2. 遍历时若当前块 Y 与上一块 Y 差值绝对值 < tolerance，则认为同一横排
        /// 3. 同一排内按 X 从小到大排序（从左到右）
        /// </summary>
        private List<AttributeBlockData> SortByTopBottom_LeftRight(List<AttributeBlockData> blocks, double tolerance)
        {
            // Step 1: 先按 Y 从大到小排序
            var sortedByY = blocks.OrderByDescending(b => b.Position.Y).ToList();

            // Step 2: 动态冒泡分组，组内按 X 排序
            var result = new List<AttributeBlockData>();
            var currentRow = new List<AttributeBlockData>();
            double? lastY = null;

            for (int i = 0; i < sortedByY.Count; i++)
            {
                var block = sortedByY[i];

                if (lastY.HasValue)
                {
                    double yDiff = Math.Abs(block.Position.Y - lastY.Value);
                    if (yDiff >= tolerance)
                    {
                        // Y 差超过容差，开启新行
                        // 将当前行的块按 X 排序后加入结果
                        foreach (var b in currentRow.OrderBy(x => x.Position.X))
                        {
                            result.Add(b);
                        }
                        currentRow.Clear();
                    }
                }

                currentRow.Add(block);
                lastY = block.Position.Y;
            }

            // 处理最后一行
            if (currentRow.Count > 0)
            {
                foreach (var b in currentRow.OrderBy(x => x.Position.X))
                {
                    result.Add(b);
                }
            }

            return result;
        }

        /// <summary>
        /// 左右上下排序 - 动态冒泡邻近容差分列法
        /// 1. 先按 X 坐标从小到大排序（左边先出现）
        /// 2. 遍历时若当前块 X 与上一块 X 差值绝对值 < tolerance，则认为同一纵列
        /// 3. 同一列内按 Y 从大到小排序（从上到下）
        /// </summary>
        private List<AttributeBlockData> SortByLeftRight_TopBottom(List<AttributeBlockData> blocks, double tolerance)
        {
            // Step 1: 先按 X 从小到大排序
            var sortedByX = blocks.OrderBy(b => b.Position.X).ToList();

            // Step 2: 动态冒泡分组，组内按 Y 排序
            var result = new List<AttributeBlockData>();
            var currentCol = new List<AttributeBlockData>();
            double? lastX = null;

            for (int i = 0; i < sortedByX.Count; i++)
            {
                var block = sortedByX[i];

                if (lastX.HasValue)
                {
                    double xDiff = Math.Abs(block.Position.X - lastX.Value);
                    if (xDiff >= tolerance)
                    {
                        // X 差超过容差，开启新列
                        // 将当前列的块按 Y 排序后加入结果
                        foreach (var b in currentCol.OrderByDescending(y => y.Position.Y))
                        {
                            result.Add(b);
                        }
                        currentCol.Clear();
                    }
                }

                currentCol.Add(block);
                lastX = block.Position.X;
            }

            // 处理最后一列
            if (currentCol.Count > 0)
            {
                foreach (var b in currentCol.OrderByDescending(y => y.Position.Y))
                {
                    result.Add(b);
                }
            }

            return result;
        }

        /// <summary>
        /// 提取用于数值排序的键（处理 "建施-01" → 1 这样的格式）
        /// </summary>
        private int ExtractSortKey(AttributeBlockData block)
        {
            string xh = block.GetAttribute("XH") ?? block.GetAttribute("TH") ?? "";
            var nums = ExtractNumbers(xh);
            return nums.Count > 0 ? nums[0] : int.MaxValue;
        }

        /// <summary>
        /// 按图号数字递增排序（兼容旧名称）
        /// </summary>
        public List<AttributeBlockData> SortByXH(List<AttributeBlockData> blocks)
        {
            return Sort(blocks, SortType.NumericOrder);
        }

        /// <summary>
        /// 从字符串中提取所有数字
        /// </summary>
        private List<int> ExtractNumbers(string s)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(s)) return result;

            string numStr = "";
            foreach (char c in s)
            {
                if (char.IsDigit(c))
                    numStr += c;
                else if (numStr.Length > 0)
                {
                    if (int.TryParse(numStr, out int num))
                        result.Add(num);
                    numStr = "";
                }
            }

            if (numStr.Length > 0 && int.TryParse(numStr, out int lastNum))
                result.Add(lastNum);

            return result;
        }
    }
}
