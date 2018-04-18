using System.Collections.Generic;
using Microsoft.Azure.Documents;

namespace Microsoft.AzureGithub
{
    class GithubEventTypes
    {
        public const string PullRequest = "pull_request";

        public const string CreateBranch = "create";

        public const string PushCode = "push";

        public const string SetupWebHook = "ping";
    }

    class GithubPullRequestActions
    {
        public const string Opened = "opened";
        public const string Updated = "synchronize";
        public const string Closed = "closed";
    }

    class GithubRepo : Resource
    {
        public string Owner { get; set; }
        public string RepoName { get; set; }
        public string CloneUrl {get;set;}
        public AzureDetails AzureData { get; set; } = new AzureDetails ();

        public string GithubToken { get; set; }

        public List<Build> Builds { get; set; } = new List<Build>();
    }

    class AzureDetails
    {
        public string Subscription { get; set; }
        public string ResourceGroup { get; set; } = "GithubDeploy";
        public string AzureToken { get; set; }
        public string Location {get;set;} = "South Central US";

        public string Sku {get;set;} = "FREE";
    }
    class Build
    {
        public int PullRequestId { get; set; }
        public string DeployedUrl { get; set; }
        public string AzureAppId { get; set; }
        public bool IsActive { get; set; }

        public string CommitHash {get;set;}

        public string StatusUrl {get;set;}

        public string Branch {get;set;}

        public string GitUrl {get;set;}
    }
}