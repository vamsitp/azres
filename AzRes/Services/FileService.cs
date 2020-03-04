namespace AzRes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    using ColoredConsole;

    using Newtonsoft.Json;

    using OfficeOpenXml;

    public class FileService
    {
        public static void HandleSubscriptions(params string[] inputFiles)
        {
            foreach (var inputFile in inputFiles)
            {
                HandleSubscription(inputFile);
            }
        }

        public static void HandleSubscription(string inputFile)
        {
            if (!File.Exists(inputFile))
            {
                ColorConsole.Write($"{inputFile} does not exist!".White().OnRed());
                return;
            }

            var distinctItems = new List<DistinctResource>();
            var outputFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"{nameof(AzResources)} - {Path.GetFileNameWithoutExtension(inputFile)}.xlsx");
            var outputFileInfo = new FileInfo(outputFile);
            if (outputFileInfo.Exists)
            {
                ColorConsole.Write($"{outputFile} already exists! Overwrite it? (", "Y/N".Green(), ") ".Yellow());
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

            using (var pkg = new ExcelPackage(outputFileInfo)) //file.OpenWrite()
            {
                HandleResources(pkg, 1, distinctItems, inputFile);
                PackageHelper.WriteToTarget(distinctItems.Distinct(), -1, "Distinct_Resources", pkg);
            }

            ColorConsole.WriteLine($"Output saved to: {outputFile}\nPress 'O' to open the file or any other key to exit...".White().OnGreen());
            var open = Console.ReadKey();
            if (File.Exists(outputFile) && open.Key == ConsoleKey.O)
            {
                Process.Start(new ProcessStartInfo(outputFile) { UseShellExecute = true });
            }
        }

        private static void HandleResources(ExcelPackage pkg, int index, List<DistinctResource> distinctItems, string inputFile)
        {
            var azRes = GetJson<AzResources>(inputFile).value.OrderBy(x => x.id);
            if (azRes == null)
            {
                ColorConsole.WriteLine("\nPress any key to quit...");
                Console.ReadKey();
                return;
            }

            var header = azRes.FirstOrDefault().id?.Split(new[] { Utils.Separator }, StringSplitOptions.RemoveEmptyEntries)?.FirstOrDefault().Trim(Utils.Slash); // .Replace(Slash, '_').Replace("subscriptions", "SUBSCRIPTION").Replace("resourceGroups", "RESOURCE-GROUP");
            var results = azRes.Select(x =>
            {
                ColorConsole.WriteLine("------------------------------");
                ColorConsole.WriteLine($"\n{x.id}".Black().OnCyan());

                var ids = x.id?.Split(new[] { Utils.Separator }, StringSplitOptions.RemoveEmptyEntries)?.LastOrDefault().Split(Utils.Slash);
                var type = x.type?.Split(new[] { Utils.Slash }, 3);
                var result = new
                {
                    COMPONENT = type[0].Replace($"{nameof(Microsoft)}.", string.Empty).Replace($"{nameof(Microsoft).ToUpperInvariant()}.", string.Empty).Replace($"{nameof(Microsoft).ToLowerInvariant()}.", string.Empty), // ids[0]
                    MODULE = type[1], // ids[1]
                    SUB_MODULE = type.Length > 2 ? type[2] : string.Empty,
                    ID = ids[2],
                    NAME = x.name,
                    LOCATION = x.location,
                    PROPS = string.Empty,
                    DIAG_INFO = string.Empty,
                    KIND = x.kind,
                    MANAGED_BY = x.managedBy?.Split(new[] { Utils.Separator }, StringSplitOptions.RemoveEmptyEntries)?.LastOrDefault(),
                    SKU_NAME = x.sku?.name,
                    SKU_TIER = x.sku?.tier,
                    SKU_CAPACITY = x.sku?.capacity,
                    SKU_SIZE = x.sku?.size,
                    SKU_FAMILY = x.sku?.family,
                    TAGS = x.tags?.tier,
                    IDENTITY = x.identity?.type,
                };
                return result;
            });

            var groups = results.GroupBy(i => i.COMPONENT);
            var max = groups.Max(x => x.Count());
            var distinct = results.Select(i => new DistinctResource { COMPONENT = i.COMPONENT, MODULE = i.MODULE, SUB_MODULE = i.SUB_MODULE }).Distinct();
            distinctItems.AddRange(distinct);
            PackageHelper.WriteToTarget(results, index, header, pkg, groups.Count(), max);
        }

        private static T GetJson<T>(string path)
        {
            var result = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(result);
        }
    }
}
