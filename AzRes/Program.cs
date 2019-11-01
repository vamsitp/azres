namespace AzRes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    using Newtonsoft.Json;

    using OfficeOpenXml;

    class Program
    {
        private const string Separator = "/providers/";
        private const char Slash = '/';

        static void Main(string[] args)
        {
            var outputFile = string.Empty;
            if (args?.Length > 0)
            {
                outputFile = Path.Combine(Path.GetDirectoryName(args[0]), $"{nameof(AzResources)} - {string.Join('_', args.Select(x => Path.GetFileNameWithoutExtension(x)))}.xlsx");
                if (File.Exists(outputFile))
                {
                    Console.WriteLine($"{outputFile} already exists! Overwrite it? (Y/N)");
                    var input = Console.ReadKey();
                    if (input.Key != ConsoleKey.Y)
                    {
                        return;
                    }
                    else
                    {
                        Console.WriteLine("\n");
                    }
                }

                foreach (var arg in args.Select((value, i) => new { i, value }))
                {
                    if (!File.Exists(arg.value))
                    {
                        Console.WriteLine($"File not found: {arg.value}");
                        continue;
                    }

                    var azRes = JsonConvert.DeserializeObject<AzResources>(GetJson(arg.value)).value.OrderBy(x => x.id);
                    var header = azRes.FirstOrDefault().id?.Split(Separator)?.FirstOrDefault().Trim(Slash); // .Replace(Slash, '_').Replace("subscriptions", "SUBSCRIPTION").Replace("resourceGroups", "RESOURCE-GROUP");

                    WriteToTarget(azRes.Select(x =>
                    {
                        var ids = x.id?.Split(Separator)?.LastOrDefault().Split(Slash);
                        var type = x.type?.Split(Slash, 3);
                        var result = new
                        {
                            COMPONENT = type[0].Replace($"{nameof(Microsoft)}.", string.Empty, StringComparison.OrdinalIgnoreCase), // ids[0]
                            MODULE = type[1], // ids[1]
                            SUB_MODULE = type.Length > 2 ? type[2] : string.Empty,
                            ID = ids[2],
                            NAME = x.name,
                            KIND = x.kind,
                            LOCATION = x.location,
                            MANAGED_BY = x.managedBy?.Split(Separator)?.LastOrDefault(),
                            SKU_NAME = x.sku?.name,
                            SKU_TIER = x.sku?.tier,
                            SKU_CAPACITY = x.sku?.capacity,
                            SKU_SIZE = x.sku?.size,
                            SKU_FAMILY = x.sku?.family,
                            TAGS = x.tags?.tier,
                            IDENTITY = x.identity?.type
                        };
                        return result;
                    }), arg.i + 1, header, outputFile);
                }

                Console.WriteLine($"Output saved to: {outputFile}\nPress 'O' to open the file or any other key to exit...");
            }
            else
            {
                Console.WriteLine("Save the JSON from the below link and provide the file-path as input.\nhttps://resources.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources");
            }

            var key = Console.ReadKey();
            if (File.Exists(outputFile) && key.Key == ConsoleKey.O)
            {
                Process.Start(new ProcessStartInfo(outputFile) { UseShellExecute = true });
            }
        }

        private static string GetJson(string path)
        {
            //var req = WebRequest.Create(path);
            //req.Method = "GET";
            //req.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes("username:password"));
            //var resp = req.GetResponse() as HttpWebResponse;
            return File.ReadAllText(path);
        }

        private static void WriteToTarget<T>(IEnumerable<T> records, int index, string header, string outputFile)
        {
            var sheetName = $"{index}. {header.Split(Slash).LastOrDefault()}".Substring(0, 31);
            using (var pkg = new ExcelPackage(new FileInfo(outputFile)))
            {
                var ws = pkg.Workbook.Worksheets.SingleOrDefault(x => x.Name.Equals(sheetName));
                if (ws != null)
                {
                    Console.WriteLine($"Deleting and recreating existing Sheet: {sheetName}");
                    pkg.Workbook.Worksheets.Delete(ws);
                }
                else
                {
                    Console.WriteLine($"Creating Sheet: {sheetName}");
                }

                ws = pkg.Workbook.Worksheets.Add(sheetName);
                ws.Cells.LoadFromCollection(records, true, OfficeOpenXml.Table.TableStyles.Light13);
                ws.Cells.AutoFitColumns(50);
                ws.InsertRow(1, 1);
                var title = ws.Cells[1, 1];
                title.Value = header.ToUpperInvariant();
                title.Style.Font.Bold = true;
                //// title.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 91, 155, 213));
                ws.View.FreezePanes(3, 4);
                pkg.Save();
            }
        }
    }
}
