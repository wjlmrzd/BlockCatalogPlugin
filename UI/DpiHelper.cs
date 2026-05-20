using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlockCatalogPlugin.UI
{
    /// <summary>
    /// DPI 缩放辅助类
    /// 处理高分辨率显示器上的界面缩放问题
    /// </summary>
    public static class DpiHelper
    {
        private static float _scaleFactor = 1.0f;
        private static bool _initialized = false;

        /// <summary>
        /// 缩放因子（相对于标准 96 DPI）
        /// </summary>
        public static float ScaleFactor
        {
            get
            {
                if (!_initialized)
                {
                    Initialize();
                }
                return _scaleFactor;
            }
        }

        /// <summary>
        /// 初始化缩放因子
        /// </summary>
        public static void Initialize()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                _scaleFactor = g.DpiX / 96f;
            }
            _initialized = true;
        }

        /// <summary>
        /// 缩放整数尺寸
        /// </summary>
        public static int Scale(int value)
        {
            return (int)(value * ScaleFactor);
        }

        /// <summary>
        /// 缩放 Size
        /// </summary>
        public static Size ScaleSize(Size size)
        {
            return new Size(Scale(size.Width), Scale(size.Height));
        }

        /// <summary>
        /// 缩放 Point
        /// </summary>
        public static Point ScalePoint(Point point)
        {
            return new Point(Scale(point.X), Scale(point.Y));
        }

        /// <summary>
        /// 缩放字体大小
        /// </summary>
        public static float ScaleFont(float fontSize)
        {
            return fontSize * ScaleFactor;
        }

        /// <summary>
        /// 设置窗体的 DPI 缩放模式
        /// </summary>
        public static void SetupFormScaling(Form form)
        {
            form.AutoScaleMode = AutoScaleMode.Font;
            form.Font = new Font("Microsoft YaHei UI", 9F * ScaleFactor);
        }

        /// <summary>
        /// 获取适合当前 DPI 的字体
        /// </summary>
        public static Font GetScaledFont(string familyName, float baseSize, FontStyle style = FontStyle.Regular)
        {
            return new Font(familyName, baseSize * ScaleFactor, style);
        }

        /// <summary>
        /// 创建自适应布局的面板
        /// </summary>
        public static TableLayoutPanel CreateAutoLayoutPanel(int rowCount, int columnCount)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = rowCount,
                ColumnCount = columnCount,
                BackColor = ThemeConfig.Bg,
                Padding = new Padding(Scale(10))
            };

            for (int i = 0; i < columnCount; i++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

            for (int i = 0; i < rowCount; i++)
            {
                panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            return panel;
        }

        /// <summary>
        /// 创建自适应的按钮
        /// </summary>
        public static Button CreateAutoScaleButton(string text, int baseWidth, int baseHeight, Color bgColor, Color fgColor)
        {
            return new Button
            {
                Text = text,
                Size = ScaleSize(new Size(baseWidth, baseHeight)),
                BackColor = bgColor,
                ForeColor = fgColor,
                FlatStyle = FlatStyle.Flat,
                Font = GetScaledFont("Microsoft YaHei UI", 9F),
                Cursor = Cursors.Hand,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
        }
    }
}