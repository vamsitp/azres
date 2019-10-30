namespace AzRes
{
    using System;
    using System.Collections.Generic;
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
            if (args?.Length > 0)
            {
                var azRes = JsonConvert.DeserializeObject<AzResources>(GetJson(args[0])).value.OrderBy(x => x.id);
                var outputFile = $"./{nameof(AzResources)}{azRes.FirstOrDefault().id?.Split(Separator)?.FirstOrDefault().Replace(Slash, '_').Replace("subscriptions", "sub").Replace("resourceGroups", "rg")}.xlsx";

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
                }), outputFile);

                Console.WriteLine($"{outputFile}\nPress any key to quit...");
            }
            else
            {
                Console.WriteLine("Save the JSON from the below link and provide the file-path as input.\nhttps://resources.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources");
            }

            Console.ReadKey();
        }

        private static string GetJson(string path)
        {
            //var req = WebRequest.Create(path);
            //req.Method = "GET";
            //req.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes("username:password"));
            //var resp = req.GetResponse() as HttpWebResponse;
            return File.ReadAllText(path);
        }

        private static void WriteToTarget<T>(IEnumerable<T> records, string outputFile)
        {
            // using (var csvWriter = new CsvWriter(new StreamWriter(outputFile)))
            // {
            //     csvWriter.WriteRecords(records);
            // }

            using (var pkg = new ExcelPackage(new FileInfo(outputFile)))
            {
                var ws = pkg.Workbook.Worksheets.SingleOrDefault(x => x.Name.Equals(nameof(AzResources)));
                if (ws != null)
                {
                    pkg.Workbook.Worksheets.Delete(ws);
                }

                ws = pkg.Workbook.Worksheets.Add(nameof(AzResources));
                // ws.HeaderFooter.FirstHeader.CenteredText = outputFile.Replace("--", "/").Replace(".xlsx", string.Empty);
                ws.Cells.LoadFromCollection(records, true, OfficeOpenXml.Table.TableStyles.Light13);
                ws.View.FreezePanes(2, 4);
                ws.Cells.AutoFitColumns(50);
                pkg.Save();
            }
        }
    }
}
