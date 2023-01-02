using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Server.IIS.Core;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;

namespace DasBlog.Web.Models.BlogViewModels
{
	public partial class PostViewModel
	{

		[Required]
		[MinLength(1)]
		public string Title { get; set; }

		[DataType(DataType.MultilineText)]
		public string Content { get; set; }

		[DataType(DataType.MultilineText)]
		public string Description { get; set; }

		public string Author { get; set; }

		public string PermaLink { get; set; }

		public string EntryId { get; set; }

		// categories associated with this blog post
		public IList<CategoryViewModel> Categories { get; set; } = new List<CategoryViewModel>();

		// all categories currently available on this blog
		public IList<CategoryViewModel> AllCategories { get; set; } = new List<CategoryViewModel>();

		public string NewCategory { get; set; }

		[Display(Name = "Allow Comments")]
		public bool AllowComments { get; set; }

		[Display(Name = "Is Public")]
		public bool IsPublic { get; set; }

		public bool Syndicated { get; set; }

		[Display(Name = "Date Created")]
		public DateTime CreatedDateTime { get; set; }

		[Display(Name = "Date Modified")]
		public DateTime ModifiedDateTime { get; set; }

		public ListCommentsViewModel Comments { get; set; }

		public IFormFile Image { get; set; }
		public string Language { get; set; }

		public IEnumerable<SelectListItem> Languages { get; set; } = new List<SelectListItem>();

		public string ImageUrl { get; set; } = string.Empty;

		public string VideoUrl { get; set; } = string.Empty;

		public int Order { get; set; } = 0;


		private static ConcurrentDictionary<string, WebManifest.WebManifest> manifests = new ConcurrentDictionary<string, WebManifest.WebManifest>();
		private static ConcurrentDictionary<string, string> openGraphEmbeddings = new ConcurrentDictionary<string, string>();
		private static ConcurrentDictionary<string, OEmbed> oEmbedEmbeddings = new ConcurrentDictionary<string, OEmbed>();

		internal void InjectCategoryLinks(IDasBlogSettings dasBlogSettings)
		{
			Content = Regex.Replace(Content, @"(?<!=['""])#(\w+)", $"<a href=\"{dasBlogSettings.RelativeToRoot("category/")}$1\">#$1 </a>");
		}
		public List<string> ErrorMessages { get; set; }
		}

