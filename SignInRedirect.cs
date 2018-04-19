
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
                if(code == null)
                    return new BadRequestObjectResult("Invalid code");
                
                var pairing = await Database.GetPairingRequest(id);
                var repo = await Database.GetRepo(pairing.RepoId);

                var orgRedirect = new Uri(Microsoft.AspNetCore.Http.Extensions.UriHelper.GetEncodedUrl(req));                
                var redirectUrl = $"{req.Scheme}://{req.Host.Value}/api/{nameof(SignInRedirect)}";
                var success = await AzureApi.Authenticate(repo,code,redirectUrl);
                return success ? (IActionResult)new RedirectResult($"{nameof(RegistrationSuccess)}?state={id}") :new BadRequestObjectResult("There was an error logging in. Please try again.");
            }
            catch(Exception e)
            {
                return new BadRequestObjectResult(e);
            }
        }
    }
}
