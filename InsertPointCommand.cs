using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Collections.Generic;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 目录插入点命令 - 处理预览确认后的插入流程
    /// </summary>
    public class InsertPointCommand
    {
        /// <summary>
        /// 执行插入命令 - 提示用户选择插入点并生成目录
        /// </summary>
        /// <param name="blocks">块数据</param>
        /// <param name="style">目录样式</param>
        /// <param name="mergeConfig">合并配置</param>
        /// <param name="targetLayoutName">目标布局（null则模型空间）</param>
        /// <param name="onComplete">完成回调</param>
        public void Execute(BlockDataResult blocks, CatalogStyle style, MergeConfig mergeConfig = null,
            string targetLayoutName = null, System.Action<bool, string> onComplete = null)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                onComplete?.Invoke(false, "无法获取CAD文档");
                return;
            }

            var ed = doc.Editor;

            // 提示用户选择插入点
            var opt = new PromptPointOptions("\n请指定目录插入点：");
            opt.AllowNone = false;

            var result = ed.GetPoint(opt);
            if (result.Status != PromptStatus.OK)
            {
                onComplete?.Invoke(false, "未指定插入点");
                return;
            }

            var insertPoint = result.Value;

            // 生成目录实体
            try
            {
                var generator = new CatalogGenerator();
                var entities = generator.Generate(blocks, style, insertPoint, targetLayoutName);

                // 插入到CAD
                InsertEntitiesToCad(entities, targetLayoutName);

                onComplete?.Invoke(true, $"目录已插入到 ({insertPoint.X:F1}, {insertPoint.Y:F1})");
            }
            catch (System.Exception ex)
            {
                onComplete?.Invoke(false, $"生成失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将实体列表插入到CAD
        /// </summary>
        private void InsertEntitiesToCad(List<Entity> entities, string targetLayoutName)
        {
            if (entities == null || entities.Count == 0) return;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            using (var docLock = doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                // 获取目标块表记录
                ObjectId targetBtrId;
                if (!string.IsNullOrEmpty(targetLayoutName) && targetLayoutName != "Model")
                {
                    var lm = LayoutManager.Current;
                    var layoutId = lm.GetLayoutId(targetLayoutName);
                    if (layoutId != ObjectId.Null)
                    {
                        var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                        targetBtrId = layout.BlockTableRecordId;
                    }
                    else
                    {
                        targetBtrId = SymbolUtilityServices.GetBlockModelSpaceId(doc.Database);
                    }
                }
                else
                {
                    targetBtrId = SymbolUtilityServices.GetBlockModelSpaceId(doc.Database);
                }

                var targetBtr = (BlockTableRecord)tr.GetObject(targetBtrId, OpenMode.ForWrite);

                // 添加所有实体
                foreach (var entity in entities)
                {
                    if (entity != null)
                    {
                        targetBtr.AppendEntity(entity);
                        tr.AddNewlyCreatedDBObject(entity, true);
                    }
                }

                tr.Commit();
            }
        }

        /// <summary>
        /// 快捷插入方法 - 使用之前确认的插入点
        /// </summary>
        public void ExecuteWithPoint(BlockDataResult blocks, CatalogStyle style, Point3d insertPoint,
            MergeConfig mergeConfig = null, string targetLayoutName = null)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                var generator = new CatalogGenerator();
                var entities = generator.Generate(blocks, style, insertPoint, targetLayoutName);
                InsertEntitiesToCad(entities, targetLayoutName);
            }
            catch (System.Exception ex)
            {
                var ed = doc.Editor;
                ed.WriteMessage($"\n插入失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 命令类扩展 - 提供 _BCINSERT 命令
    /// </summary>
    public class InsertCommands
    {
        private static InsertPointCommand _insertCommand = new InsertPointCommand();

        /// <summary>
        /// 目录插入命令 - 从UI触发
        /// </summary>
        [CommandMethod("_BCINSERT", CommandFlags.Modal)]
        public void BcInsert()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || Plugin._panel == null) return;

            var ed = doc.Editor;

            // 获取当前面板的数据
            var blocks = Plugin._panel.GetCurrentBlockData();
            if (blocks == null || blocks.Blocks.Count == 0)
            {
                ed.WriteMessage("\n请先提取属性块");
                return;
            }

            var style = Plugin._panel.GetCurrentStyle();
            var mergeConfig = Plugin._panel.GetCurrentMergeConfig();

            // 确定目标布局
            string targetLayout = null;
            var radLayout = Plugin._panel.GetLayoutRadioState();
            if (radLayout)
            {
                targetLayout = LayoutManager.Current.CurrentLayout;
            }

            // 执行插入
            _insertCommand.Execute(blocks, style, mergeConfig, targetLayout, (success, message) =>
            {
                if (success)
                    ed.WriteMessage($"\n{message}");
                else
                    ed.WriteMessage($"\n错误: {message}");
            });
        }

        /// <summary>
        /// 快速插入到指定位置命令
        /// </summary>
        [CommandMethod("_BCINSERTAT", CommandFlags.Modal)]
        public void BcInsertAt()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || Plugin._panel == null) return;

            var ed = doc.Editor;

            // 提示选择插入点
            var opt = new PromptPointOptions("\n请指定目录插入位置：");
            var result = ed.GetPoint(opt);
            if (result.Status != PromptStatus.OK) return;

            var insertPoint = result.Value;

            var blocks = Plugin._panel.GetCurrentBlockData();
            if (blocks == null || blocks.Blocks.Count == 0)
            {
                ed.WriteMessage("\n请先提取属性块");
                return;
            }

            var style = Plugin._panel.GetCurrentStyle();
            var mergeConfig = Plugin._panel.GetCurrentMergeConfig();

            _insertCommand.ExecuteWithPoint(blocks, style, insertPoint, mergeConfig);
            ed.WriteMessage("\n目录已插入");
        }
    }
}