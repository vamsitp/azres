namespace AzRes
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using ColoredConsole;

    class Program
    {
        // e.g.: "https://management.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources?api-version=2017-05-10"
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            PrintHelp();
            do
            {
                var tenant = string.Empty;
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
                    ColorConsole.Write("> ".Green(), "Azure AD Tenant ID (hit ", "enter".Green(), " to use the ", "common".Green(), $" tenant): {tenant}");
                    tenant = Console.ReadLine();
                    await CloudService.HandleSubscriptions(tenant, key);
                }
                else if (key.Equals("f", StringComparison.OrdinalIgnoreCase) || key.StartsWith("file", StringComparison.OrdinalIgnoreCase) || key.Equals("j", StringComparison.OrdinalIgnoreCase) || key.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                {
                    ColorConsole.Write("> ".Green(), "Offline JSON files (space-separated in double-quotes): ");
                    var inputs = Console.ReadLine();
                    FileService.HandleSubscriptions(inputs.Split(new[] { '"' }, StringSplitOptions.RemoveEmptyEntries).Where(f => !string.IsNullOrWhiteSpace(f)).ToArray());
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
                    "\nEnter ", "f".Green(), " to process offline files (JSON downloaded from https://resources.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupId}/resources)",
                    "\nEnter ", "c".Green(), " to clear the console",
                    "\nEnter ", "q".Green(), " to quit",
                    "\nEnter ", "?".Green(), " to print this help"
                });
        }
    }
}
