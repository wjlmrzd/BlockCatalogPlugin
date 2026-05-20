using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 批量处理汇总生成器
    /// 根据批量处理结果生成汇总表格
    /// </summary>
    public class BatchSummaryGenerator
    {
        /// <summary>
        /// 生成汇总目录实体
        /// </summary>
        /// <param name="results">批量处理结果列表</param>
        /// <param name="style">目录样式配置</param>
        /// <param name="insertPoint">插入位置</param>
        /// <returns>CAD Entity列表（Polyline边框 + DBText文字）</returns>
        public List<Entity> Generate(List<BatchResult> results, CatalogStyle style, Point3d insertPoint)
        {
            if (results == null || results.Count == 0)
                throw new ArgumentException("没有可生成汇总的处理结果");

            var entities = new List<Entity>();

            // 定义列信息
            var columns = new[]
            {
                new { Header = "文件名", Width = 60.0 },
                new { Header = "状态", Width = 20.0 },
                new { Header = "生成数量", Width = 25.0 },
                new { Header = "时间", Width = 40.0 }
            };

            int colCount = columns.Length;
            double[] colWidths = new double[colCount];
            for (int c = 0; c < colCount; c++)
                colWidths[c] = columns[c].Width;

            // 计算尺寸
            double totalWidth = colWidths.Sum();
            double headerHeight = style.HeaderHeight > 0 ? style.HeaderHeight : style.RowHeight * 1.2;
            int rowCount = results.Count;
            double totalHeight = headerHeight + rowCount * style.RowHeight;

            double startX = insertPoint.X;
            double startY = insertPoint.Y;

            // 绘制外边框
            var outerBorder = CreateRectangle(startX, startY, totalWidth, totalHeight);
            entities.Add(outerBorder);

            // 绘制垂直分割线
            double x = startX;
            for (int c = 0; c <= colCount; c++)
            {
                if (c > 0) x += colWidths[c - 1];
                if (c > 0 && c < colCount)
                {
                    var vLine = CreateLine(x, startY, x, startY - totalHeight);
                    entities.Add(vLine);
                }
            }

            // 绘制水平分割线（表头与内容之间）
            double y = startY - headerHeight;
            entities.Add(CreateLine(startX, y, startX + totalWidth, y));

            // 行分隔线
            double rowY = startY - headerHeight;
            for (int r = 0; r < rowCount; r++)
            {
                rowY -= style.RowHeight;
                if (r < rowCount - 1)
                {
                    entities.Add(CreateLine(startX, rowY, startX + totalWidth, rowY));
                }
            }

            // 绘制表头
            double textHeight = style.RowHeight * 0.7;
            double headerTextY = startY - textHeight * 0.5;

            x = startX;
            for (int c = 0; c < colCount; c++)
            {
                x += colWidths[c];
                entities.Add(CreateText(columns[c].Header, new Point3d(x - columns[c].Width + 2, headerTextY, 0), textHeight, true));
            }

            // 绘制数据行
            double rowTextY = startY - headerHeight - textHeight * 0.5;

            foreach (var result in results)
            {
                x = startX;

                // 文件名（截断过长的路径）
                string fileName = System.IO.Path.GetFileName(result.FilePath);
                if (fileName.Length > 18)
                    fileName = fileName.Substring(0, 15) + "...";
                entities.Add(CreateText(fileName, new Point3d(x + 2, rowTextY, 0), textHeight, false));

                x += colWidths[0];

                // 状态
                string status = result.Success ? "成功" : "失败";
                entities.Add(CreateText(status, new Point3d(x + 2, rowTextY, 0), textHeight, false));

                x += colWidths[1];

                // 生成数量
                string count = result.BlocksGenerated.ToString();
                entities.Add(CreateText(count, new Point3d(x + 2, rowTextY, 0), textHeight, false));

                x += colWidths[2];

                // 时间
                string time = result.ProcessedAt.ToString("HH:mm:ss");
                entities.Add(CreateText(time, new Point3d(x + 2, rowTextY, 0), textHeight, false));

                rowTextY -= style.RowHeight;
            }

            // 添加统计信息
            int successCount = results.FindAll(r => r.Success).Count;
            int failCount = results.Count - successCount;

            double summaryY = rowTextY - style.RowHeight;
            entities.Add(CreateLine(startX, summaryY, startX + totalWidth, summaryY));

            string summaryText = $"汇总: {successCount} 个成功, {failCount} 个失败";
            entities.Add(CreateText(summaryText, new Point3d(startX + 2, summaryY - textHeight, 0), textHeight, true));

            return entities;
        }

        /// <summary>
        /// 生成汇总目录并插入到CAD
        /// </summary>
        public ObjectId GenerateAndInsert(List<BatchResult> results, CatalogStyle style, Point3d insertPoint, string targetLayoutName = null)
        {
            var entities = Generate(results, style, insertPoint);

            if (entities.Count == 0)
                return ObjectId.Null;

            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("无法获取CAD文档");

            ObjectIdCollection objectIds = new ObjectIdCollection();

            using (var docLock = doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
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

                    foreach (var entity in entities)
                    {
                        targetBtr.AppendEntity(entity);
                        tr.AddNewlyCreatedDBObject(entity, true);
                        objectIds.Add(entity.Id);
                    }

                    tr.Commit();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }

            return objectIds.Count > 0 ? objectIds[0] : ObjectId.Null;
        }

        /// <summary>
        /// 创建矩形边框（闭合Polyline）
        /// </summary>
        private Polyline CreateRectangle(double x, double y, double width, double height)
        {
            var pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(x, y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(x + width, y), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(x + width, y - height), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(x, y - height), 0, 0, 0);
            pl.Closed = true;
            return pl;
        }

        /// <summary>
        /// 创建直线
        /// </summary>
        private Line CreateLine(double x1, double y1, double x2, double y2)
        {
            return new Line(new Point3d(x1, y1, 0), new Point3d(x2, y2, 0));
        }

        /// <summary>
        /// 创建单行文字
        /// </summary>
        private DBText CreateText(string text, Point3d position, double height, bool bold)
        {
            var dbText = new DBText
            {
                TextString = text ?? "",
                Position = position,
                Height = height,
                HorizontalMode = TextHorizontalMode.TextLeft,
                VerticalMode = TextVerticalMode.TextBottom,
                AlignmentPoint = position
            };
            if (bold)
            {
                dbText.WidthFactor = 1.2;
            }
            return dbText;
        }
    }
}