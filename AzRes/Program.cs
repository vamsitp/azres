namespace AzRes
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using OfficeOpenXml;

    class Program
    {
        // Get the token from https://docs.microsoft.com/en-us/rest/api/monitor/diagnosticsettings/list by clicking on the TRY IT button
        private const string Separator = "/providers/";
        private const char Slash = '/';
        private static HttpClient Client = new HttpClient();

        private static string TenantId => ConfigurationManager.AppSettings[nameof(TenantId)];
        private static string ClientId => ConfigurationManager.AppSettings[nameof(ClientId)];
        private static string ClientSecret => ConfigurationManager.AppSettings[nameof(ClientSecret)];
        private static string UserName => ConfigurationManager.AppSettings[nameof(UserName)];
        private static string Password => ConfigurationManager.AppSettings[nameof(Password)];

        // e.g.: "https://management.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources?api-version=2017-05-10"
        static void Main(string[] args)
        {
            var outputFile = string.Empty;
            if (args?.Length > 0)
            {
                outputFile = Path.Combine(args[0].StartsWith("https:", StringComparison.OrdinalIgnoreCase) ? "./" : Path.GetDirectoryName(args[0]), $"{nameof(AzResources)} - {string.Join('_', args.Select(x => x.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ? DateTime.Now.ToString("ddMMMyy") : Path.GetFileNameWithoutExtension(x)))}.xlsx");
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
                    // if (!File.Exists(arg.value))
                    // {
                    //     Console.WriteLine($"File not found: {arg.value}");
                    //     continue;
                    // }

                    var azRes = GetJson(arg.value).GetAwaiter().GetResult()?.value?.OrderBy(x => x.id);
                    if (azRes == null)
                    {
                        Console.WriteLine("azRes null!");
                        return;
                    }

                    var header = azRes.FirstOrDefault().id?.Split(Separator)?.FirstOrDefault().Trim(Slash); // .Replace(Slash, '_').Replace("subscriptions", "SUBSCRIPTION").Replace("resourceGroups", "RESOURCE-GROUP");
                    WriteToTarget(azRes.Select(x =>
                    {
                        var diag = GetDiagnostics(x.id).GetAwaiter().GetResult();
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
                Process.Start(new ProcessStartInfo(outputFile) { UseShellExecute = true });
            }
        }

        private static async Task<AzResources> GetJson(string path)
        {
            var result = string.Empty;
            if (File.Exists(path))
            {
                result = File.ReadAllText(path);
            }
            else
            {
                await AddAuthHeader(path).ConfigureAwait(false);
                var response = await Client.GetAsync(path).ConfigureAwait(false);
                result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            return JsonConvert.DeserializeObject<AzResources>(result);
        }

        private static async Task AddAuthHeader(string path)
        {
            var token = await GetAccessToken(path);
            Client.DefaultRequestHeaders.Remove("Authorization");
            Client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
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

        // https://stackoverflow.com/a/40499342
        // https://stackoverflow.com/a/39590155
        private static async Task<string> GetAccessToken(string resourceUrl)
        {
            var client = new HttpClient();
            string tokenEndpoint = $"https://login.microsoftonline.com/{TenantId}/oauth2/token";
            var body = $"resource={resourceUrl}&client_id={ClientId}&client_secret={ClientSecret}&grant_type=password&username={UserName}&password={Password}";
            var stringContent = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
            var result = await client.PostAsync(tokenEndpoint, stringContent).ContinueWith<string>((response) =>
            {
                return response.Result.Content.ReadAsStringAsync().Result;
            });

            var jobject = JObject.Parse(result);
            var token = jobject["access_token"].Value<string>();
            return token;
        }

        ////private static async Task<string> GetAccessToken(string tenantId, string clientId, string clientKey)
        ////{
        ////    try
        ////    {
        ////        var authContextUrl = "https://login.windows.net/" + tenantId;
        ////        var authenticationContext = new AuthenticationContext(authContextUrl);
        ////
        ////        // https://github.com/AzureAD/azure-activedirectory-library-for-dotnet/wiki/Acquiring-tokens-with-username-and-password
        ////        // await context.AcquireTokenAsync(resource, clientId, new UserPasswordCredential("john@contoso.com", johnsPassword))
        ////        var credential = new ClientCredential(clientId, clientKey);
        ////        var result = await authenticationContext.AcquireTokenAsync("https://management.azure.com/", credential);
        ////        if (result == null)
        ////        {
        ////            throw new InvalidOperationException("Failed to obtain the JWT token");
        ////        }

        ////        var token = result.AccessToken;
        ////        return token;
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        Console.WriteLine(ex.Message);
        ////        return null;
        ////    }
        ////}

        ////private static async Task<string> GetAuthTokenSilentAsync(string username, string password)
        ////{
        ////    AuthenticationResult result = null;
        ////    try
        ////    {
        ////        var securePassword = new SecureString();
        ////        foreach (char c in password)
        ////        {
        ////            securePassword.AppendChar(c);
        ////        }

        ////        var app = PublicClientApplicationBuilder.Create(ClientId).WithAuthority(this.Authority).Build();
        ////        result = await app.AcquireTokenByUsernamePassword(new string[] { this.ApiScopes }, username, securePassword).ExecuteAsync().ConfigureAwait(false);
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        Console.WriteLine(ex);
        ////    }

        ////    return result.AccessToken;
        ////}
    }
}
