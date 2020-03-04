namespace AzRes
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;

    using Microsoft.IdentityModel.Clients.ActiveDirectory;

    class AuthHelper
    {
        internal const string ClientId = "872cd9fa-d31f-45e0-9eab-6e460a02d1f1";       // Change to your app registration's Application ID, unless you are an MSA backed account
        internal const string ReplyUri = "urn:ietf:wg:oauth:2.0:oob";                  // Change to your app registration's reply URI, unless you are an MSA backed account
        internal const string AzureDevOpsResourceId = "https://management.azure.com"; // Constant value to target Azure DevOps. Do not change

        internal static readonly ConcurrentDictionary<string, string> AuthTokens = new ConcurrentDictionary<string, string>();

        public static string GetAuthToken(string tenantId)
        {
            var accessToken = AuthTokens.GetOrAdd(tenantId ?? string.Empty, k =>
            {
                var ctx = GetAuthenticationContext(tenantId);
                AuthenticationResult result = null;
                var promptBehavior = new PlatformParameters(PromptBehavior.SelectAccount);

                try
                {
                    result = ctx.AcquireTokenAsync(AzureDevOpsResourceId, ClientId, new Uri(ReplyUri), promptBehavior).GetAwaiter().GetResult();
                }
                catch (UnauthorizedAccessException)
                {
                    // If the token has expired, prompt the user with a login prompt
                    result = ctx.AcquireTokenAsync(AzureDevOpsResourceId, ClientId, new Uri(ReplyUri), promptBehavior).GetAwaiter().GetResult();
                }

                return result?.AccessToken;
            });

            return accessToken;
        }

        private static AuthenticationContext GetAuthenticationContext(string tenant)
        {
            AuthenticationContext ctx = null;
            if (!string.IsNullOrWhiteSpace(tenant))
            {
                ctx = new AuthenticationContext("https://login.microsoftonline.com/" + tenant);
            }
            else
            {
                ctx = new AuthenticationContext("https://login.windows.net/common");
                if (ctx.TokenCache.Count > 0)
                {
                    string homeTenant = ctx.TokenCache.ReadItems().First().TenantId;
                    ctx = new AuthenticationContext("https://login.microsoftonline.com/" + homeTenant);
                }
            }

            return ctx;
        }
    }
}
