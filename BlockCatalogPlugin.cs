using System;
using System.Reflection;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Windows;
using Autodesk.AutoCAD.Geometry;
using BlockCatalogPlugin.UI;

[assembly: ExtensionApplication(typeof(BlockCatalogPlugin.Plugin))]
[assembly: CommandClass(typeof(BlockCatalogPlugin.Commands))]

namespace BlockCatalogPlugin
{
    public class Plugin : IExtensionApplication
    {
        private static PaletteSet _ps;
        private static readonly Guid _guid = new Guid("C8D2E3F4-A5B6-7890-ABCD-EF1234567891");
        private static bool _initialized = false;

        // 静态字段用于UI与命令通信
        internal static BlockCatalogPanel _panel;
        internal static Point3d? _pendingInsertPoint = null;
        internal static bool _pendingGenerateAfterPick = false;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += OnCadAssemblyResolve;

                TemplateManager.Instance.CreatePresetTemplates();

                // 注册文档打开事件，在文档激活后注册快捷键（仅在UI需要时调用，不在启动时自动注册）
                Application.DocumentManager.DocumentActivated += OnDocumentActivated;

                Editor ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                string shortcut = PreferencesManager.Instance.Preferences.ShortcutKey ?? "bca";
                ed?.WriteMessage($"\n✔ 目录生成插件 V2.0 已加载 | 命令: BLOCKCATALOG | 快捷键: {shortcut}");
            }
            catch (System.Exception ex)
            {
                // 捕获加载异常，确保能看到错误信息
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n[ERROR] 插件初始化失败: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 文档激活时处理（不再自动注册快捷键，避免干扰选择操作）
        /// </summary>
        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            // 不再自动注册快捷键，只在用户明确修改时调用 RegisterShortcutKey()
        }

        /// <summary>
        /// 注册快捷键别名（已禁用，不再自动注册以避免干扰CAD选择操作）
        /// </summary>
        internal static void RegisterShortcutKey()
        {
            // 已禁用：-alias 命令在部分 AutoCAD 版本不可用，会导致选择操作被干扰
            // 快捷键功能暂时搁置，用户可使用 BLOCKCATALOG 命令
        }

