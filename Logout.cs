
using System.IO;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Microsoft.AzureGithub
{
    public static class Logout
    {
        [FunctionName("Logout")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            try
            {
                log.Info("C# HTTP trigger function processed a request.");

                string name = req.Query["repo"];
                string id = req.Query["id"];
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(id))
                    return new BadRequestObjectResult("Please pass 'repo' or 'id' in the query string.");
                string site = req.Query["site"];

                if (string.IsNullOrWhiteSpace(site))
                    return new BadRequestObjectResult("Please pass 'site' in the query string with one of the following values all|github|azure");

                var repoId = name?.Replace("/", "-")?.Replace("_", "-");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    var pairing = await Database.GetPairingRequest(id);
                    if (pairing == null)
                        return new BadRequestObjectResult("Invalid Id");
                    repoId = pairing.RepoId;
                }

                var repo = await Database.GetRepo(repoId);

                if (repo == null)
                    return new BadRequestObjectResult("Invalid Id");
                switch (site.ToLower())
                {
                    case "all":
                        repo.AzureAccount = new Account();
                        repo.AzureData.Subscription = null;
                        repo.GithubAccount = new Account();
                        break;
                    case "github":
                        repo.GithubAccount = new Account();
                        break;
                    case "azure":
                        repo.AzureAccount = new Account();
                        repo.AzureData.Subscription = null;
                        break;
                }
                await Database.Save(repo);
                if (string.IsNullOrWhiteSpace(id))
                {
                    var pariing = await Database.GetOrCreatePairingRequestByRepoId(repoId);
                    id = pariing.Id;
                }
                return new RedirectResult($"{nameof(RegisterRepo)}?id={id}");
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }
        }
    }
}