		internal void InjectEmbeddedTweets(IDasBlogSettings dasBlogSettings)
		{
			HttpClient httpClient = new HttpClient();
			// be on Edge ;) 
			httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0 (Linux; Android 6.0; Nexus 5 Build/MRA58N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Mobile Safari/537.36 Edg/108.0.1462.54");
			InjectOEmbeddingsForExpression(dasBlogSettings, httpClient);
		}

		public static bool SupportedUri(string pattern, string text)
		{
			pattern = pattern.Replace(".", @"\.").Replace("*", ".*");
			return Regex.IsMatch(text, pattern);
		}

		static XmlSerializer xsoe = new XmlSerializer(typeof(OEmbed));


		protected string GetOpenGraphEmbedding(HttpClient client, string url)
		{
			if (!openGraphEmbeddings.TryGetValue(url, out var embedding))
			{
				try
				{
					// Use HttpClient to send a GET request to the page
					HttpResponseMessage response = client.GetAsync(url).Result;
					if (response.IsSuccessStatusCode && response.Content.Headers.GetValues("Content-Type").FirstOrDefault().StartsWith("text/html"))
					{
						string html = response.Content.ReadAsStringAsync().Result;

						// Use HtmlAgilityPack to parse the HTML content
						HtmlDocument doc = new HtmlDocument();
						doc.LoadHtml(html);

						// create the site title
						string siteTitle = response.RequestMessage.RequestUri.Host;
						string[] parts = siteTitle.Split('.');
						if (parts.Length > 2)
						{
							siteTitle = string.Join(".", parts.Skip(parts.Length - 2));
						}

						// get the web site manifest
						WebManifest.WebManifest webManifest = null;
						if (!manifests.TryGetValue(response.RequestMessage.RequestUri.Host, out webManifest))
						{
							string manifestUrl = doc.DocumentNode.SelectSingleNode("//link[@rel='manifest']")?.Attributes["href"]?.Value;
							if (!string.IsNullOrEmpty(manifestUrl))
							{
								manifestUrl = new Uri(response.RequestMessage.RequestUri, manifestUrl).AbsoluteUri;
								webManifest = GetWebManifest(client, manifestUrl);
							}
							// we are also adding the manifest if it's NULL so that we don't do this request again
							manifests.TryAdd(response.RequestMessage.RequestUri.Host, webManifest);
						}

						if (webManifest != null && !string.IsNullOrEmpty(webManifest.Name))
						{
							siteTitle = webManifest.Name;
						}

						if (webManifest?.Icons?.Count() > 0)
						{
							ManifestImageResource icon = webManifest.Icons?.FirstOrDefault();
							if (icon != null && !string.IsNullOrEmpty(icon.Src))
							{
								siteTitle = $"<img class=\"opengraph-preview-site-icon\" src=\"{new Uri(response.RequestMessage.RequestUri, icon.Src).AbsoluteUri}\"></img> {siteTitle}";
							}
						}
						else
						{

							string icon = doc.DocumentNode.SelectSingleNode("//link[translate(@rel, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='icon']")?.Attributes["href"]?.Value;
							if (string.IsNullOrEmpty(icon))
							{
								icon = doc.DocumentNode.SelectSingleNode("//link[translate(@rel, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='alternate icon']")?.Attributes["href"]?.Value;
							}
							if (string.IsNullOrEmpty(icon))
							{
								icon = doc.DocumentNode.SelectSingleNode("//link[translate(@rel, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='shortcut icon']")?.Attributes["href"]?.Value;
							}
							if (!string.IsNullOrEmpty(icon))
							{
								siteTitle = $"<img class=\"opengraph-preview-site-icon\" src=\"{new Uri(response.RequestMessage.RequestUri, icon).AbsoluteUri}\"></img> {siteTitle}";
							}
						}

						// Extract the values of the og:title, og:description, and og:image <meta> tags
						string title = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.Attributes["content"]?.Value;
						string description = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']")?.Attributes["content"]?.Value;
						string imageUrl = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.Attributes["content"]?.Value;
						if (string.IsNullOrEmpty(title))
						{
							title = doc.DocumentNode.SelectSingleNode("//meta[@property='twitter:title']")?.Attributes["content"]?.Value;
						}
						if (string.IsNullOrEmpty(description))
						{
							title = doc.DocumentNode.SelectSingleNode("//meta[@property='twitter:description']")?.Attributes["content"]?.Value;
						}
						if (string.IsNullOrEmpty(imageUrl))
						{
							title = doc.DocumentNode.SelectSingleNode("//meta[@property='twitter:imageUrl']")?.Attributes["content"]?.Value;
						}

						if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(description) && string.IsNullOrEmpty(imageUrl))
						{
							openGraphEmbeddings.TryAdd(url, null);
							return null;
						}

						// Build the HTML code for the OpenGraph preview
						string htmlResult = $"<a class=\"opengraph-preview-link\" href=\"{url}\"><div class=\"opengraph-preview\">";
						if (!string.IsNullOrEmpty(siteTitle))
						{
							htmlResult += $"<div class=\"opengraph-preview-site\">{siteTitle}</div>";
						}
						if (!string.IsNullOrEmpty(imageUrl))
						{
							htmlResult += $"<img class=\"opengraph-preview-image\" src=\"{imageUrl}\"/>";
						}
						htmlResult += "<div class=\"opengraph-preview-info\">";
						if (!string.IsNullOrEmpty(title))
						{
							htmlResult += $"<h3 class=\"opengraph-preview-title\">{title}</h3>";
						}
						if (!string.IsNullOrEmpty(description))
						{
							htmlResult += $"<p class=\"opengraph-preview-description\">{description}</p>";
						}
						htmlResult += "</div></div></a>";
						openGraphEmbeddings.TryAdd(url, htmlResult);
						return htmlResult;
					}
					else
					{
						openGraphEmbeddings.TryAdd(url, null);
						return null;
					}
				}
				catch (Exception ex)
				{
					// TODO log it 
					// for now just swallow it
					openGraphEmbeddings.TryAdd(url, null);
					return null;
				}
			}
			else
			{
				return embedding;
			}
		}

		protected WebManifest.WebManifest GetWebManifest(HttpClient client, string url)
		{
			try
			{
				var manifestResult = client.GetAsync(url).GetAwaiter().GetResult();
				if (manifestResult != null && manifestResult.IsSuccessStatusCode &&
					(manifestResult.Content.Headers.ContentType.MediaType.Contains("+json") ||
					 manifestResult.Content.Headers.ContentType.MediaType.Contains("/json")))
				{
					return WebManifest.WebManifest.FromJson(manifestResult.Content.ReadAsStringAsync().GetAwaiter().GetResult());
				}
			}
			catch
			{
				// TODO log it 
				// for now just swallow it
			}
			return null;
		}

