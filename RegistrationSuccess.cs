
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Microsoft.AzureGithub
{
    public static class RegistrationSuccess
    {
        [FunctionName("RegistrationSuccess")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");
            string id = req.Query["state"];       
            if(string.IsNullOrEmpty(id))
                return new BadRequestObjectResult("Invalid ID");     
            var pairing = await Database.GetPairingRequest(id);
            if(pairing == null)
                return new BadRequestObjectResult("Invalid ID");
            var repo = await Database.GetRepo(pairing.RepoId);
            //TODO: Check if Github token is set
            if(string.IsNullOrWhiteSpace(repo.AzureAccount.Token))
               return new RedirectResult($"{nameof(RegisterRepo)}?state={id}");
               
            //TODO: Check if the Azure Subcription is set.
            if(string.IsNullOrWhiteSpace(repo.AzureData.Subscription))
            {                    
                //TODO: output register subscription url
            }
            return new OkObjectResult($"Success, {repo.RepoName} is now registered");
        }
    }
}