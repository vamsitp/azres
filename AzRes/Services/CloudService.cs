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

    public class CloudService
    {
        // Get the token from https://docs.microsoft.com/en-us/rest/api/monitor/diagnosticsettings/list by clicking on the TRY IT button
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

        private static Dictionary<string, List<(int index, string name, string id, string state)>> subs = new Dictionary<string, List<(int index, string name, string id, string state)>>();
        private static Dictionary<string, List<(int index, string name, string id, string location)>> rgs = new Dictionary<string, List<(int index, string name, string id, string location)>>();

        public static async Task HandleSubscriptions(string tenant, string inputFile = null)
        {
            if (subs.Count == 0)
            {
                var sbJson = await GetJson<dynamic>(SubUrl, tenant);
                var sbList = new List<(int index, string name, string id, string state)>();
                foreach (var s in (sbJson?.value as JArray).Select((item, i) => new { index = i + 1, item }))
                {
                    sbList.Add((s.index, s.item.SelectToken(".displayName").Value<string>(), s.item.SelectToken(".subscriptionId").Value<string>(), s.item.SelectToken(".state").Value<string>()));
                }

                subs.Add(tenant, sbList);
            }

            var subsValue = subs.SingleOrDefault(x => x.Key.Equals(tenant)).Value;
            ColorConsole.WriteLine("\n", "Subscriptions".White().OnGreen());
            foreach (var s in subsValue)
            {
                ColorConsole.WriteLine($"{s.index}.".PadLeft(5).Green(), $" {s.name} - {s.id} ({s.state})");
            }

            ColorConsole.Write("\n> ".Green(), "Subscription ID (hit ", "enter".Green(), " to process ResourceGroups from all Subscriptions): ");
            var subInput = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(subInput))
            {
                var proceed = subsValue.Count <= 1;
                if (!proceed)
                {
                    ColorConsole.Write($"Process all Subscriptions? (", "Y/N".Green(), ") ");
                    var input = Console.ReadKey();
                    if (input.Key != ConsoleKey.Y)
                    {
                        proceed = false;
                    }
                    else
                    {
                        proceed = true;
                        Console.WriteLine("\n");
                    }
                }

                if (proceed)
                {
                    foreach (var sub in subsValue)
                    {
                        await HandleSubscription(tenant, sub, inputFile);
                    }
                }
            }
            else
            {
                var sub = subsValue.SingleOrDefault(s => s.index.ToString().Equals(subInput) || s.name.Equals(subInput) || s.id.Equals(subInput));
                await HandleSubscription(tenant, sub, inputFile);
            }
        }

        private static async Task HandleSubscription(string tenant, (int index, string name, string id, string state) sub, string inputFile)
        {
            var distinctItems = new List<DistinctResource>();
            var outputFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"{nameof(AzResources)} - {sub.name} ({sub.id}).xlsx");
            var outputFileInfo = new FileInfo(outputFile);
            if (outputFileInfo.Exists)
            {
                ColorConsole.Write($"{outputFile} already exists! Overwrite it? (".Yellow(), "Y/N".Green(), ") ".Yellow());
                var input = Console.ReadKey();
                if (input.Key != ConsoleKey.Y)
                {
                    return;
                }
                else
                {
                    File.Delete(outputFile);
                    Console.WriteLine("\n");
                }
            }

            await HadleResourceGroups(tenant, sub, distinctItems, outputFileInfo, inputFile);

            ColorConsole.WriteLine($"Output saved to: {outputFile}\nPress 'O' to open the file or any other key to exit...".White().OnGreen());
            var open = Console.ReadKey();
            if (File.Exists(outputFile) && open.Key == ConsoleKey.O)
            {
                Process.Start(new ProcessStartInfo(outputFile) { UseShellExecute = true });
            }
        }

        private static async Task HadleResourceGroups(string tenant, (int index, string name, string id, string state) sub, List<DistinctResource> distinctItems, FileInfo outputFileInfo, string inputFile)
        {
            using (var pkg = new ExcelPackage(outputFileInfo)) //file.OpenWrite()
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

                var rgsValue = rgs.SingleOrDefault(x => x.Key.Equals(sub.id)).Value;
                ColorConsole.WriteLine("\n", "Resource Groups".White().OnGreen());
                foreach (var r in rgsValue)
                {
                    ColorConsole.WriteLine($"{r.index}.".PadLeft(5).Green(), $" {r.name}");
                }

                ColorConsole.Write("\n> ".Green(), "ResouceGroup ID (hit ", "enter".Green(), " to fetch Resources from all ResourceGroups): ");
                var rgInput = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(rgInput))
                {
                    var proceed = rgsValue.Count <= 1;
                    if (!proceed)
                    {
                        ColorConsole.Write($"Process all ResourceGroups? (", "Y/N".Green(), ") ");
                        var input = Console.ReadKey();
                        if (input.Key != ConsoleKey.Y)
                        {
                            proceed = false;
                        }
                        else
                        {
                            proceed = true;
                            Console.WriteLine("\n");
                        }
                    }

                    if (proceed)
                    {
                        foreach (var rg in rgsValue)
                        {
                            await HandleResourceGroup(tenant, sub.id, rg.name, rg.index, distinctItems, pkg, inputFile);
                        }
                    }
                }
                else
                {
                    var rg = rgsValue.SingleOrDefault(r => r.index.ToString().Equals(rgInput) || r.name.Equals(rgInput) || r.id.Equals(rgInput));
                    await HandleResourceGroup(tenant, sub.id, rg.name, 0, distinctItems, pkg, inputFile);
                }

                PackageHelper.WriteToTarget(distinctItems.Distinct(), -1, "Distinct_Resources", pkg);
            }
        }

        private static async Task HandleResourceGroup(string tenant, string subId, string rgName, int index, List<DistinctResource> distinctItems, ExcelPackage pkg, string inputFile)
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
            var header = azRes.FirstOrDefault().id?.Split(new[] { Utils.Separator }, StringSplitOptions.RemoveEmptyEntries)?.FirstOrDefault().Trim(Utils.Slash); // .Replace(Slash, '_').Replace("subscriptions", "SUBSCRIPTION").Replace("resourceGroups", "RESOURCE-GROUP");
            var results = azRes.Select(async x =>
            {
                ColorConsole.WriteLine("------------------------------");
                ColorConsole.WriteLine($"\n{x.id}".Black().OnCyan());

                var apiVersion = await GetApiVersion(x.type, subId);
                var props = await GetProperties(x.id, apiVersion);
                var diag = await GetDiagnosticSettings(x.id, diagApiVersion);

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
                    PROPS = props,
                    DIAG_INFO = diag,
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

            var items = await Task.WhenAll(results);
            var groups = items.GroupBy(i => i.COMPONENT);
            var max = groups.Max(x => x.Count());
            var distinct = items.Select(i => new DistinctResource { COMPONENT = i.COMPONENT, MODULE = i.MODULE, SUB_MODULE = i.SUB_MODULE }).Distinct();
            distinctItems.AddRange(distinct);
            PackageHelper.WriteToTarget(items, index, header, pkg, groups.Count(), max);
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
                await AddAuthHeader(tenant);
                var response = await Client.GetAsync(path);
                result = await response.Content.ReadAsStringAsync();
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

        private static async Task AddAuthHeader(string tenant)
        {
            var token = await AuthHelper.GetAuthToken(tenant);
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
                var response = await Client.GetAsync(url);
                ColorConsole.WriteLine($"ApiVersion - {response.StatusCode}: {subscription}/{resourceType}".White().OnDarkBlue());
                var output = await response.Content.ReadAsStringAsync();
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
                var response = await Client.GetAsync(url);
                ColorConsole.WriteLine($"Properties - {response.StatusCode}: {resource}".White().OnDarkGreen());
                var output = await response.Content.ReadAsStringAsync();
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
                var response = await Client.GetAsync(url);
                ColorConsole.WriteLine($"DiagnosticSettings - {response.StatusCode}: {resource}".White().OnDarkMagenta());
                var output = await response.Content.ReadAsStringAsync();
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