		private void InjectOEmbeddingsForExpression(IDasBlogSettings dasBlogSettings, HttpClient httpClient)
		{

			var linkPattern = @"(?:(<a\s+([^>]+)?href=['""](?'url'(?:http(s)?://)\S+)['""]([^>]+)?>\k'url'<\/a>)|(?<![\+&])(?<!=['""])(?<!<[aA][^>]+>)(?'url'(?:http(s)?://)[^<\s]*))";
			Match match = Regex.Match(Content, linkPattern);
			int stringOffset = 0;
			if (match.Success)
			{
				do
				{
					bool linkReplaced = false;
					string rest = Content.Substring(stringOffset + match.Index + match.Length);
					var link = match.Groups["url"].Value;
					var matchOffset = match.Length;

					if (!oEmbedEmbeddings.TryGetValue(link, out var embedding))
					{
						OEmbedEndpoint endpoint = null;
						OEmbedProvider provider = null;
						var prvs = dasBlogSettings.OEmbedProviders.Providers.ToList();
						// sort reversely by the tracked UsageCount in the provider object
						prvs.Sort();
						foreach (var prv in prvs)
						{
							if (prv.Endpoints != null) foreach (var ep in prv.Endpoints)
								{
									if (ep.Schemes != null) foreach (var scheme in ep.Schemes)
										{
											if (SupportedUri(scheme, link))
											{
												provider = prv;
												endpoint = ep;
												break;
											}
										}
								}
						}

						if (endpoint != null && endpoint.Url != null)
						{
							provider.UsageCount++;
							try
							{
								lock (provider)
								{
									// wait? didn't we check this above? Yes, but that was quite while ago
									// we now found the provider that matches and just blocked on it and 
									// will now look whether a parallel thread managed to already fetch the data
									// before us
									if (!oEmbedEmbeddings.TryGetValue(link, out embedding))
									{
										var endpointUrl = endpoint.Url.Replace("{format}", "json");
										var result = httpClient.GetAsync(new Uri($"{endpointUrl}?url={link}")).GetAwaiter().GetResult();
										if (result.StatusCode == System.Net.HttpStatusCode.OK)
										{
											if (result.Content.Headers.GetValues("Content-Type").FirstOrDefault().StartsWith("application/json"))
											{
												embedding = JsonConvert.DeserializeObject<OEmbed>(result.Content.ReadAsStringAsync().GetAwaiter().GetResult());
											}
											else if (result.Content.Headers.GetValues("Content-Type").FirstOrDefault().StartsWith("text/xml"))
											{
												embedding = (OEmbed)xsoe.Deserialize(result.Content.ReadAsStream());
											}
										}
										else if (result.StatusCode == System.Net.HttpStatusCode.NotFound ||
												  result.StatusCode == System.Net.HttpStatusCode.Forbidden)
										{
											// remove the endpoint for this instance
											endpoint.Url = null;
										}
									}
								}
							}
							catch (Exception ex)
							{
								// TODO: logging, even though not critical
								// keep going, but kill the endpoint
								endpoint.Url = null;
							}
						}
						oEmbedEmbeddings.TryAdd(link, embedding);
					}
					
					if (embedding != null)
					{
						Content = Content.Substring(0, stringOffset + match.Index) + embedding.Html + rest;
						matchOffset = embedding.Html.Length;
						linkReplaced = true;
					}

					if (!linkReplaced)
					{
						var html = GetOpenGraphEmbedding(httpClient, link);
						if (!string.IsNullOrEmpty(html))
						{
							Content = Content.Substring(0, stringOffset + match.Index) + html + rest;
							matchOffset = html.Length;
						}
					}

					stringOffset = stringOffset + match.Index + matchOffset;
					match = Regex.Match(rest, linkPattern);
				} while (match.Success);
			}
		}

		internal void ReplaceBareLinksWithIcons(IDasBlogSettings dasBlogSettings)
		{
			// This replaces the link with a link icon if the href and the inner text are identical
			Content = Regex.Replace(Content, @"(<a\s+([^>]+)?href=['""](\S+)['""]([^>]+)?>\3<\/a>)", "<a $2 href='$3' $4>&#x1F517;</a>");
			// This replaces "naked" links outside of HTML attributes and as text of <a> tags
			Content = Regex.Replace(Content, @"(?<![\+&])(?<!=['""])(?<!<[aA][^>]+>)((?:http(s)?://)[^<\s]*)", @"<a href=""$1"">&#x1F517;</a>");

		}
	}
}
