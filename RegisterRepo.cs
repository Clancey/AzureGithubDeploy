
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System;
using System.Web;

namespace Microsoft.AzureGithub
{
    public static class RegisterRepo
    {
        [FunctionName("RegisterRepo")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]HttpRequest req, TraceWriter log)
        {
            try
            {
                string id = req.Query["id"];
                if (id == null)
                    return new BadRequestObjectResult("Invalid ID");

                var pairing = await Database.GetPairingRequest(id);
                if(pairing == null)
                    return new BadRequestObjectResult("Invalid ID");
                var repo = await Database.GetRepo(pairing.RepoId);
                
                var redirectUrl = $"{req.Scheme}://{req.Host.Value}/api/SignInRedirect";
                redirectUrl = HttpUtility.UrlEncode(redirectUrl);
                var url = $"https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/oauth2/authorize?client_id=576f04f2-6e7b-4d6b-ae9f-5462653f341b&response_type=code&redirect_uri={redirectUrl}&resource=https%3a%2f%2fmanagement.azure.com%2f&state={id}";
                return new RedirectResult(url);

            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }
        }
    }
}
