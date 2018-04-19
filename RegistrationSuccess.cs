
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
            
            //If either account is not logged in, send them to the register page.
            if(string.IsNullOrWhiteSpace(repo.AzureAccount.Token) || string.IsNullOrWhiteSpace(repo.GithubAccount.Token))
               return new RedirectResult($"{nameof(RegisterRepo)}?state={id}");
               
            //Check if the Azure Subcription is set.
            if(string.IsNullOrWhiteSpace(repo.AzureData.Subscription))
            {                  
                var redirectUrl = $"{req.Scheme}://{req.Host.Value}/api/{nameof(Settings)}?id={id}&AzureSubscriptionId=YourSubscriptionId";
                return new OkObjectResult($"You still need to set your Azure Subscription. Edit the URL to include your Azure subscription ID {redirectUrl} and navigate there.");
            }
            return new OkObjectResult($"Success, {repo.RepoName} is now registered");
        }
    }
}
