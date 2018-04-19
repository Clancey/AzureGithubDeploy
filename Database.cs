
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Linq;

namespace Microsoft.AzureGithub
{
    class Database
    {
        static readonly string endpointUrl = Environment.GetEnvironmentVariable("Endpoint");
        static readonly string authorizationKey = Environment.GetEnvironmentVariable("AuthKey");

        static readonly string databaseId = Environment.GetEnvironmentVariable("DatabaseId");
        static readonly string collectionId = Environment.GetEnvironmentVariable("CollectionId");
        static readonly string authCollectionId = Environment.GetEnvironmentVariable("AuthCollectionId");
        static readonly string pairingCollectionId = Environment.GetEnvironmentVariable("PairingCollectionId");
        
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

        public static async Task<PairingRequest> GetOrCreatePairingRequestByRepoId(string id)
        {
            var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, pairingCollectionId);
            using (var client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
            {
                var request = client.CreateDocumentQuery<PairingRequest>(collectionLink).Where(x => x.RepoId == id).AsEnumerable().FirstOrDefault();
                if (request != null)
                    return request;
                
                request = new PairingRequest
                {
                    RepoId = id,
                    Id = Guid.NewGuid().ToString(),
                };
                var doc = await client.CreateDocumentAsync(collectionLink, request);
                return request;
            }
        }

        //  public static async Task<PairingRequest> GetOrCreatePairingRequestByRepoId(string id)
        // {
        //     var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, authCollectionId);
        //     using (var client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
        //     {
        //         PairingRequest request;
        //         try
        //         {
        //             request = client.CreateDocumentQuery<PairingRequest>(collectionLink).FirstOrDefault(x => x.RepoId == id);
        //             if (request != null)
        //                 return request;
        //         }
        //         catch (Exception)
        //         {

        //         }
        //         request = new PairingRequest
        //         {
        //             RepoId = id,
        //             Id = Guid.NewGuid().ToString(),
        //         };
        //         var doc = await client.CreateDocumentAsync(collectionLink, request);
        //         return request;
        //     }
        // }

        public static async Task<PairingRequest> GetPairingRequest(string id)
        {
            using (var client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
            {
                return (PairingRequest)(await client.ReadDocumentAsync<PairingRequest>(UriFactory.CreateDocumentUri(databaseId, pairingCollectionId, id)));
            }
        }
    }

}