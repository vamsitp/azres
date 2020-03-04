namespace AzRes
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;

    using ColoredConsole;

    using OfficeOpenXml;
    using OfficeOpenXml.Drawing.Chart;
    using OfficeOpenXml.Table.PivotTable;

    public class PackageHelper
    {
        private const char Slash = '/';

        public static void WriteToTarget<T>(IEnumerable<T> records, int index, string header, ExcelPackage pkg, int noOfGroups = 0, int maxGroupCount = 0)
        {
            var sheetName = index > 0 ? $"{index}. {header.Split(Slash).LastOrDefault()}" : $"{header.Split(Slash).LastOrDefault()}";
            if (sheetName.Length > 31)
            {
                sheetName = sheetName.Substring(0, 31);
            }

            var ws = pkg.Workbook.Worksheets.SingleOrDefault(x => x.Name.Equals(sheetName));
            if (ws != null)
            {
                ColorConsole.WriteLine($"Deleting and recreating existing Sheet: {sheetName}".Yellow());
                pkg.Workbook.Worksheets.Delete(ws);
            }
            else
            {
                ColorConsole.WriteLine($"Creating Sheet: {sheetName}");
            }

            ws = pkg.Workbook.Worksheets.Add(sheetName);
            ws.Cells.Style.Font.Name = "Segoe UI";
            ws.Cells.Style.Font.Size = 10;
            ws.Cells.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
            ws.Cells.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
            var range = ws.Cells.LoadFromCollection(records, true, OfficeOpenXml.Table.TableStyles.Light13);
            ws.InsertRow(1, 1);
            var title = ws.Cells[1, 1];
            title.Value = header.ToUpperInvariant();
            title.Style.Font.Bold = true;
            //// title.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 91, 155, 213));
            ws.Column(ws.Cells.Columns).Style.WrapText = true;
            ws.View.FreezePanes(3, noOfGroups > 0 ? 5 : 4);
            ws.Cells.AutoFitColumns(10, 25);

            if (noOfGroups == 0)
            {
                pkg.Workbook.Worksheets.MoveToStart(sheetName);
            }
            else
            {
                var rowCount = ws.Dimension.End.Row;
                var colCount = ws.Dimension.End.Column;

                var pivotTable = ws.PivotTables.Add(ws.Cells[$"A{rowCount + 2}"], ws.Cells[2, 1, rowCount, colCount], $"PivotTable_{sheetName}");
                pivotTable.TableStyle = OfficeOpenXml.Table.TableStyles.Medium13;
                var rowField = pivotTable.RowFields.Add(pivotTable.Fields[0]);
                //pivotTable.ColumnFields.Add(pivotTable.Fields[1]);
                //pivotTable.ColumnFields.Add(pivotTable.Fields[2]);
                var dataField = pivotTable.DataFields.Add(pivotTable.Fields[0]);
                dataField.Function = DataFieldFunctions.Count;
                // pivotTable.DataOnRows = false;

                var chart = (ExcelBarChart)ws.Drawings.AddChart($"PivotChart_{sheetName}", eChartType.BarStacked, pivotTable);
                //chart.XAxis.DisplayUnit = 1;
                //chart.XAxis.MajorUnit = 1;
                //chart.XAxis.MaxValue = maxGroupCount;
                // chart.Style = eChartStyle.Style8;
                chart.RoundedCorners = false;
                chart.DataLabel.Font.Color = Color.White;
                chart.DataLabel.ShowValue = true;
                chart.SetPosition(rowCount + 1, 0, 2, 0);
                chart.SetSize(480, 40 * noOfGroups);
            }

            pkg.Save();
        }
    }
}
