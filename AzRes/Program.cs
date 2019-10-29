namespace AzRes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using CsvHelper;

    using Newtonsoft.Json;

    class Program
    {
        private const string Separator = "/providers/";

        static void Main(string[] args)
        {
            if (args?.Length > 0)
            {
                var azRes = JsonConvert.DeserializeObject<AzResources>(File.ReadAllText(args[0]));
                var outputFile = $"./{nameof(AzResources)}_{azRes.value.FirstOrDefault().id?.Split(Separator)?.FirstOrDefault().Replace("/", "--")}.csv";

                WriteToTarget(azRes.value.Select(x => new
                {
                    id = x.id?.Split(Separator)?.LastOrDefault(),
                    x.name,
                    x.type,
                    x.kind,
                    x.location,
                    managedBy = x.managedBy?.Split(Separator)?.LastOrDefault(),
                    sku_name = x.sku?.name,
                    sku_tier = x.sku?.tier,
                    sku_capacity = x.sku?.capacity,
                    sku_size = x.sku?.size,
                    sku_family = x.sku?.family,
                    tags = x.tags?.tier,
                    identity = x.identity?.type
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
            using (var csvWriter = new CsvWriter(new StreamWriter(outputFile)))
            {
                csvWriter.WriteRecords(records);
            }
        }
    }
}
