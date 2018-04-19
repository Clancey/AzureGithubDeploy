
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
    public static class Settings
    {
        [FunctionName("Settings")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            string id = req.Query["id"];
            if (id == null)
                return new BadRequestObjectResult("Invalid ID");

            var pairing = await Database.GetPairingRequest(id);
            if(pairing == null)
                return new BadRequestObjectResult("Invalid ID");
            var repo = await Database.GetRepo(pairing.RepoId);

            
            string azureSubscriptionId = req.Query["AzureSubscriptionId"];
            if (azureSubscriptionId != null){
                repo.AzureData.Subscription = azureSubscriptionId;
                await Database.Save(repo);
                return new RedirectResult($"{nameof(RegistrationSuccess)}?state={id}");
            }
            
            return new BadRequestObjectResult("Missing Settings Parameter, i.e : AzureSubscriptionId");
        }
    }
}
