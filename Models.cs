using System.Collections.Generic;
using Microsoft.Azure.Documents;
using System;

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


    public class Account : Resource
    {
		public string IdToken { get; set; }

        public string Subscription { get; set; }

		public string Token { get; set; }

		public string RefreshToken { get; set; }

		public long ExpiresIn { get; set; }
		//UTC Datetime created
		public DateTime Created { get; set; }

        public bool IsValid()
	    {
			if (string.IsNullOrWhiteSpace(Token))
				return false;
			// This allows you to specify -1 for never expires
		    if (ExpiresIn <= 0)
			    return true;
			if(string.IsNullOrWhiteSpace(RefreshToken))
				return false;
            // for simplicity sake, just expire it a 5 min early
			var expireTime = Created.AddSeconds(ExpiresIn - 300);
			return expireTime > DateTime.UtcNow;
		}
    }

    public class PairingRequest : Resource
    {
        public string RepoId { get; set; }
    }
}