        /// <summary>
        /// 验证快捷键是否有效（只允许字母和数字）
        /// </summary>
        private static bool IsValidShortcutKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            foreach (char c in key)
            {
                if (!char.IsLetterOrDigit(c)) return false;
            }
            return true;
        }

        public void Terminate()
        {
            // 保存用户偏好
            PreferencesManager.Instance.Save();

            // 注销事件处理器
            Application.DocumentManager.DocumentActivated -= OnDocumentActivated;

            try { _ps?.Dispose(); } catch { }
            // 释放静态 GDI 资源
            try { BlockCatalogPanel.ReleaseCachedResources(); } catch { }
        }

        internal static void EnsurePanelVisible()
        {
            try
            {
                if (_ps == null)
                {
                    _ps = new PaletteSet("目录生成", _guid)
                    {
                        Style = PaletteSetStyles.ShowAutoHideButton
                               | PaletteSetStyles.ShowCloseButton
                               | PaletteSetStyles.Snappable,
                        DockEnabled = DockSides.Left | DockSides.Right | DockSides.None,
                        Dock = DockSides.Right,
                        MinimumSize = new System.Drawing.Size(340, 600)
                    };
                    _panel = new BlockCatalogPanel();
                    _ps.Add("目录生成", _panel);
                }
                _ps.Visible = true;
                // 必须等 Visible=true 之后设置 Size，CAD 布局管理器才会服从
                _ps.Size = new System.Drawing.Size(360, 800);
            }
            catch (System.Exception ex)
            {
                Editor ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n打开目录生成面板失败：{ex.Message}");
            }
        }

        private static Assembly OnCadAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string name = args.Name.Split(',')[0] + ".dll";
            string[] cadDlls = { "accoremgd.dll", "acdbmgd.dll", "acmgd.dll", "AcWindows.dll" };

            if (!cadDlls.Any(d => name.Equals(d, StringComparison.OrdinalIgnoreCase)))
                return null;

            string baseDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string path = System.IO.Path.Combine(baseDir, name);
            if (System.IO.File.Exists(path)) return System.Reflection.Assembly.LoadFrom(path);

            // 从注册表查 AutoCAD 安装路径
            string cadDir = FindCadDirectoryFromRegistry();
            if (!string.IsNullOrEmpty(cadDir))
            {
                path = System.IO.Path.Combine(cadDir, name);
                if (System.IO.File.Exists(path)) return System.Reflection.Assembly.LoadFrom(path);
            }

            return null;
        }

        private static string FindCadDirectoryFromRegistry()
        {
            string[] regKeys = new[]
            {
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Autodesk\AutoCAD\R24.2\InstalledUsers",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Autodesk\AutoCAD\R25.0\InstalledUsers",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Autodesk\AutoCAD\R26.0\InstalledUsers",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Autodesk\AutoCAD\R24.2\InstalledUsers",
            };
            foreach (var key in regKeys)
            {
                try
                {
                    string val = Microsoft.Win32.Registry.GetValue(key, "AcadExePath", null) as string;
                    if (!string.IsNullOrEmpty(val) && System.IO.File.Exists(val))
                        return System.IO.Path.GetDirectoryName(val);
                }
                catch { }
            }
            return null;
        }
    }

    public class Commands
    {
        [CommandMethod("BLOCKCATALOG")]
        public void ShowPanel()
        {
            Plugin.EnsurePanelVisible();
        }

        [CommandMethod("BLOCKCATALOGPANEL")]
        public void ShowPanelAlt()
        {
            Plugin.EnsurePanelVisible();
        }

        /// <summary>
        /// 选择属性块命令 - 从UI按钮触发
        /// </summary>
        [CommandMethod("_BCSELECT", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void BcSelect()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            if (Plugin._panel == null)
            {
                doc.Editor.WriteMessage("\n面板未初始化，请先输入 BLOCKCATALOG 打开面板");
                return;
            }

            var ed = doc.Editor;
            var filter = new SelectionFilter(new TypedValue[] { new TypedValue(0, "INSERT") });
            PromptSelectionResult pr;
            try
            {
                pr = ed.GetSelection(filter);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n选择失败: {ex.Message}");
                return;
            }

            if (pr.Status == PromptStatus.OK && pr.Value != null)
            {
                ExtractionResult result;
                try
                {
                    var extractor = new BlockExtractor();
                    result = extractor.ExtractFromSelection(pr.Value);
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n提取属性块失败: {ex.Message}");
                    return;
                }

                // 直接在 CAD 线程上调用，不跨线程
                try
                {
                    Plugin._panel.OnBlocksSelected(result);
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n刷新面板失败: {ex.Message}");
                }
            }
            else if (pr.Status == PromptStatus.Cancel)
            {
                // 用户按 ESC 取消，不提示
            }
            else
            {
                Plugin._panel.AppendLog("未选择到属性块", BlockCatalogPlugin.UI.Theme.Warning);
            }
        }

        /// <summary>
        /// 点选插入位置命令 - 从UI按钮触发
        /// </summary>
        [CommandMethod("_BCPICKPOS", CommandFlags.Modal)]
        public void BcPickPos()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || Plugin._panel == null) return;

            var ed = doc.Editor;
            var opt = new PromptPointOptions("\n请指定目录插入点：");
            opt.AllowNone = true;

            var result = ed.GetPoint(opt);
            if (result.Status == PromptStatus.OK)
            {
                Plugin._pendingInsertPoint = result.Value;
                Plugin._panel.SafeInvoke(() => Plugin._panel.OnInsertPointSelected(result.Value));
            }
            else
            {
                Plugin._pendingInsertPoint = null;
                Plugin._panel.SafeInvoke(() => Plugin._panel.AppendLog("未指定插入点", BlockCatalogPlugin.UI.Theme.Warning));
            }
        }

        /// <summary>
        /// 点选位置后生成目录命令
        /// </summary>
        [CommandMethod("_BCGENPOS", CommandFlags.Modal)]
        public void BcGenPos()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || Plugin._panel == null) return;

            var ed = doc.Editor;

            // 1. 指定插入位置
            var opt = new PromptPointOptions("\n请指定目录插入位置：");
            opt.AllowNone = true;

            var result = ed.GetPoint(opt);
            if (result.Status != PromptStatus.OK)
            {
                Plugin._pendingGenerateAfterPick = false;
                Plugin._panel.SafeInvoke(() => Plugin._panel.AppendLog("未指定插入位置，生成取消", BlockCatalogPlugin.UI.Theme.Warning));
                return;
            }

            var insertPoint = result.Value;
            Plugin._pendingInsertPoint = insertPoint;

            // 2. 如果启用鼠标拖拽定义尺寸，则继续获取对角点
            var style = Plugin._panel.GetCurrentStyle();
            if (style.UseMouseDefineSize)
            {
                ed.WriteMessage("\n拖拽鼠标定义表格尺寸...");
                var cornerOpt = new PromptCornerOptions("\n拖拽到右下角定义表格大小：", insertPoint);
                cornerOpt.AllowNone = true;

                var cornerResult = ed.GetCorner(cornerOpt);
                if (cornerResult.Status == PromptStatus.OK)
                {
                    // 计算表格尺寸
                    double width = Math.Abs(cornerResult.Value.X - insertPoint.X);
                    double height = Math.Abs(cornerResult.Value.Y - insertPoint.Y);

                    // 根据尺寸计算行高列宽
                    int rowCount = Plugin._panel.GetBlockCount();
                    if (rowCount > 0)
                    {
                        style.RowHeight = height / (rowCount + 1); // +1 for header
                        style.HeaderHeight = style.RowHeight * 1.2;
                    }

                    Plugin._panel.SafeInvoke(() =>
                    {
                        Plugin._panel.OnInsertPointSelected(insertPoint);
                        Plugin._panel.AppendLog($"鼠标定义尺寸: 宽={width:F1}mm, 高={height:F1}mm, 行高={style.RowHeight:F1}mm",
                            BlockCatalogPlugin.UI.Theme.Success);
                        // 继续生成目录
                        if (Plugin._pendingGenerateAfterPick)
                        {
                            Plugin._pendingGenerateAfterPick = false;
                            Plugin._panel.DoGenerateCatalogDirect();
                        }
                    });
                }
                else
                {
                    Plugin._panel.SafeInvoke(() =>
                    {
                        Plugin._panel.OnInsertPointSelected(insertPoint);
                        Plugin._panel.AppendLog("未定义尺寸，使用默认样式", BlockCatalogPlugin.UI.Theme.TextDim);
                        if (Plugin._pendingGenerateAfterPick)
                        {
                            Plugin._pendingGenerateAfterPick = false;
                            Plugin._panel.DoGenerateCatalogDirect();
                        }
                    });
                }
            }
            else
            {
                Plugin._panel.SafeInvoke(() =>
                {
                    Plugin._panel.OnInsertPointSelected(insertPoint);
                    // 继续生成目录
                    if (Plugin._pendingGenerateAfterPick)
                    {
                        Plugin._pendingGenerateAfterPick = false;
                        Plugin._panel.DoGenerateCatalogDirect();
                    }
                });
            }
        }

        /// <summary>
        /// 拾取块名命令
        /// </summary>
        [CommandMethod("_BCPICKBLOCK", CommandFlags.Modal)]
        public void BcPickBlock()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || Plugin._panel == null) return;

            var ed = doc.Editor;
            var opt = new PromptEntityOptions("\n请选择一个图框块：");
            opt.SetRejectMessage("\n必须选择块参照");
            opt.AddAllowedClass(typeof(BlockReference), true);

            var result = ed.GetEntity(opt);
            if (result.Status == PromptStatus.OK)
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var br = tr.GetObject(result.ObjectId, OpenMode.ForRead) as BlockReference;
                    if (br != null)
                    {
                        var btr = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        string blockName = btr?.Name ?? "";
                        Plugin._panel.SafeInvoke(() => Plugin._panel.OnBlockPicked(blockName));
                    }
                }
            }
            else
            {
                Plugin._panel.SafeInvoke(() => Plugin._panel.AppendLog("未选择块", BlockCatalogPlugin.UI.Theme.Warning));
            }
        }

        /// <summary>
        /// 智能提取图框命令
        /// </summary>
        [CommandMethod("_BCSMARTEXTRACT", CommandFlags.Modal)]
        public void BcSmartExtract()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || Plugin._panel == null) return;

            var ed = doc.Editor;
            var frameSelector = new FrameSelector();

            // 获取所有布局名称
            var layoutNames = frameSelector.GetAllLayoutNames();

            var opt = new PromptKeywordOptions("\n选择提取模式 [布局(L)/图层(F)/块名(B)]：");
            opt.Keywords.Add("L");
            opt.Keywords.Add("F");
            opt.Keywords.Add("B");
            opt.AllowNone = true;

            var kpr = ed.GetKeywords(opt);
            if (kpr.Status != PromptStatus.OK) return;

            var config = new FrameSelectionConfig
            {
                Mode = kpr.StringResult switch
                {
                    "L" => NumberingMode.ByLayout,
                    "F" => NumberingMode.Global,
                    "B" => NumberingMode.Global,
                    _ => NumberingMode.Global
                },
                OnlyAttributeBlocks = true
            };

            var result = frameSelector.SelectFrames(config);
            if (result.Blocks.Count > 0)
            {
                Plugin._panel.SafeInvoke(() => Plugin._panel.OnBlocksSelected(result));
                ed.WriteMessage($"\n成功提取 {result.Blocks.Count} 个图框");
            }
            else
            {
                ed.WriteMessage("\n未找到符合条件的图框");
            }
        }

        /// <summary>
        /// 鼠标拖拽定义表格尺寸命令
        /// </summary>
        [CommandMethod("_BCPICKDIMSIZE", CommandFlags.Modal)]
        public void BcPickDimSize()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || Plugin._panel == null) return;

            var style = Plugin._panel.GetCurrentStyle();
            style.UseMouseDefineSize = true;
            Plugin._panel.AppendLog("请在CAD中拖拽定义表格尺寸...", BlockCatalogPlugin.UI.Theme.Primary);
            doc.SendStringToExecute("_BCGENPOS ", true, false, true);
        }

        /// <summary>
        /// 拖拽获取列宽命令（两点自由拾取）
        /// </summary>
        [CommandMethod("_BCPICKCOLWIDTH", CommandFlags.Modal)]
        public void BcPickColWidth()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || Plugin._panel == null) return;

            var ed = doc.Editor;
            // 第一点：用户自由指定列左边界
            var p1Opt = new PromptPointOptions("\n点击列左边界：");
            p1Opt.AllowNone = true;
            var p1Res = ed.GetPoint(p1Opt);
            if (p1Res.Status != PromptStatus.OK)
            {
                Plugin._panel.SafeInvoke(() => Plugin._panel.AppendLog("取消获取列宽", BlockCatalogPlugin.UI.Theme.TextDim));
                return;
            }

            // 第二点：用户自由指定列右边界
            var p2Opt = new PromptCornerOptions("\n点击列右边界：", p1Res.Value);
            p2Opt.AllowNone = true;
            var p2Res = ed.GetCorner(p2Opt);
            if (p2Res.Status != PromptStatus.OK)
            {
                Plugin._panel.SafeInvoke(() => Plugin._panel.AppendLog("取消获取列宽", BlockCatalogPlugin.UI.Theme.TextDim));
                return;
            }

            double width = Math.Abs(p2Res.Value.X - p1Res.Value.X);
            Plugin._panel.SafeInvoke(() => Plugin._panel.ApplyPickedColumnWidth(width));
        }

        /// <summary>
        /// 拖拽获取行高命令（两点自由拾取）
        /// </summary>
        [CommandMethod("_BCPICKROWHEIGHT", CommandFlags.Modal)]
        public void BcPickRowHeight()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || Plugin._panel == null) return;

            var ed = doc.Editor;
            // 第一点：用户自由指定行上边界
            var p1Opt = new PromptPointOptions("\n点击行上边界：");
            p1Opt.AllowNone = true;
            var p1Res = ed.GetPoint(p1Opt);
            if (p1Res.Status != PromptStatus.OK)
            {
                Plugin._panel.SafeInvoke(() => Plugin._panel.AppendLog("取消获取行高", BlockCatalogPlugin.UI.Theme.TextDim));
                return;
            }

            // 第二点：用户自由指定行下边界
            var p2Opt = new PromptCornerOptions("\n点击行下边界：", p1Res.Value);
            p2Opt.AllowNone = true;
            var p2Res = ed.GetCorner(p2Opt);
            if (p2Res.Status != PromptStatus.OK)
            {
                Plugin._panel.SafeInvoke(() => Plugin._panel.AppendLog("取消获取行高", BlockCatalogPlugin.UI.Theme.TextDim));
                return;
            }

            double height = Math.Abs(p2Res.Value.Y - p1Res.Value.Y);
            Plugin._panel.SafeInvoke(() => Plugin._panel.ApplyPickedRowHeight(height));
        }
    }
}
