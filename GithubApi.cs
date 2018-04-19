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
    class GithubApi
    {
        public static async Task<bool> PostStatus(GithubRepo repo, string statuUrl, bool success, string url)
        {
            using(var client = await CreateClient(repo))
            {
                var resp = await SendMessage(client,statuUrl,HttpMethod.Post,new {
                    state = success? "succcess" : "error",
                    target_url = url,
                });
                var data =  resp.Content.ReadAsStringAsync();
                resp.EnsureSuccessStatusCode();
                return true;
            }
        }

        static Task<HttpResponseMessage> SendMessage(HttpClient client, string path, HttpMethod method, object input, Dictionary<string, string> headers = null)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add("Accept", "application/json");
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(input);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (headers != null)
                foreach (var pair in headers)
                    request.Headers.Add(pair.Key, pair.Value);
            return client.SendAsync(request);
        }
        static async Task<HttpClient> CreateClient(GithubRepo repo, bool authenticate = true)
        {
            if (authenticate)
                await Authenticate(repo);
            return new HttpClient
            {
                BaseAddress = new Uri($"https://api.github.com"),
                DefaultRequestHeaders = {
                    Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(repo.GithubAccount.TokenType, repo.GithubAccount.Token)
                }
            };
        }

        static string clientId => Environment.GetEnvironmentVariable("GithubClientId");

        static string clientSecret => Environment.GetEnvironmentVariable("GithubClientSecret");
        
        public static string AuthUrl(string id,string redirectUrl) =>$"https://github.com/login/oauth/authorize?client_id={clientId}&response_type=code&redirect_uri={redirectUrl}&scope=repo&state={id}"; 
        public static async Task<bool> Authenticate(GithubRepo repo)
        {
            if (repo.GithubAccount.IsValid())
                return true;
            try
            {
                var postData = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = repo.GithubAccount.RefreshToken,
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
                        repo.GithubAccount.RefreshToken = result.RefreshToken;
                    repo.GithubAccount.TokenType = result.TokenType;
                    repo.GithubAccount.Token = result.AccessToken;
                    repo.GithubAccount.ExpiresIn = result.ExpiresIn;
                    repo.GithubAccount.Created = DateTime.UtcNow;
                }
                await Database.Save(repo);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        static string TokenUrl => "https://github.com/login/oauth/access_token";
        public static async Task<bool> Authenticate(GithubRepo repo, string code, string redirect)
        {
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

                repo.GithubAccount = new Account()
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