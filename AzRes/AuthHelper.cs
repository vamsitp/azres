namespace AzRes
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;

    class AuthHelper
    {
        //============= Config [Edit these with your settings] =====================
        internal const string clientId = "872cd9fa-d31f-45e0-9eab-6e460a02d1f1";       // Change to your app registration's Application ID, unless you are an MSA backed account
        internal const string replyUri = "urn:ietf:wg:oauth:2.0:oob";                  // Change to your app registration's reply URI, unless you are an MSA backed account
        //==========================================================================

        internal const string azureDevOpsResourceId = "https://management.azure.com"; // Constant value to target Azure DevOps. Do not change

        public static async Task<string> GetAuthTokenAsync(string tenantId)
        {
            var ctx = GetAuthenticationContext(tenantId);
            AuthenticationResult result = null;
            var promptBehavior = new PlatformParameters(PromptBehavior.SelectAccount);

            try
            {
                result = await ctx.AcquireTokenAsync(azureDevOpsResourceId, clientId, new Uri(replyUri), promptBehavior);
            }
            catch (UnauthorizedAccessException)
            {
                // If the token has expired, prompt the user with a login prompt
                result = await ctx.AcquireTokenAsync(azureDevOpsResourceId, clientId, new Uri(replyUri), promptBehavior);
            }

            return result?.AccessToken;
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
