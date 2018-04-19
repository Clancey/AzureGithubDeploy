
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
                var repo = await Database.GetRepo(pairing.RepoId);

                var responseMessage = new HttpResponseMessage(HttpStatusCode.Redirect);
                //responseMessage.Headers.Location = new Uri(redirect.Url);
                return new RedirectResult("http://www.google.com");

            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }
        }
    }
}
