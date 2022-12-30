using DasBlog.Managers.Interfaces;
using DasBlog.Services;
using DasBlog.Services.ActivityLogs;
using DasBlog.Services.Rss.Rss20;
using DasBlog.Services.Rss.Rsd;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using DasBlog.Core.Extensions;
using System.Text.RegularExpressions;

namespace DasBlog.Web.Controllers
{
    public class FeedController : DasBlogController
    {
        private IMemoryCache memoryCache;
        private readonly ISubscriptionManager subscriptionManager;
		private readonly IXmlRpcManager xmlRpcManager;
		private readonly IDasBlogSettings dasBlogSettings;
		private readonly ILogger<FeedController> logger;

		public FeedController(ISubscriptionManager subscriptionManager, IXmlRpcManager xmlRpcManager, 
								IMemoryCache memoryCache, IDasBlogSettings dasBlogSettings, ILogger<FeedController> logger)
        {  
            this.subscriptionManager = subscriptionManager;
			this.xmlRpcManager = xmlRpcManager;
			this.memoryCache = memoryCache;
			this.dasBlogSettings = dasBlogSettings;
			this.logger = logger;
		}

		[Produces("text/xml")]
        [HttpGet("feed/rss"), HttpHead("feed/rss")]
        public IActionResult Rss()
        {
			if (!memoryCache.TryGetValue(CACHEKEY_RSS, out RssRoot rss))
			{
				rss = subscriptionManager.GetRss();

				memoryCache.Set(CACHEKEY_RSS, rss, SiteCacheSettings());
			}

			return Ok(rss);
        }

		[Produces("text/xml")]
		[HttpGet("feed/rss/{id}"), HttpHead("feed/rss/{id}")]
		public IActionResult RssItem(string id)
		{
			if (!memoryCache.TryGetValue(CACHEKEY_RSS+id, out RssItem rssItem))
			{
				rssItem = subscriptionManager.GetRssItem(id);

				memoryCache.Set(CACHEKEY_RSS+id, rssItem, SiteCacheSettings());
			}
			return Ok(rssItem);
		}

		[Produces("application/json")]
		[HttpGet("feed/rss/{id}/json"), HttpHead("feed/rss/{id}/json")]
		public IActionResult RssItemAsJson(string id)
		{
			if (!memoryCache.TryGetValue(CACHEKEY_RSS + id, out RssItem rssItem))
			{
				rssItem = subscriptionManager.GetRssItem(id);
				memoryCache.Set(CACHEKEY_RSS + id, rssItem, SiteCacheSettings());
			}

			if ( string.IsNullOrEmpty(rssItem.Description) )
			{
				rssItem.Description = rssItem.Body;
			}
			var categoriesString = string.Join(';', (from RssCategory c in rssItem.Categories select c.Text));

			rssItem.Description = rssItem.Description.StripHTMLFromText();

			string quoteLink = "";
			// special twitter treat: if there's a twitter link in the description text, move that all the way to the end. 
			// this will only capture the first link in the text.
			string twitterLinkPattern = "((https?://)?(mobile\\.)?twitter\\.com/[^\\s]+)";
			Match twitterLinkMatch = Regex.Match(rssItem.Description, twitterLinkPattern);
			if (twitterLinkMatch.Success)
			{
				quoteLink = twitterLinkMatch.Groups[1].Value;
				rssItem.Description = rssItem.Description.Replace(quoteLink, "");
			}

			// we're doing some work here to make the description fit into 280 characters
			// when the (back-)link is taken into consideration. 
			// maxLen is 280 minus 6 characters for separators and fillers and 23 characters for a quoteLink to be appended later if present
			var maxLen = 280 - rssItem.Link.Length - rssItem.Title.Length - 6 - (string.IsNullOrEmpty(quoteLink)?0:23); 
			
			if (rssItem.Description.Length > maxLen)
			{
				Regex r = new Regex(@"[^\s\u0000-\u001F]+$");
				var truncatedString = rssItem.Description.Substring(0, maxLen).TrimEnd();
				var lastExpression = r.Match(truncatedString);
				if (lastExpression != null && !string.Equals(lastExpression.Groups[0].Value,rssItem.Description.Substring(truncatedString.Length - lastExpression.Groups[0].Value.Length)))
				{
					rssItem.Description = r.Replace(truncatedString, "").TrimEnd();
				}
				rssItem.Description += " ...";
			}

			

			return Ok(new
			{
				id = rssItem.Id,
				title = rssItem.Title,
				author = rssItem.Author,
				categories = categoriesString,
				enclosure = rssItem.Enclosure,
				description = rssItem.Description,
				link = rssItem.Link,
				pubDate= rssItem.PubDate,
				comments = rssItem.Comments,
				body = rssItem.Body, 
				quoteLink = quoteLink
			});
		}

		[Produces("text/xml")]
		[HttpGet("feed/tags/{category}/rss"), HttpHead("feed/tags/{category}/rss")]
        public IActionResult RssByCategory(string category)
        {
			if (!memoryCache.TryGetValue(CACHEKEY_RSS + "_" + category, out RssRoot rss))
			{
				rss = subscriptionManager.GetRssCategory(category);

				if (rss.Channels[0]?.Items?.Count > 0)
				{
					memoryCache.Set(CACHEKEY_RSS + "_" + category, rss, SiteCacheSettings());
				}
			}

			if(rss.Channels[0]?.Items?.Count == 0)
			{
				return NoContent();
			}

			return Ok(rss);
        }

		[Produces("text/xml")]
		[HttpGet("feed/rsd")]
        public ActionResult Rsd()
        {
            RsdRoot rsd = null;

            rsd = subscriptionManager.GetRsd();

            return Ok(rsd);
        }

		[Produces("text/xml")]
		[HttpGet("feed/blogger")]
		public ActionResult Blogger()
		{
			// https://www.poppastring.com/blog/blogger.aspx
			// Implementation of Blogger XML-RPC Api
			// blogger
			// metaWebLog
			// mt

			return NoContent();
		}

		[Produces("text/xml")]
		[HttpPost("feed/blogger")]
		public async Task<IActionResult> BloggerPost()
		{
			var blogger = string.Empty;

			try
			{
				using (var mem = new MemoryStream())
				{
					await Request.Body.CopyToAsync(mem);
					blogger = xmlRpcManager.Invoke(mem);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(new EventDataItem(EventCodes.RSS, null, "FeedController.BloggerPost Error: {0}", ex.Message));
			}

			BreakSiteCache();

			return Content(blogger);
		}

		[HttpGet("feed/pingback")]
		public ActionResult PingBack()
		{
			return Ok();
		}

		[HttpGet("feed/rss/{entryid}/comments"), HttpHead("feed/rss/{entryid}/comments")]
		public ActionResult RssComments(string entryid)
		{
			return Ok();
		}

		[HttpGet("feed/trackback/{entryid}")]
		public ActionResult TrackBack(string entryid)
		{
			return Ok();
		}

		private void BreakSiteCache()
		{
			memoryCache.Remove(CACHEKEY_RSS);
			memoryCache.Remove(CACHEKEY_FRONTPAGE);
		}
	}
}
