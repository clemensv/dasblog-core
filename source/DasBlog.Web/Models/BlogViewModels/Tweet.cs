using System;
using Newtonsoft.Json;

namespace DasBlog.Web.Models.BlogViewModels
{
	public partial class PostViewModel
	{
		[Serializable]
		public class Tweet
		{
			[JsonProperty("url")]
			public string Url { get; set; }

			[JsonProperty("author_name")]
			public string AuthorName { get; set; }

			[JsonProperty("author_url")]
			public string AuthorUrl { get; set; }

			[JsonProperty("html")]
			public string Html { get; set; }

			[JsonProperty("width")]
			public int Width { get; set; }

			[JsonProperty("height")]
			public object Height { get; set; }

			[JsonProperty("type")]
			public string Type { get; set; }

			[JsonProperty("cache_age")]
			public string CacheAge { get; set; }

			[JsonProperty("provider_name")]
			public string ProviderName { get; set; }

			[JsonProperty("provider_url")]
			public string ProviderUrl { get; set; }

			[JsonProperty("version")]
			public string Version { get; set; }
		}
	}
}
