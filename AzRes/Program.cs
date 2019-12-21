namespace AzRes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Humanizer;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using OfficeOpenXml;

    class Program
    {
        // Get the token from https://docs.microsoft.com/en-us/rest/api/monitor/diagnosticsettings/list by clicking on the TRY IT button
        private const string Separator = "/providers/";
        private const char Slash = '/';
        private const string AuthHeader = "Authorization";
        private const string Bearer = "Bearer ";
        private const string BaseApiVersion = "2017-05-10";
        private const string DefaultApiVersion = "2019-08-01";

        private readonly static string[] PropsKeyFilters = new[] { "dns", "url", "uri", "link", "host", "path", "cidr", "dns", "fqdn", "address", "server", "gateway", "endpoint", "consortium", "connection"  };
        private readonly static string[] PropsValueFilters = new[] { "://", ".com", ".net", ".io" };

        private static HttpClient Client = new HttpClient();

        // e.g.: "https://management.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources?api-version=2017-05-10"
        static void Main(string[] args)
        {
            var outputFile = string.Empty;
            if (args?.Length > 0)
            {
                outputFile = Path.Combine(!args[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "./" : Path.GetDirectoryName(args[0]), $"{nameof(AzResources)} - {string.Join("_", args.Select(x => x.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ? DateTime.Now.ToString("ddMMMyy") : Path.GetFileNameWithoutExtension(x)))}.xlsx");
                if (File.Exists(outputFile))
                {
                    ColorConsole.Write($"{outputFile} already exists! Overwrite it? (Y/N) ".Yellow());
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
                    ColorConsole.WriteLine("------------------------------");
                    ColorConsole.WriteLine($"\nTenant: {arg.tenant}\nSubscription: {arg.sub}\nResourceGroup: {arg.rg}".Black().OnWhite());
                    var url = $"https://management.azure.com/subscriptions/{arg.sub}/resourceGroups/{arg.rg}/resources?api-version={BaseApiVersion}";
                    var azRes = GetJson(url, arg.tenant).GetAwaiter().GetResult()?.value?.OrderBy(x => x.id);
                    if (azRes == null)
                    {
                        ColorConsole.WriteLine("\nPress any key to quit...");
                        Console.ReadKey();
                        return;
                    }

                    var diagApiVersion = GetApiVersion("microsoft.insights/diagnosticSettings", arg.sub).GetAwaiter().GetResult();
                    var header = azRes.FirstOrDefault().id?.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)?.FirstOrDefault().Trim(Slash); // .Replace(Slash, '_').Replace("subscriptions", "SUBSCRIPTION").Replace("resourceGroups", "RESOURCE-GROUP");
                    WriteToTarget(azRes.Select(x =>
                    {
                        ColorConsole.WriteLine("------------------------------");
                        ColorConsole.WriteLine($"\n{x.id}".Black().OnCyan());
                        var apiVersion = GetApiVersion(x.type, arg.sub).GetAwaiter().GetResult();
                        var props = GetProperties(x.id, apiVersion).GetAwaiter().GetResult();
                        var diag = GetDiagnosticSettings(x.id, diagApiVersion).GetAwaiter().GetResult();
                        var ids = x.id?.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)?.LastOrDefault().Split(Slash);
                        var type = x.type?.Split(new[] { Slash }, 3);
                        var result = new
                        {
                            COMPONENT = type[0].Replace($"{nameof(Microsoft)}.", string.Empty).Replace($"{nameof(Microsoft).ToUpperInvariant()}.", string.Empty).Replace($"{nameof(Microsoft).ToLowerInvariant()}.", string.Empty), // ids[0]
                            MODULE = type[1], // ids[1]
                            SUB_MODULE = type.Length > 2 ? type[2] : string.Empty,
                            ID = ids[2],
                            NAME = x.name,
                            LOCATION = x.location,
                            PROPS = props,
                            DIAG_INFO = diag,
                            KIND = x.kind,
                            MANAGED_BY = x.managedBy?.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)?.LastOrDefault(),
                            SKU_NAME = x.sku?.name,
                            SKU_TIER = x.sku?.tier,
                            SKU_CAPACITY = x.sku?.capacity,
                            SKU_SIZE = x.sku?.size,
                            SKU_FAMILY = x.sku?.family,
                            TAGS = x.tags?.tier,
                            IDENTITY = x.identity?.type,
                        };
                        return result;
                    }), arg.i + 1, header, outputFile);
                }

                ColorConsole.WriteLine($"Output saved to: {outputFile}\nPress 'O' to open the file or any other key to exit...".White().OnGreen());
            }
            else
            {
                ColorConsole.WriteLine("Save the JSON from the below link and provide the file-path as input.\nhttps://resources.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources".Yellow());
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
                AddAuthHeader(path, tenant);
                var response = await Client.GetAsync(path).ConfigureAwait(false);
                result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var err = JsonConvert.DeserializeObject<InvalidAuthTokenError>(result);
                    if (err == null)
                    {
                        ColorConsole.WriteLine($"GetJson - {response.StatusCode}: {response.ReasonPhrase}\n{result}");
                    }
                    else
                    {
                        ColorConsole.WriteLine($"GetJson - {response.StatusCode}: {response.ReasonPhrase}\n{err.error.code}: {err.error.message}".White().OnRed());
                    }
                }
            }

            return JsonConvert.DeserializeObject<AzResources>(result);
        }

        private static void AddAuthHeader(string path, string tenant)
        {
            var token = AuthHelper.GetAuthToken(tenant);
            Client.DefaultRequestHeaders.Remove(AuthHeader);
            Client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
        }

        // Credit: https://zimmergren.net/developing-with-azure-resource-manager-part-5-tip-get-the-available-api-version-for-the-arm-endpoints/
        private static async Task<string> GetApiVersion(string resourceType, string subscription)
        {
            var result = DefaultApiVersion;
            try
            {
                var types = resourceType.Split(new[] { '/' }, 2);
                var url = $"https://management.azure.com/subscriptions/{subscription}/providers/{types[0]}?api-version={result}";
                var response = await Client.GetAsync(url).ConfigureAwait(false);
                ColorConsole.WriteLine($"ApiVersion - {response.StatusCode}: {subscription}/{resourceType}".White().OnDarkBlue());
                var output = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(output))
                {
                    var obj = JsonConvert.DeserializeObject<ResourceType>(output);
                    if (obj != null)
                    {
                        result = obj.resourceTypes.SingleOrDefault(x => x.resourceType.Equals(types[1])).apiVersions.FirstOrDefault();
                    }
                    else
                    {
                        ColorConsole.WriteLine($"{subscription}/{resourceType}: Unable to deserialize:\n{output}".Yellow());
                    }
                }
            }
            catch (Exception ex)
            {
                ColorConsole.WriteLine($"{ex.GetType()} - {ex.Message}".White().OnRed());
            }
            finally
            {
                ColorConsole.WriteLine(result);
            }

            return result;
        }

        private static async Task<string> GetProperties(string resource, string apiVersion)
        {
            var result = string.Empty;
            try
            {
                var url = $"https://management.azure.com{resource}?api-version={apiVersion}";
                var response = await Client.GetAsync(url).ConfigureAwait(false);
                ColorConsole.WriteLine($"Properties - {response.StatusCode}: {resource}".White().OnDarkGreen());
                var output = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(output))
                {
                    var obj = JsonConvert.DeserializeObject<JObject>(output);
                    var dict = FlattenJson(obj);
                    if (dict?.Count > 0)
                    {
                        result = string.Join(Environment.NewLine, dict
                            .Where(Predicate)
                            .Select(x => $"{x.Key.Replace("properties.", string.Empty)}: {x.Value}"));
                    }
                    else
                    {
                        ColorConsole.WriteLine($"{resource}: Unable to deserialize:\n{output}".Yellow());
                        result = output;
                    }
                }
            }
            catch (Exception ex)
            {
                ColorConsole.WriteLine($"{ex.GetType()} - {ex.Message}".White().OnRed());
            }
            finally
            {
                ColorConsole.WriteLine(result);
            }

            return result;

            bool Predicate(KeyValuePair<string, string> x)
            {
                var isProp = x.Key.Contains("properties.");
                var isFilter = PropsKeyFilters.Any(y => x.Key.Humanize().Split(' ').Contains(y, StringComparer.OrdinalIgnoreCase)) || PropsValueFilters.Any(y => x.Value.Contains(".") && x.Value.Split('.').Contains(y, StringComparer.OrdinalIgnoreCase));
                var match = isProp && isFilter;
                return match;
            }
        }

        private static async Task<string> GetDiagnosticSettings(string resource, string apiVersion)
        {
            var result = string.Empty;
            string url = $"https://management.azure.com{resource}/providers/microsoft.insights/diagnosticSettings?api-version={apiVersion}";
            try
            {
                var response = await Client.GetAsync(url).ConfigureAwait(false);
                ColorConsole.WriteLine($"DiagnosticSettings - {response.StatusCode}: {resource}".White().OnDarkMagenta());
                var output = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(output))
                {
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
                        ColorConsole.WriteLine($"{resource}: Unable to deserialize:\n{output}".Yellow());
                        result = output;
                    }
                }
            }
            catch (Exception ex)
            {
                ColorConsole.WriteLine($"{ex.GetType()} - {ex.Message}".White().OnRed());
            }
            finally
            {
                ColorConsole.WriteLine(result);
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
                ws.Cells.LoadFromCollection(records, true, OfficeOpenXml.Table.TableStyles.Light13);
                ws.InsertRow(1, 1);
                var title = ws.Cells[1, 1];
                title.Value = header.ToUpperInvariant();
                title.Style.Font.Bold = true;
                //// title.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 91, 155, 213));
                ws.Column(ws.Cells.Columns).Style.WrapText = true;
                ws.View.FreezePanes(3, 5);
                ws.Cells.AutoFitColumns(10, 25);
                pkg.Save();
            }
        }

        // Credit: https://stackoverflow.com/a/35838986
        private static Dictionary<string, string> FlattenJson(JObject jsonObject)
        {
            var tokens = jsonObject.Descendants().Where(p => p.Count() == 0);
            var results = tokens.Aggregate(new Dictionary<string, string>(), (props, token) =>
            {
                props.Add(token.Path, token.ToString());
                return props;
            });

            return results;
        }
    }
}
