using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using System.Net;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.AzureGithub
{
    class AzureApi
    {
        static readonly string azureTennant = Environment.GetEnvironmentVariable("AzureTennant");
        public static async Task<(string username, string password)> GetAzureDeployCredentials(GithubRepo repo, Build build)
        {
            using (var client = await CreateClient(repo))
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
            using (var client = await CreateClient(repo))
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
            var name = string.IsNullOrWhiteSpace(build.AzureAppId) ? $"{repo.Id}-Pull{build.PullRequestId}-{Guid.NewGuid().ToString().Substring(0, 8)}" : build.AzureAppId.Replace("_", "-");
            using (var client = await CreateClient(repo))
            {
                var path = $"resourceGroups/{repo.AzureData.ResourceGroup}/providers/Microsoft.Web/serverfarms/{name}?api-version=2016-09-01";
                var input = new
                {
                    name = name,
                    location = repo.AzureData.Location,
                    properties = new
                    {
                        name = name,
                        perSiteScaling = false
                    },
                    sku = new
                    {
                        name = "F1",
                        tier = "Free",
                        capacity = 1
                    }
                };

                var resp = await SendMessage(client, path, HttpMethod.Put, input, new Dictionary<string, string> { ["CommandName"] = "appservice plan create" });
                var data = await resp.Content.ReadAsStringAsync();
                resp.EnsureSuccessStatusCode();
                build.AzureAppId = name;
                return name;
            }
        }
        public static async Task<string> CreateWebApp(GithubRepo repo, Build build)
        {
            using (var client = await CreateClient(repo))
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
                build.IsActive = true;
                var data = await resp.Content.ReadAsStringAsync();
                resp.EnsureSuccessStatusCode();
                return build.AzureAppId;
            }
        }

        public static async Task<bool> DeleteAppService(GithubRepo repo, Build build)
        {
            var name = string.IsNullOrWhiteSpace(build.AzureAppId) ? $"{repo.Id}-Pull{build.PullRequestId}-{Guid.NewGuid().ToString().Substring(0, 8)}" : build.AzureAppId.Replace("_", "-");
            using (var client = await CreateClient(repo))
            {
                var path = $"resourceGroups/{repo.AzureData.ResourceGroup}/providers/Microsoft.Web/serverfarms/{name}?api-version=2016-09-01";
                var resp = await SendMessage(client, path, HttpMethod.Delete, null);
                var data = await resp.Content.ReadAsStringAsync();
                resp.EnsureSuccessStatusCode();
                build.AzureAppId = name;
                return true;
            }
        }
        public static async Task<bool> DeleteWebApp(GithubRepo repo, Build build)
        {
            using (var client = await CreateClient(repo))
            {
                var path = $"resourceGroups/{repo.AzureData.ResourceGroup}/providers/Microsoft.Web/sites/{build.AzureAppId}?api-version=2016-08-01";
                var resp = await SendMessage(client, path, HttpMethod.Delete, null);
                var data = await resp.Content.ReadAsStringAsync();
                resp.EnsureSuccessStatusCode();
                build.IsActive = false;
                return true;
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
                var resp = await SendMessage(client, "settings", HttpMethod.Post, new
                {
                    key = "branch",
                    value = build.Branch,
                });

                var data = await resp.Content.ReadAsStringAsync();
                resp.EnsureSuccessStatusCode();

                var url = $"https://{build.AzureAppId}.scm.azurewebsites.net/deploy";
                var cloneUrl = await FormatRepoCloneUrl(repo, build);
                var payload = new
                {
                    format = "basic",
                    url = cloneUrl
                };
                resp = await SendMessage(client, url, HttpMethod.Post, payload);
                data = await resp.Content.ReadAsStringAsync();
                return resp.IsSuccessStatusCode;
            }
        }

        static async Task<string> FormatRepoCloneUrl(GithubRepo repo, Build build)
        {
            var url = $"{repo.CloneUrl}#{build.CommitHash}";

            if (repo.IsPrivate)
            {
                await GithubApi.Authenticate(repo);
                var uri = new Uri(url);
                url = $"{uri.Host}://{repo.GithubAccount.Token}@{uri.Host}/{uri.PathAndQuery}";
            }
            return url;
        }

        static Task<HttpResponseMessage> SendMessage(HttpClient client, string path, HttpMethod method, object input, Dictionary<string, string> headers = null)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add("Accept", "application/json");
            if (input != null)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(input);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            if (headers != null)
                foreach (var pair in headers)
                    request.Headers.Add(pair.Key, pair.Value);
            return client.SendAsync(request);
        }
        // static HttpClient CreateClient(GithubRepo repo) => new HttpClient
        // {
        //     BaseAddress = new Uri($"https://management.azure.com/subscriptions/{repo.AzureData.Subscription}/"),
        //     DefaultRequestHeaders = {
        //         Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(repo.AzureAccount.TokenType, repo.AzureAccount.Token)
        //     }
        // };

        static async Task<HttpClient> CreateClient(GithubRepo repo, bool authenticate = true)
        {
            if (authenticate)
                await Authenticate(repo);
            return new HttpClient
            {
                BaseAddress = new Uri($"https://management.azure.com/subscriptions/{repo.AzureData.Subscription}/"),
                DefaultRequestHeaders = {
                    Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(repo.AzureAccount.TokenType, repo.AzureAccount.Token)
                }
            };
        }

        static string clientId => Environment.GetEnvironmentVariable("AzureClientId");

        static string clientSecret => Environment.GetEnvironmentVariable("AzureClientSecret");

        public static string AuthUrl(string id, string redirectUrl) => $"https://login.microsoftonline.com/{azureTennant}/oauth2/authorize?client_id={clientId}&response_type=code&redirect_uri={redirectUrl}&resource=https%3a%2f%2fmanagement.azure.com%2f&state={id}";
        public static async Task<bool> Authenticate(GithubRepo repo)
        {
            if (repo.AzureAccount.IsValid())
                return true;
            try
            {
                var postData = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = repo.AzureAccount.RefreshToken,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                };

                var message = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
                {
                    Content = new FormUrlEncodedContent(postData),
                    Headers = {
                        {"Accept","application/json"}
                    }
                };
                using (var client = await CreateClient(repo, false))
                {
                    var reply = await client.SendAsync(message);
                    var resp = await reply.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<OauthResponse>(resp);
                    if (!string.IsNullOrEmpty(result.RefreshToken))
                        repo.AzureAccount.RefreshToken = result.RefreshToken;
                    repo.AzureAccount.TokenType = result.TokenType;
                    repo.AzureAccount.Token = result.AccessToken;
                    repo.AzureAccount.ExpiresIn = result.ExpiresIn;
                    repo.AzureAccount.Created = DateTime.UtcNow;
                }
                await Database.Save(repo);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        static string TokenUrl => $"https://login.microsoftonline.com/{azureTennant}/oauth2/token";
        public static async Task<bool> Authenticate(GithubRepo repo, string code, string redirect)
        {
            var authUrl = $"https://login.microsoftonline.com/{azureTennant}/oauth2/authorize";
            var postData = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirect
            };
            var message = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(postData),
                Headers = {
                    {"Accept","application/json"}
                }
            };
            using (var client = new HttpClient())
            {
                var reply = await client.SendAsync(message);
                var resp = await reply.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<OauthResponse>(resp);
                if (!string.IsNullOrEmpty(result?.Error))
                    throw new Exception($"{result.Error} : {result.ErrorDescription}");
                reply.EnsureSuccessStatusCode();

                repo.AzureAccount = new Account()
                {
                    ExpiresIn = result.ExpiresIn,
                    Created = DateTime.UtcNow,
                    RefreshToken = result.RefreshToken,
                    TokenType = result.TokenType,
                    Token = result.AccessToken,
                    IdToken = result.Id
                };
                await Database.Save(repo);
            }
            return true;
        }
    }
}