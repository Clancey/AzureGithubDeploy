
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.AzureGithub
{
    class Database
    {
        static readonly string endpointUrl = Environment.GetEnvironmentVariable("Endpoint");
        static readonly string authorizationKey = Environment.GetEnvironmentVariable("AuthKey");

        static readonly string databaseId = Environment.GetEnvironmentVariable("DatabaseId");
        static readonly string collectionId = Environment.GetEnvironmentVariable("CollectionId");
        public static async Task<Document> CreateRepo(GithubRepo repo)
        {
            var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);
            using (var client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
            {
                return await client.CreateDocumentAsync(collectionLink, repo);
            }
        }

        public static async Task<GithubRepo> GetRepo(string id)
        {
            using (var client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
            {
                return (GithubRepo)(await client.ReadDocumentAsync<GithubRepo>(UriFactory.CreateDocumentUri(databaseId, collectionId, id)));
            }
        }

        public static async Task<GithubRepo> Save(GithubRepo repo)
        {
             var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);
            using (var client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
            {
               var resp =await client.UpsertDocumentAsync(collectionLink, repo);
               return repo;
            }
        }
        public static string CleanseName(string id) => id.Replace("/", "-").Replace("_","-");
    }

}