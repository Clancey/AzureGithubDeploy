using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.Azure.Documents;
using System.Collections.Generic;
using Microsoft.Azure.Documents.Client;
using System;

using Microsoft.Azure.Documents.Linq;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.AzureGithub
{
    public static class OnPullRequestTrigger
    {
        [FunctionName("OnPullRequestTrigger")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            try{
                log.Info("C# HTTP trigger function processed a request.");
                var eventType = req.Headers["X-GitHub-Event"];
                
                if(eventType != GithubEventTypes.PullRequest && eventType != GithubEventTypes.SetupWebHook)
                    return new OkResult();
                var requestBody = new StreamReader(req.Body).ReadToEnd();
                dynamic data = JsonConvert.DeserializeObject(requestBody);


                if(eventType == GithubEventTypes.SetupWebHook)
                    return await CreateNewApp(req, data);

                var action = (string)data.action;
                if(action == GithubPullRequestActions.Opened)
                    return await CreateNewPullRequest(req, data);
                
                if(action == GithubPullRequestActions.Updated)
                    return UpdateOldApp(data);
                else if(action == GithubPullRequestActions.Closed)
                    return CleanupOldApp(data);                
                return new BadRequestObjectResult($"Not supported action: {action}");
            }
            catch(Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }
        }

        static async Task<IActionResult> CreateNewApp(HttpRequest req, dynamic data)
        {
            var repData = data.repository;
            var id = Database.CleanseName(repData.full_name.ToString());
            var repo = new GithubRepo
            {
                Id = id,
                Owner = repData.owner.login,
                RepoName = repData.Name,
                IsPrivate = repData.@private,
            };
            try{
            var doc = await Database.CreateRepo(repo);
            }
            catch(Exception)
            {

            }
            return await GetRegisterUrl(req,id);
        }

        static async Task<IActionResult> GetRegisterUrl(HttpRequest req, string repoId)
        {
            var paring = await Database.GetOrCreatePairingRequestByRepoId(repoId);
            var orgUrl = req.Host.Value;
            var url = $"{req.Scheme}://{orgUrl}/api/RegisterRepo?id={paring.Id}";
            return new OkObjectResult($"Please go to {url} to register the app with azure.");
        }
        static async Task<IActionResult> CreateNewPullRequest(HttpRequest req, dynamic data)
        {
            var repData = data.repository;
            var id = Database.CleanseName(repData.full_name.ToString());
            GithubRepo repo;
            try{
                repo = await Database.GetRepo(id);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                repo =  new GithubRepo
                {
                    Id = id,
                    Owner = repData.owner.login,
                    RepoName = repData.Name,
                    CloneUrl = repData.clone_url,
                    IsPrivate = repData.@private,
                    AzureData = {
                        Location = "southcentralus",                    }
                };
                await Database.CreateRepo(repo);
            }
            var isAuthenticated = await AzureApi.Authenticate(repo);
            if(!isAuthenticated)
            {
                var paring = await Database.GetOrCreatePairingRequestByRepoId(id);
                var orgUrl = req.Host.Value;
                var url = $"{req.Scheme}://{orgUrl}/api/RegisterRepo?id={paring.Id}";
                return new BadRequestObjectResult($"Please go to {url} to register the app with azure.");
            }
            if(repo.Builds == null)
                repo.Builds = new List<Build>();
            var pullRequest = data.pull_request;
            var number = (int)data.number;
            var build = repo.Builds?.FirstOrDefault(x=> x.PullRequestId == number);
            if(build == null)
            { 
                build = new Build{
                    PullRequestId = number,
                    CommitHash = pullRequest.head.sha,
                    StatusUrl = pullRequest.statuses_url,
                    Branch = pullRequest.head.@ref,
                };
                repo.Builds.Add(build);
            }
            build.Branch = pullRequest.head.@ref;
            //Make sure we have a resource group!
            repo.AzureData.ResourceGroup = await AzureApi.GetOrCreateResourceGroup(repo);
            bool shouldSave = string.IsNullOrWhiteSpace(build.AzureAppId);
            build.AzureAppId = await AzureApi.CreateAppService(repo,build);
            if(shouldSave)
                await Database.Save(repo);
            await AzureApi.CreateWebApp(repo,build);
            build.DeployedUrl = $"http://{build.AzureAppId}.azurewebsites.net";
            var success = await AzureApi.PublishPullRequst(repo,build);
            
            await Database.Save(repo);
            return new OkResult();
        }
        static IActionResult UpdateOldApp(dynamic data)
        {
            //https://docs.microsoft.com/en-us/azure/app-service/scripts/app-service-cli-deploy-github
            return new BadRequestResult();
        }
        static IActionResult CleanupOldApp(dynamic data)
        {
            return new BadRequestResult();
        }

        
    }
}
