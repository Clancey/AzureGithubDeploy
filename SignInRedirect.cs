
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System;

namespace Microsoft.AzureGithub
{
    public static class SignInRedirect
    {
        [FunctionName("SignInRedirect")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]HttpRequest req, TraceWriter log)
        {
            try{
                string id = req.Query["state"];
                if(id == null)
                    return new BadRequestObjectResult("Invalid state");
                string code = req.Query["code"]; 
                if(id == null)
                    return new BadRequestObjectResult("Invalid code");
                
                var pairing = await Database.GetPairingRequest(id);
                var repo = await Database.GetRepo(pairing.RepoId);
                

                return new BadRequestObjectResult("Please pass a name on the query string or in the request body");
            }
            catch(Exception e)
            {
                return new BadRequestObjectResult(e);
            }
        }
    }
}
