namespace AzRes
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class Program
    {
        // Credit: https://stackoverflow.com/questions/26384034/how-to-get-the-azure-account-tenant-id
        private const string TenantInfoUrl = "https://login.windows.net/{0}.onmicrosoft.com/.well-known/openid-configuration";
        private static HttpClient Client = new HttpClient();

        // e.g.: "https://management.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources?api-version=2017-05-10"
        static async Task Main(string[] args)
        {
            // Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            PrintHelp();
            var tenant = string.Empty;
            do
            {
                try
                {
                    ColorConsole.Write("\n> ".Green());
                    var key = Console.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        PrintHelp();
                        continue;
                    }
                    else if (key.Equals("q", StringComparison.OrdinalIgnoreCase) || key.StartsWith("quit", StringComparison.OrdinalIgnoreCase) || key.StartsWith("exit", StringComparison.OrdinalIgnoreCase) || key.StartsWith("close", StringComparison.OrdinalIgnoreCase))
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
                    else if (key.Equals("s", StringComparison.OrdinalIgnoreCase) || key.Equals("a", StringComparison.OrdinalIgnoreCase))
                    {
                        ColorConsole.Write("> ".Green(), $"Azure (AD) Tenant/Directory name (e.g. ", "abc".Green(), $" in 'abc.onmicrosoft.com'): {tenant}");
                        tenant = Console.ReadLine();
                        if (!Guid.TryParse(tenant, out var tenantId))
                        {
                            var tenantName = tenant.ToLowerInvariant().Replace(".onmicrosoft.com", string.Empty);
                            var response = await Client.GetAsync(string.Format(TenantInfoUrl, tenantName)).ConfigureAwait(false);
                            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var json = JsonConvert.DeserializeObject<JObject>(result);
                            tenant = json?.SelectToken(".issuer")?.Value<string>()?.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)?.LastOrDefault();
                        }

                        await CloudService.HandleSubscriptions(tenant, key);
                    }
                    else if (key.Equals("f", StringComparison.OrdinalIgnoreCase) || key.StartsWith("file", StringComparison.OrdinalIgnoreCase) || key.Equals("j", StringComparison.OrdinalIgnoreCase) || key.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                    {
                        ColorConsole.Write("> ".Green(), "Offline JSON files (space-separated in double-quotes): ");
                        var inputs = Console.ReadLine();
                        FileService.HandleSubscriptions(inputs.Split(new[] { '"' }, StringSplitOptions.RemoveEmptyEntries).Where(f => !string.IsNullOrWhiteSpace(f)).ToArray());
                    }
                }
                catch (Exception ex)
                {
                    ColorConsole.WriteLine(ex.Message.White().OnRed());
                }
            }
            while (true);
        }

        private static void PrintHelp()
        {
            ColorConsole.WriteLine(
                new[]
                {
                    "--------------------------------------------------------------".Green(),
                    "\nEnter ", "s".Green(), " to process online subscriptions",
                    "\nEnter ", "f".Green(), " to process offline files (JSON downloaded from https://resources.azure.com/subscriptions/", "{subscriptionId}".Green(), "/resourceGroups/", "{resourceGroupId}".Green(), "/resources)",
                    "\nEnter ", "c".Green(), " to clear the console",
                    "\nEnter ", "q".Green(), " to quit",
                    "\nEnter ", "?".Green(), " to print this help"
                });
        }
    }
}
