using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 用户偏好配置
    /// </summary>
    public class UserPreferences
    {
        // 排序偏好
        public int LastSortTypeIndex { get; set; } = 2;

        // 序号格式偏好
        public int LastSeqFormatIndex { get; set; } = 0;

        // 输出位置偏好
        public bool LastOutputToLayout { get; set; } = false;

        // 列配置偏好
        public List<string> LastSelectedColumns { get; set; } = new List<string>();

        // 图框过滤偏好
        public string LastFilterLayer { get; set; } = "";
        public string LastFilterBlockName { get; set; } = "";
        public int LastFrameSelectionMode { get; set; } = 0;

        // 表格样式偏好
        public string TableFontName { get; set; } = "宋体";
        public double TableFontHeight { get; set; } = 3.5;
        public double TableRowHeight { get; set; } = 8.0;
        public double TableHeaderHeight { get; set; } = 10.0;
        public bool DrawTableBorder { get; set; } = true;
        public bool SmartMergeEnabled { get; set; } = false;

        // 批量处理偏好
        public string LastBatchFolder { get; set; } = "";
        public bool BatchGenerateSummary { get; set; } = true;

        // 界面偏好
        public bool AutoSwitchToConfigTab { get; set; } = true;
        public bool ShowLogTimestamp { get; set; } = true;

        // 快捷键偏好
        public string ShortcutKey { get; set; } = "bca";
    }

    /// <summary>
    /// 用户偏好管理器
    /// </summary>
    public class PreferencesManager
    {
        private static readonly string PreferencesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WjlToolsBox", "BlockCatalog", "preferences.json"
        );

        private static PreferencesManager _instance;
        public static PreferencesManager Instance => _instance ??= new PreferencesManager();

        private UserPreferences _preferences;

        public UserPreferences Preferences => _preferences ?? Load();

        private PreferencesManager()
        {
            EnsureDirectoryExists();
        }

        private void EnsureDirectoryExists()
        {
            var dir = Path.GetDirectoryName(PreferencesFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        /// <summary>
        /// 加载用户偏好
        /// </summary>
        public UserPreferences Load()
        {
            try
            {
                if (File.Exists(PreferencesFilePath))
                {
                    string json = File.ReadAllText(PreferencesFilePath);
                    _preferences = JsonSerializer.Deserialize<UserPreferences>(json);
                    if (_preferences != null) return _preferences;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载偏好失败: {ex.Message}");
            }

            _preferences = new UserPreferences();
            return _preferences;
        }

        /// <summary>
        /// 保存用户偏好
        /// </summary>
        public void Save()
        {
            try
            {
                EnsureDirectoryExists();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_preferences, options);
                File.WriteAllText(PreferencesFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存偏好失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void Reset()
        {
            _preferences = new UserPreferences();
            Save();
        }
    }
}