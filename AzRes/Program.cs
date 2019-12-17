namespace AzRes
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    using OfficeOpenXml;

    class Program
    {
        // Get the token from https://docs.microsoft.com/en-us/rest/api/monitor/diagnosticsettings/list by clicking on the TRY IT button
        private const string Separator = "/providers/";
        private const char Slash = '/';
        private const string AuthHeader = "Authorization";
        private const string Bearer = "Bearer ";
        private static HttpClient Client = new HttpClient();

        private static string TenantId => ConfigurationManager.AppSettings[nameof(TenantId)];

        // e.g.: "https://management.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources?api-version=2017-05-10"
        static void Main(string[] args)
        {
            var outputFile = string.Empty;
            if (args?.Length > 0)
            {
                outputFile = Path.Combine(!args[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "./" : Path.GetDirectoryName(args[0]), $"{nameof(AzResources)} - {string.Join("_", args.Select(x => x.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ? DateTime.Now.ToString("ddMMMyy") : Path.GetFileNameWithoutExtension(x)))}.xlsx");
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

                foreach (var arg in args.Select((value, i) =>
                {
                    var val = value.Split('/');
                    var result = new { i, tenant = val[0], sub = val[1], rg = val[2] };
                    return result;
                }))
                {
                    // if (!File.Exists(arg.value))
                    // {
                    //     Console.WriteLine($"File not found: {arg.value}");
                    //     continue;
                    // }

                    var url = $"https://management.azure.com/subscriptions/{arg.sub}/resourceGroups/{arg.rg}/resources?api-version=2017-05-10";
                    var azRes = GetJson(url, arg.tenant).GetAwaiter().GetResult()?.value?.OrderBy(x => x.id);
                    if (azRes == null)
                    {
                        Console.WriteLine("\nPress any key to quit...");
                        Console.ReadKey();
                        return;
                    }

                    var header = azRes.FirstOrDefault().id?.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)?.FirstOrDefault().Trim(Slash); // .Replace(Slash, '_').Replace("subscriptions", "SUBSCRIPTION").Replace("resourceGroups", "RESOURCE-GROUP");
                    WriteToTarget(azRes.Select(x =>
                    {
                        var diag = GetDiagnostics(x.id).GetAwaiter().GetResult();
                        var ids = x.id?.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)?.LastOrDefault().Split(Slash);
                        var type = x.type?.Split(new[] { Slash }, 3);
                        var result = new
                        {
                            COMPONENT = type[0].Replace($"{nameof(Microsoft)}.", string.Empty).Replace($"{nameof(Microsoft).ToUpperInvariant()}.", string.Empty).Replace($"{nameof(Microsoft).ToLowerInvariant()}.", string.Empty), // ids[0]
                            MODULE = type[1], // ids[1]
                            SUB_MODULE = type.Length > 2 ? type[2] : string.Empty,
                            ID = ids[2],
                            NAME = x.name,
                            KIND = x.kind,
                            LOCATION = x.location,
                            MANAGED_BY = x.managedBy?.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)?.LastOrDefault(),
                            SKU_NAME = x.sku?.name,
                            SKU_TIER = x.sku?.tier,
                            SKU_CAPACITY = x.sku?.capacity,
                            SKU_SIZE = x.sku?.size,
                            SKU_FAMILY = x.sku?.family,
                            TAGS = x.tags?.tier,
                            IDENTITY = x.identity?.type,
                            DIAG_INFO = diag
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
                Process.Start(new ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, outputFile)) { UseShellExecute = true });
            }
        }

        private static async Task<AzResources> GetJson(string path, string tenant)
        {
            var result = string.Empty;
            if (File.Exists(path))
            {
                result = File.ReadAllText(path);
            }
            else
            {
                await AddAuthHeader(path, tenant).ConfigureAwait(false);
                var response = await Client.GetAsync(path).ConfigureAwait(false);
                result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var err = JsonConvert.DeserializeObject<InvalidAuthTokenError>(result);
                    if (err == null)
                    {
                        Console.WriteLine($"{response.StatusCode}: {response.ReasonPhrase}\n{result}");
                    }
                    else
                    {
                        Console.WriteLine($"{response.StatusCode}: {response.ReasonPhrase}\n{err.error.code}: {err.error.message}");
                    }
                }
            }

            return JsonConvert.DeserializeObject<AzResources>(result);
        }

        private static async Task AddAuthHeader(string path, string tenant)
        {
            var token = await AuthHelper.GetAuthTokenAsync(tenant);
            Client.DefaultRequestHeaders.Remove(AuthHeader);
            Client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
        }

        private static async Task<string> GetDiagnostics(string resourceId)
        {
            var result = string.Empty;
            string url = $"https://management.azure.com{resourceId}/providers/microsoft.insights/diagnosticSettings?api-version=2017-05-01-preview";
            try
            {
                var response = await Client.GetAsync(url).ConfigureAwait(false);
                Console.WriteLine($"{response.StatusCode}: {url}");
                var output = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(output))
                {
                    // Console.WriteLine(result);
                    var diag = JsonConvert.DeserializeObject<DiagInfo>(output);
                    if (diag != null)
                    {
                        var diagInfo = diag?.value;
                        if (diagInfo?.Length > 0)
                        {
                            result = "LOGS: " + string.Join(Environment.NewLine, diagInfo?.Select(x => string.Join(", ", x?.properties?.logs?.Select(y => $"{y?.category} - {y?.enabled}"))));
                            result += $"{Environment.NewLine}METRICS: " + string.Join(Environment.NewLine, diagInfo?.Select(x => string.Join(", ", x?.properties?.metrics?.Select(y => $"{y?.category} - {y?.enabled}"))));
                        }
                        else if (diag.error != null)
                        {
                            result = $"ERROR: {diag?.error.code} - {diag?.error.message}";
                        }
                        else if (!string.IsNullOrWhiteSpace(diag.code))
                        {
                            result = $"WARNING: {diag?.code} - {diag?.message}";
                        }
                    }
                    else
                    {
                        result = output;
                    }
                }
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            finally
            {
                Console.WriteLine(result);
                Console.WriteLine("------------------------------");
            }

            return result;
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
                ws.Cells.Style.Font.Name = "Segoe UI";
                ws.Cells.Style.Font.Size = 10;
                ws.Cells.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                ws.Cells.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                ws.Cells.LoadFromCollection(records, true, OfficeOpenXml.Table.TableStyles.Light13);
                ws.InsertRow(1, 1);
                var title = ws.Cells[1, 1];
                title.Value = header.ToUpperInvariant();
                title.Style.Font.Bold = true;
                //// title.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 91, 155, 213));
                ws.Column(ws.Cells.Columns).Style.WrapText = true;
                ws.View.FreezePanes(3, 4);
                ws.Cells.AutoFitColumns(25);
                pkg.Save();
            }
        }
    }
}
