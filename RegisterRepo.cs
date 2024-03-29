
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
                
                if(string.IsNullOrWhiteSpace(repo.AzureAccount.Token)){
                    var redirectUrl = $"{req.Scheme}://{req.Host.Value}/api/{nameof(SignInRedirect)}";
                    redirectUrl = HttpUtility.UrlEncode(redirectUrl);
                    var url = AzureApi.AuthUrl(id,redirectUrl);
                    return new RedirectResult(url);
                }

                if(string.IsNullOrWhiteSpace(repo.GithubAccount.Token))
                {
                    var redirectUrl = $"{req.Scheme}://{req.Host.Value}/api/{nameof(GithubSignIn)}";
                    redirectUrl = HttpUtility.UrlEncode(redirectUrl);
                    var url = GithubApi.AuthUrl(id,redirectUrl);
                    return new RedirectResult(url);
                }

                if(string.IsNullOrWhiteSpace(repo.AzureData.Subscription))
                {
                    var redirectUrl = $"{req.Scheme}://{req.Host.Value}/api/{nameof(Settings)}?id={id}&AzureSubscriptionId=YourSubscriptionId";
                    return new OkObjectResult($"You still need to set your Azure Subscription. Edit the URL to include your Azure subscription ID {redirectUrl} and navigate there.");
                }
                return new RedirectResult($"{nameof(RegistrationSuccess)}?state={id}");

            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }
        }
    }
}
