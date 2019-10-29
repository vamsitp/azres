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

        static void Main(string[] args)
        {
            if (args?.Length > 0)
            {
                var azRes = JsonConvert.DeserializeObject<AzResources>(File.ReadAllText(args[0])).value.OrderBy(x => x.id);
                var outputFile = $"./{nameof(AzResources)}{azRes.FirstOrDefault().id?.Split(Separator)?.FirstOrDefault().Replace("/", "_").Replace("subscriptions", "sub").Replace("resourceGroups", "rg")}.xlsx";

                WriteToTarget(azRes.Select(x =>
                {
                    var ids = x.id?.Split(Separator)?.LastOrDefault().Split('/');
                    var result = new
                    {
                        COMPONENT = ids[0].Replace($"{nameof(Microsoft)}.", string.Empty, StringComparison.OrdinalIgnoreCase),
                        MODULE = ids[1],
                        ID = ids[2],
                        NAME = x.name,
                        TYPE = x.type,
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

                Console.WriteLine(outputFile);
            }
            else
            {
                Console.WriteLine("Save the JSON from the below link and provide the file-path as input.\nhttps://resources.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources");
            }

            Console.ReadLine();
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
