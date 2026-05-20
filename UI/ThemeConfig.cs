using System.Drawing;

namespace BlockCatalogPlugin.UI
{
    /// <summary>
    /// 统一深色主题配色方案
    /// VS Code / JetBrains 风格
    /// </summary>
    public static class ThemeConfig
    {
        // 背景色（蓝系风格）
        public static Color Bg = Color.FromArgb(45, 45, 48);           // 主背景
        public static Color Card = Color.FromArgb(37, 37, 38);         // 卡片/面板背景
        public static Color InputBg = Color.FromArgb(51, 51, 54);      // 输入框背景
        public static Color LogBg = Color.FromArgb(30, 30, 35);        // 日志区域背景

        // 背景色（青系风格 - 用于主面板）
        public static Color BgAlt = Color.FromArgb(24, 24, 28);        // 替代主背景
        public static Color CardAlt = Color.FromArgb(37, 37, 42);      // 替代卡片背景
        public static Color CardHover = Color.FromArgb(44, 44, 50);    // 卡片悬停背景
        public static Color InputBgAlt = Color.FromArgb(45, 45, 52);   // 替代输入框背景

        // 文字色
        public static Color Text = Color.FromArgb(220, 220, 220);      // 主文字
        public static Color TextBright = Color.FromArgb(245, 245, 250); // 高亮文字
        public static Color TextDim = Color.FromArgb(120, 120, 135);   // 次要文字

        // 功能色（蓝系）
        public static Color Accent = Color.FromArgb(0, 122, 204);      // 主强调色（蓝色）
        public static Color AccentLight = Color.FromArgb(30, 140, 220); // 浅蓝色

        // 功能色（青系 - 用于主面板）
        public static Color Primary = Color.FromArgb(0, 183, 195);     // 主色（青色）
        public static Color PrimaryLight = Color.FromArgb(0, 210, 225); // 浅青色

        // 状态色
        public static Color Success = Color.FromArgb(76, 175, 80);     // 成功（绿色）
        public static Color SuccessAlt = Color.FromArgb(16, 185, 129); // 替代成功色
        public static Color Warning = Color.FromArgb(255, 152, 0);     // 警告（橙色）
        public static Color WarningAlt = Color.FromArgb(245, 158, 11); // 替代警告色
        public static Color Error = Color.FromArgb(239, 68, 68);       // 错误（红色）

        // 边框色
        public static Color Border = Color.FromArgb(63, 63, 70);       // 边框
        public static Color BorderAlt = Color.FromArgb(55, 55, 62);    // 替代边框
        public static Color BorderLight = Color.FromArgb(80, 80, 85);  // 浅边框

        // 状态色
        public static Color Hover = Color.FromArgb(62, 62, 64);        // 悬停背景
        public static Color Selected = Color.FromArgb(0, 122, 204);    // 选中背景
        public static Color Disabled = Color.FromArgb(90, 90, 95);     // 禁用背景
    }
}