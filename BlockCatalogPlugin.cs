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

            AppDomain.CurrentDomain.AssemblyResolve += OnCadAssemblyResolve;

            TemplateManager.Instance.CreatePresetTemplates();

            Editor ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n✔ 目录生成插件 V2.0 已加载 | 命令: BLOCKCATALOG");
        }

        public void Terminate()
        {
            // 保存用户偏好
            PreferencesManager.Instance.Save();
            try { _ps?.Dispose(); } catch { }
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
                        MinimumSize = new System.Drawing.Size(420, 600),
                        KeepFocus = true
                    };
                    _panel = new BlockCatalogPanel();
                    _ps.Add("目录生成", _panel);
                }
                _ps.Visible = true;
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
        [CommandMethod("_BCSELECT", CommandFlags.Modal)]
        public void BcSelect()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || Plugin._panel == null) return;

            var ed = doc.Editor;
            var filter = new SelectionFilter(new TypedValue[] { new TypedValue(0, "INSERT") });
            var pr = ed.GetSelection(filter);

            if (pr.Status == PromptStatus.OK && pr.Value != null)
            {
                var extractor = new BlockExtractor();
                var result = extractor.ExtractFromSelection(pr.Value);
                Plugin._panel.SafeInvoke(() => Plugin._panel.OnBlocksSelected(result));
            }
            else
            {
                Plugin._panel.SafeInvoke(() => Plugin._panel.AppendLog("未选择到属性块", BlockCatalogPlugin.UI.Theme.Warning));
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
    }
}
