namespace AzRes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Humanizer;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using OfficeOpenXml;
    using OfficeOpenXml.Drawing.Chart;
    using OfficeOpenXml.Table.PivotTable;

    class Program
    {
        // Get the token from https://docs.microsoft.com/en-us/rest/api/monitor/diagnosticsettings/list by clicking on the TRY IT button
        private const string Separator = "/providers/";
        private const char Slash = '/';
        private const string AuthHeader = "Authorization";
        private const string Bearer = "Bearer ";
        private const string BaseApiVersion = "2014-04-01";
        private const string ResourcesApiVersion = "2017-05-10";
        private const string DefaultApiVersion = "2019-08-01";

        private readonly static string[] PropsKeyFilters = new[] { "dns", "url", "uri", "link", "host", "path", "cidr", "dns", "fqdn", "address", "server", "gateway", "endpoint", "consortium", "connection" };
        private readonly static string[] PropsValueFilters = new[] { "://", ".com", ".net", ".io" };

        private static HttpClient Client = new HttpClient();

        private const string SubUrl = "https://management.azure.com/subscriptions?api-version=" + BaseApiVersion;
        private const string RgUrl = "https://management.azure.com/subscriptions/{0}/resourceGroups?api-version=" + BaseApiVersion;

        private static string tenant = string.Empty;
        private static Dictionary<string, List<(int index, string name, string id, string state)>> subs = new Dictionary<string, List<(int index, string name, string id, string state)>>();
        private static Dictionary<string, List<(int index, string name, string id, string location)>> rgs = new Dictionary<string, List<(int index, string name, string id, string location)>>();

        // e.g.: "https://management.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources?api-version=2017-05-10"
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            PrintHelp();
            do
            {
                ColorConsole.Write("\n> ".Green());
                var key = Console.ReadLine()?.Trim();

                if (key.Equals("q", StringComparison.OrdinalIgnoreCase) || key.StartsWith("quit", StringComparison.OrdinalIgnoreCase) || key.StartsWith("exit", StringComparison.OrdinalIgnoreCase) || key.StartsWith("close", StringComparison.OrdinalIgnoreCase))
                {
                    ColorConsole.WriteLine("DONE!".White().OnDarkGreen());
                    break;
                }
                else if (key.Equals("?") || key.StartsWith("help", StringComparison.OrdinalIgnoreCase))
                {
                    PrintHelp();
                }
                else if (key.Equals("c", StringComparison.OrdinalIgnoreCase) || key.StartsWith("cls", StringComparison.OrdinalIgnoreCase) || key.StartsWith("clear", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Clear();
                }
                else
                {
                    ColorConsole.Write("> ".Green(), "Azure AD Tenant ID (hit ", "enter".Green(), " to use the ", "common".Green(), $" tenant): {tenant}");
                    tenant = Console.ReadLine();
                    await HandleTenant(tenant);
                }
            }
            while (true);
        }

        private static async Task HandleTenant(string tenant)
        {
            if (subs.Count == 0)
            {
                var sbJson = await GetJson<dynamic>(SubUrl, tenant);
                var sbList = new List<(int index, string name, string id, string state)>();
                foreach (var s in (sbJson?.value as JArray).Select((item, i) => new { index = i + 1, item}))
                {
                    sbList.Add((s.index, s.item.SelectToken(".displayName").Value<string>(), s.item.SelectToken(".subscriptionId").Value<string>(), s.item.SelectToken(".state").Value<string>()));
                }

                subs.Add(tenant, sbList);
            }

            ColorConsole.WriteLine("\nSubscriptions:");
            foreach (var s in subs.SingleOrDefault(x => x.Key.Equals(tenant)).Value)
            {
                ColorConsole.WriteLine($"{s.index}.".PadLeft(5).Green(), $" {s.name} - {s.id} ({s.state})");
            }

            ColorConsole.Write("\n> ".Green(), "Subscription ID (hit ", "enter".Green(), " to fetch all the available Subscriptions): ");
            var subInput = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(subInput))
            {
                foreach (var sub in subs.SingleOrDefault(x => x.Key.Equals(tenant)).Value)
                {
                    await HandleSubscription(tenant, sub);
                }
            }
            else
            {
                var sub = subs.SingleOrDefault(x => x.Key.Equals(tenant)).Value.SingleOrDefault(s => s.index.ToString().Equals(subInput) || s.name.Equals(subInput) || s.id.Equals(subInput));
                await HandleSubscription(tenant, sub);
            }
        }

        private static async Task HandleSubscription(string tenant, (int index, string name, string id, string state) sub)
        {
            var distinctItems = new List<DistinctResource>();
            var outputFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"{nameof(AzResources)} - {sub.name} ({sub.id}).xlsx");
            var file = new FileInfo(outputFile);
            if (file.Exists)
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

            using (var pkg = new ExcelPackage(file)) //file.OpenWrite()
            {
                if (rgs.Count == 0)
                {
                    var rgJson = await GetJson<dynamic>(string.Format(RgUrl, sub.id), tenant);
                    var rgList = new List<(int index, string name, string id, string location)>();
                    foreach (var r in (rgJson?.value as JArray).Select((item, i) => new { index = i + 1, item }))
                    {
                        rgList.Add((r.index, r.item.SelectToken(".name").Value<string>(), r.item.SelectToken(".id").Value<string>(), r.item.SelectToken(".location").Value<string>()));
                    }

                    rgs.Add(sub.id, rgList);
                }

                ColorConsole.WriteLine("\nResource Groups:");
                foreach (var r in rgs.SingleOrDefault(x => x.Key.Equals(sub.id)).Value)
                {
                    ColorConsole.WriteLine($"{r.index}.".PadLeft(5).Green(), $" {r.name}");
                }

                ColorConsole.Write("\n> ".Green(), "ResouceGroup ID (hit ", "enter".Green(), " to fetch all the available ResourceGroups): ");
                var rgInput = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(rgInput))
                {
                    foreach (var rg in rgs.SingleOrDefault(x => x.Key.Equals(sub.id)).Value)
                    {
                        await HandleResourceGroup(sub.id, rg.name, rg.index, distinctItems, pkg);
                    }
                }
                else
                {
                    var rg = rgs.SingleOrDefault(x => x.Key.Equals(sub.id)).Value.SingleOrDefault(r => r.index.ToString().Equals(rgInput) || r.name.Equals(rgInput) || r.id.Equals(rgInput));
                    await HandleResourceGroup(sub.id, rg.name, 0, distinctItems, pkg);
                }

                WriteToTarget(distinctItems.Distinct(), -1, "Distinct_Resources", pkg);
            }

            ColorConsole.WriteLine($"Output saved to: {outputFile}\nPress 'O' to open the file or any other key to exit...".White().OnGreen());
            var open = Console.ReadKey();
            if (File.Exists(outputFile) && open.Key == ConsoleKey.O)
            {
                Process.Start(new ProcessStartInfo(outputFile) { UseShellExecute = true });
            }
        }

        private static async Task HandleResourceGroup(string subId, string rgName, int index, List<DistinctResource> distinctItems, ExcelPackage pkg)
        {
            var url = $"https://management.azure.com/subscriptions/{subId}/resourceGroups/{rgName}/resources?api-version={ResourcesApiVersion}";
            var azRes = (await GetJson<AzResources>(url, tenant))?.value?.OrderBy(x => x.id);
            if (azRes == null)
            {
                ColorConsole.WriteLine("\nPress any key to quit...");
                Console.ReadKey();
                return;
            }

            var diagApiVersion = await GetApiVersion("microsoft.insights/diagnosticSettings", subId);
            var header = azRes.FirstOrDefault().id?.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)?.FirstOrDefault().Trim(Slash); // .Replace(Slash, '_').Replace("subscriptions", "SUBSCRIPTION").Replace("resourceGroups", "RESOURCE-GROUP");
            var results = azRes.Select(async x =>
            {
                ColorConsole.WriteLine("------------------------------");
                ColorConsole.WriteLine($"\n{x.id}".Black().OnCyan());
                var apiVersion = await GetApiVersion(x.type, subId);
                var props = await GetProperties(x.id, apiVersion);
                var diag = await GetDiagnosticSettings(x.id, diagApiVersion);
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
            });

            var items = await Task.WhenAll(results);
            var groups = items.GroupBy(i => i.COMPONENT);
            var max = groups.Max(x => x.Count());
            var distinct = items.Select(i => new DistinctResource { COMPONENT = i.COMPONENT, MODULE = i.MODULE, SUB_MODULE = i.SUB_MODULE }).Distinct();
            distinctItems.AddRange(distinct);
            WriteToTarget(items, index, header, pkg, groups.Count(), max);
        }

        private static async Task<T> GetJson<T>(string path, string tenant)
        {
            var result = string.Empty;
            if (File.Exists(path))
            {
                result = File.ReadAllText(path);
            }
            else
            {
                AddAuthHeader(tenant);
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

            return JsonConvert.DeserializeObject<T>(result);
        }

        private static void AddAuthHeader(string tenant)
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

        private static void WriteToTarget<T>(IEnumerable<T> records, int index, string header, ExcelPackage pkg, int noOfGroups = 0, int maxGroupCount = 0)
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

        private static void PrintHelp()
        {
            ColorConsole.WriteLine(
                new[]
                {
                    "--------------------------------------------------------------".Green(),
                    "\nHit ", "Enter".Green(), " to proceed...",
                    "\nEnter ", "c".Green(), " to clear the console",
                    "\nEnter ", "q".Green(), " to quit",
                    "\nEnter ", "?".Green(), " to print this help"
                });
        }
    }

    // Credit: https://github.com/JanKallman/EPPlus/issues/8
    class DistinctResource
    {
        public string COMPONENT { get; set; }
        public string MODULE { get; set; }
        public string SUB_MODULE { get; set; }

        public override bool Equals(object obj)
        {
            return obj is DistinctResource resource &&
                   this.COMPONENT.Equals(resource.COMPONENT, StringComparison.OrdinalIgnoreCase) &&
                   this.MODULE.Equals(resource.MODULE, StringComparison.OrdinalIgnoreCase) &&
                   this.SUB_MODULE.Equals(resource.SUB_MODULE, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            var hashCode = -1522090145;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.COMPONENT);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.MODULE);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.SUB_MODULE);
            return hashCode;
        }
    }
}
