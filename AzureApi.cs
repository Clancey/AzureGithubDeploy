using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using System.Net;
using System.Text;
using System.Collections.Generic;

namespace Microsoft.AzureGithub
{
    class AzureApi
    {
        public static async Task<(string username, string password)> GetAzureDeployCredentials(GithubRepo repo, Build build)
        {
            using (var client = CreateClient(repo))
            {
                var resp = await client.PostAsync($"resourceGroups/{repo.AzureData.ResourceGroup}/providers/Microsoft.Web/sites/{build.AzureAppId}/publishxml?api-version=2015-08-01", null);
                var stream = await resp.Content.ReadAsStreamAsync();
                var xml = XElement.Load(stream);
                var element = xml.Elements("publishProfile").Where(el => (string)el.Attribute("publishMethod") == "MSDeploy").FirstOrDefault();
                var username = (string)element.Attribute("userName");
                var password = (string)element.Attribute("userPWD");

                return (username, password);
            }
        }

        public static async Task<string> GetOrCreateResourceGroup(GithubRepo repo)
        {
            using (var client = CreateClient(repo))
            {
                var path = $"resourceGroups/{repo.AzureData.ResourceGroup}?api-version=2018-02-01";
                var resp = await client.GetAsync(path);
                if (resp.StatusCode == HttpStatusCode.OK)
                    return repo.AzureData.ResourceGroup;
                if (resp.StatusCode == HttpStatusCode.NotFound)
                {
                    var input = new
                    {
                        location = repo.AzureData.Location,
                        properties = new { },
                        name = repo.AzureData.ResourceGroup,
                    };
                    resp = await SendMessage(client, path, HttpMethod.Put, input);
                    var data = await resp.Content.ReadAsStringAsync();
                    resp.EnsureSuccessStatusCode();
                    return repo.AzureData.ResourceGroup; ;
                }
                var error = await resp.Content.ReadAsStringAsync();
                resp.EnsureSuccessStatusCode();
            }
            return repo.AzureData.ResourceGroup;
        }

        public static async Task<string> CreateAppService(GithubRepo repo, Build build)
        {
            var name = string.IsNullOrWhiteSpace(build.AzureAppId) ? $"{repo.Id}-Pull{build.PullRequestId}-{Guid.NewGuid().ToString().Substring(0,8)}" : build.AzureAppId.Replace("_","-");
            using (var client = CreateClient(repo))
            {
                var path = $"resourceGroups/{repo.AzureData.ResourceGroup}/providers/Microsoft.Web/serverfarms/{name}?api-version=2016-09-01";
                var input = new {
                    name = name,
                    location = repo.AzureData.Location,
                    properties = new {
                        name = name,
                        perSiteScaling = false
                    },
                    sku = new {
                        name = "F1",
                        tier = "Free",
                        capacity = 1
                    }
                };

                var resp = await SendMessage(client,path,HttpMethod.Put,input,new Dictionary<string, string>{["CommandName"]  ="appservice plan create"});
                var data = await resp.Content.ReadAsStringAsync();
                resp.EnsureSuccessStatusCode();
                build.AzureAppId = name;
                return name;
            }
        }
        public static async Task<string> CreateWebApp(GithubRepo repo, Build build)
        {
            using (var client = CreateClient(repo))
            {
                var path = $"resourceGroups/{repo.AzureData.ResourceGroup}/providers/Microsoft.Web/sites/{build.AzureAppId}?api-version=2016-08-01";
                var resp = await SendMessage(client, path, HttpMethod.Put, new
                {
                    location = repo.AzureData.Location,
                    properties = new
                    {
                        name = build.AzureAppId,
                    }
                });
                var data = await resp.Content.ReadAsStringAsync();
                resp.EnsureSuccessStatusCode();
                return build.AzureAppId;
            }
        }

        public static async Task<bool> PublishPullRequst(GithubRepo repo, Build build)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri($"https://{build.AzureAppId}.scm.azurewebsites.net/");
                var credentials = await GetAzureDeployCredentials(repo, build);

                var key = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.username}:{credentials.password}"));
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", key);

                //Switch branches
                var resp = await SendMessage(client,"settings", HttpMethod.Post,new {
                    key ="branch",
                    value = build.Branch,
                });

                var data = await resp.Content.ReadAsStringAsync();
                resp.EnsureSuccessStatusCode();

                var url = $"https://{build.AzureAppId}.scm.azurewebsites.net/deploy";
                var payload = new
                {
                    format = "basic",
                    url = $"{repo.CloneUrl}#{build.CommitHash}"
                };
                resp = await SendMessage(client, url, HttpMethod.Post, payload);
                data = await resp.Content.ReadAsStringAsync();
                return resp.IsSuccessStatusCode;
            }
        }
        
        static Task<HttpResponseMessage> SendMessage(HttpClient client, string path, HttpMethod method, object input, Dictionary<string,string> headers = null)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add("Accept","application/json");
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(input);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if(headers != null)
                foreach(var pair in headers)
                    request.Headers.Add(pair.Key,pair.Value);
            return client.SendAsync(request);
        }
        static HttpClient CreateClient(GithubRepo repo) => new HttpClient
        {
            BaseAddress = new Uri($"https://management.azure.com/subscriptions/{repo.AzureData.Subscription}/"),
            DefaultRequestHeaders = {
                Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", repo.AzureData.AzureToken)
            }
        };
    }
}