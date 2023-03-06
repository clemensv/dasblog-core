using AutoMapper;
using DasBlog.Core.Common;
using DasBlog.Managers.Interfaces;
using DasBlog.Services;
using DasBlog.Services.ActivityLogs;
using DasBlog.Web.Models.BlogViewModels;
using DasBlog.Web.Services.Interfaces;
using DasBlog.Web.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NBR = newtelligence.DasBlog.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using reCAPTCHA.AspNetCore;
using Markdig;
using DasBlog.Core.Extensions;
using System.Text.RegularExpressions;
using DasBlog.Services.Site;
using System.Security.Cryptography;
using System.Text;
using newtelligence.DasBlog.Runtime;
using EventDataItem = DasBlog.Services.ActivityLogs.EventDataItem;
using EventCodes = DasBlog.Services.ActivityLogs.EventCodes;
using AutoMapper.Internal;
using Markdig.Helpers;
using Microsoft.AspNetCore.Mvc.Routing;

namespace DasBlog.Web.Controllers
{
	[Authorize]
	public class BlogPostController : DasBlogBaseController
	{
		private readonly IBlogManager blogManager;
		private readonly ICategoryManager categoryManager;
		private readonly IHttpContextAccessor httpContextAccessor;
		private readonly IDasBlogSettings dasBlogSettings;
		private readonly IMapper mapper;
		private readonly IFileSystemBinaryManager binaryManager;
		private readonly ILogger<BlogPostController> logger;
		private readonly IBlogPostViewModelCreator modelViewCreator;
		private readonly IMemoryCache memoryCache;
		private readonly IExternalEmbeddingHandler embeddingHandler;
		private readonly IRecaptchaService recaptcha;

		
		public BlogPostController(IBlogManager blogManager, IHttpContextAccessor httpContextAccessor, IDasBlogSettings dasBlogSettings,
									IMapper mapper, ICategoryManager categoryManager, IFileSystemBinaryManager binaryManager, ILogger<BlogPostController> logger,
									IBlogPostViewModelCreator modelViewCreator, IMemoryCache memoryCache, IExternalEmbeddingHandler embeddingHandler, IRecaptchaService recaptcha)
									: base(dasBlogSettings)
		{
			this.blogManager = blogManager;
			this.categoryManager = categoryManager;
			this.httpContextAccessor = httpContextAccessor;
			this.dasBlogSettings = dasBlogSettings;
			this.mapper = mapper;
			this.binaryManager = binaryManager;
			this.logger = logger;
			this.modelViewCreator = modelViewCreator;
			this.memoryCache = memoryCache;
			this.embeddingHandler = embeddingHandler;
			this.recaptcha = recaptcha;
		}

		[AllowAnonymous]
		public IActionResult Post(string posttitle, string day, string month, string year)
		{
			Entry entry = ResolveEntryFromRequest(posttitle, day, month, year);
			if (entry != null)
			{
				var lpvm = new ListPostsViewModel();
				var pvm = mapper.Map<PostViewModel>(entry);
				pvm.Content = embeddingHandler.InjectCategoryLinksAsync(pvm.Content).GetAwaiter().GetResult();
				pvm.Content = embeddingHandler.InjectDynamicEmbeddingsAsync(pvm.Content).GetAwaiter().GetResult();
				pvm.Content = embeddingHandler.InjectIconsForBareLinksAsync(pvm.Content).GetAwaiter().GetResult();

				var lcvm = new ListCommentsViewModel
				{
					Comments = blogManager.GetComments(entry.EntryId, false)
									.Select(comment => mapper.Map<CommentViewModel>(comment)).ToList(),
					PostId = entry.EntryId,
					PostDate = entry.CreatedUtc,
					CommentUrl = dasBlogSettings.GetCommentViewUrl(posttitle),
					ShowComments = dasBlogSettings.SiteConfiguration.ShowCommentsWhenViewingEntry,
					AllowComments = entry.AllowComments
				};
				pvm.Comments = lcvm;

				if (!dasBlogSettings.SiteConfiguration.UseAspxExtension && httpContextAccessor.HttpContext.Request.Path.Value.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
				{
					return RedirectPermanent(pvm.PermaLink);
				}

				lpvm.Posts = new List<PostViewModel>() { pvm };
				return SinglePostView(lpvm);
			}
			else
			{
				// Post was not found. Let's see if it's a static page before we route user to home page.
				var sp = blogManager.GetStaticPage(posttitle);
				if(sp != null)	
				{
					var spvm = mapper.Map<StaticPageViewModel>(sp);
					return View("LoadStaticPage", spvm);

				}
				return RedirectToAction("index", "home");
			}
		}

		private Entry ResolveEntryFromRequest(string posttitle, string day, string month, string year)
		{
			Entry entry;
			// if posttitle matches D-00000000 then it is a virtual day entry
			if (string.IsNullOrEmpty(posttitle) && !string.IsNullOrEmpty(day) && !string.IsNullOrEmpty(month) && !string.IsNullOrEmpty(year))
			{
				var postDay = new DateTime(int.Parse(year), int.Parse(month), int.Parse(day));
				entry = blogManager.GetVirtualBlogPostForDay(postDay);
			}
			else if (posttitle.StartsWith("day-", StringComparison.InvariantCultureIgnoreCase) && posttitle.Substring(4).All(x => x.IsDigit()))
			{
				var postDay = DateTime.ParseExact(posttitle.Substring(4), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
				entry = blogManager.GetVirtualBlogPostForDay(postDay);
			}
			else if (Guid.TryParse(posttitle, out Guid postid))
			{
				entry = blogManager.GetBlogPostByGuid(postid);
			}
			else
			{
				var uniquelinkdate = ValidateUniquePostDate(year, month, day);
				entry = blogManager.GetBlogPost(posttitle, uniquelinkdate);
			}

			return entry;
		}

		[AllowAnonymous]
		[HttpGet("post/{postid:guid}")]
		public IActionResult PostGuid(Guid postid)
		{
			var lpvm = new ListPostsViewModel();
			var entry = blogManager.GetBlogPostByGuid(postid);
			if (entry != null)
			{
				var pvm = mapper.Map<PostViewModel>(entry);

				var lcvm = new ListCommentsViewModel
				{
					Comments = blogManager.GetComments(entry.EntryId, false)
									.Select(comment => mapper.Map<CommentViewModel>(comment)).ToList(),
					PostId = entry.EntryId,
					PostDate = entry.CreatedUtc,
					CommentUrl = dasBlogSettings.GetCommentViewUrl(entry.EntryId),
					ShowComments = dasBlogSettings.SiteConfiguration.ShowCommentsWhenViewingEntry,
					AllowComments = entry.AllowComments
				};
				pvm.Comments = lcvm;
				pvm.Content = embeddingHandler.InjectCategoryLinksAsync(pvm.Content).GetAwaiter().GetResult();
				pvm.Content = embeddingHandler.InjectIconsForBareLinksAsync(pvm.Content).GetAwaiter().GetResult();
				lpvm.Posts = new List<PostViewModel>() { pvm };

				return SinglePostView(lpvm);
			}
			else
			{
				return NotFound();
			}
		}

		[HttpGet("post/{postid:guid}/edit")]
		public IActionResult EditPost(Guid postid)
		{
			PostViewModel pvm = new PostViewModel();

			if (!string.IsNullOrEmpty(postid.ToString()))
			{
				var entry = blogManager.GetEntryForEdit(postid.ToString());
				if (entry != null)
				{
					pvm = mapper.Map<PostViewModel>(entry);
					pvm.PermaLink = dasBlogSettings.RelativeToRoot(pvm.PermaLink);
					modelViewCreator.AddAllLanguages(pvm);
					List<CategoryViewModel> allcategories = mapper.Map<List<CategoryViewModel>>(blogManager.GetCategories());

					HashSet<string> tags = new HashSet<string>();
					var matches = Regex.Matches(pvm.Content.StripHTMLFromText(), "#([a-zA-Z0-9_]+)\r\n");
					foreach (Match hashtag in matches)
					{
						tags.Add(hashtag.Groups[1].Value.ToLower());
					}

					foreach (var cat in allcategories)
					{
						if (pvm.Categories.Count(x => x.Category.Equals(cat.Category, StringComparison.InvariantCultureIgnoreCase)) > 0 ||
							tags.Contains(cat.Category.ToLower()))
						{
							cat.Checked = true;
						}						
					}

					pvm.AllCategories = allcategories;

					return View(pvm);
				}
			}

			return NotFound();
		}

		[HttpPost("post/edit")]
		public IActionResult EditPost(PostViewModel post, string submit)
		{
			// languages does not get posted as part of form
			modelViewCreator.AddAllLanguages(post);
			if (submit == Constants.BlogPostAddCategoryAction)
			{
				return HandleNewCategory(post);
			}
			if (submit == Constants.UploadImageAction)
			{
				return HandleImageUpload(post);
			}

			ValidatePostName(post);
			if (!ModelState.IsValid)
			{
				return LocalRedirect(string.Format("~/post/{0}/edit", post.EntryId));
			}

			if (!string.IsNullOrWhiteSpace(post.NewCategory))
			{
				ModelState.AddModelError(nameof(post.NewCategory),
					$"Please click 'Add' to add the category, \"{post.NewCategory}\" or clear the text before continuing");
				return LocalRedirect(string.Format("~/post/{0}/edit", post.EntryId));
			}
			try
			{
				var entry = mapper.Map<NBR.Entry>(post);
				entry.Author = httpContextAccessor.HttpContext.User.Identity.Name;
				entry.Language = "en-us"; //TODO: We inject this fron http context?
				entry.ModifiedUtc = DateTime.UtcNow;
				entry.Latitude = null;
				entry.Longitude = null;

				BreakSiteCache();

				var sts = blogManager.UpdateEntry(entry);
				if (sts == NBR.EntrySaveState.Failed)
				{
					ModelState.AddModelError("", "Failed to edit blog post. Please check Logs for more details.");
					return LocalRedirect(string.Format("~/post/{0}/edit", post.EntryId));
				}

			}
			catch (Exception ex)
			{
				logger.LogError(ex, ex.Message, null);
				ModelState.AddModelError("", "Failed to edit blog post. Please check Logs for more details.");
			}

			return LocalRedirect(string.Format("~/post/{0}/edit", post.EntryId));
		}

		[HttpGet("post/create")]
		public IActionResult CreatePost()
		{
			var post = modelViewCreator.CreateBlogPostVM();

			return View(post);
		}


		/// <summary>
		/// This method is for the companion app and allows submitting a snippet 
		/// as a blog post in form of a plain POST. The method implements a 
		/// SAS based AuthZ mechanism
		/// </summary>
		/// <param name="request"></param>
		/// <returns></returns>
		[HttpPost("submit"), AllowAnonymous]
		public IActionResult SubmitContent()
		{
			return InternalSubmitContent();
		}

		[HttpOptions("submit"), AllowAnonymous]
		public IActionResult CheckSubmitContent()
		{
			return InternalSubmitContent();
		}

		IActionResult InternalSubmitContent()
		{ 
			var request = HttpContext.Request;
			string tokenUsername = null;
			var token = request.Headers.Authorization;
			
			// check the token and extract the username from the token while we're at it
			if ( !VerifySasToken(token, new Uri(new Uri(dasBlogSettings.GetBaseUrl()), "submit").ToString(),
				(user) =>
				{
					var tokenUser = dasBlogSettings.SecurityConfiguration.Users.Where(
						(u) => { return (u.EmailAddress == user); }).FirstOrDefault();
					if (tokenUser != null)
					{
						tokenUsername = tokenUser.EmailAddress;
						return Array.ConvertAll<string, byte>(tokenUser.Password.Split('-'), s => Convert.ToByte(s, 16));
					}
					return null;
				}))
			{
				return new UnauthorizedResult();
			}

			if ( request.Method.Equals("OPTIONS", StringComparison.InvariantCultureIgnoreCase ))
			{
				HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
				HttpContext.Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
				HttpContext.Response.Headers.Add("Allow", "POST, OPTIONS");
				return Ok();				
			}

			string title = string.Empty;
			if (request.Headers.TryGetValue("Title", out var titles))
			{
				title = WebUtility.UrlDecode(titles[0]);
			}

			Entry entry = new Entry();
			entry.Initialize();
			entry.Title = title;
			entry.Author = tokenUsername;
			entry.IsPublic = true;
			entry.AllowComments = true;
			
			string tags = string.Empty;
			if (!string.IsNullOrEmpty(entry.Title))
			{
				var matches = Regex.Matches(entry.Title, "#([a-zA-Z0-9_]+)");
				foreach (Match hashtag in matches)
				{
					tags += "#" + hashtag.Groups[1].Value.ToLower() + " ";
				}
			}
			tags = tags.Trim();
			entry.Title = Regex.Replace(entry.Title, "#([a-zA-Z0-9_]+)", "").Trim();
			
			if (request.ContentType.StartsWith("image/"))
			{
				string fileName = null;
				if (request.Headers.ContainsKey("Content-Disposition"))
				{
					var contentDisposition = request.Headers["Content-Disposition"].First();
					var fileNameMatch = Regex.Match(contentDisposition, @"filename\s*=\s*(?:""(?<file>[^""]+)""|(?<file>[^;]+));?");
					if (fileNameMatch.Success)
					{
						fileName = fileNameMatch.Groups["file"].Value;
						fileName = WebUtility.UrlDecode(fileName);
					}
				}
				if (fileName == null)
				{
					var fileType = Regex.Match(request.ContentType, @"^image/(?<extension>[a-z0-9]+)").Groups["extension"].Value;
					fileName = $"media{DateTime.UtcNow.ToFileTimeUtc().ToString("x")}";
					switch (fileType)
					{
						case "jpeg": 
							fileName = fileName + ".jpg"; 
							break;
						default:
							fileName = fileName + fileType;
							break;
					}
				}

				var bufferedStream = new MemoryStream();
				request.Body.CopyToAsync(bufferedStream).GetAwaiter().GetResult();
				bufferedStream.Position = 0;
				var fullImageUrl = binaryManager.SaveFile(bufferedStream, Path.GetFileName(fileName));
				var imageUrl = dasBlogSettings.RelativeToRoot(fullImageUrl);
				entry.Attachments.Add(new Attachment(fileName, request.ContentType, 0, AttachmentType.Picture) { Url = imageUrl });

				entry.Content = string.Format("<p><img border=\"0\" src=\"{0}\"></p>", imageUrl);
			}
			else
			{
				entry.Content = (new StreamReader(request.Body).ReadToEndAsync()).GetAwaiter().GetResult();
			}

			if (tags.Length > 0)
			{
				entry.Content += $"<p>{tags}</p>";
			}

			var sts = blogManager.CreateEntry(entry);
			if (sts != NBR.EntrySaveState.Added)
			{
				return new BadRequestResult();
			}
			BreakSiteCache();
			return new CreatedResult(string.Format("~/post/{0}", entry.EntryId), null);
		}

		private bool VerifySasToken(string sasToken, string uri, Func<string, byte[]> getKeyForUserName)
		{
			var parts = sasToken.Split('&')
						 .Select(p => p.Split('='))
						 .ToDictionary(p => p[0], p => p[1]);
			var keyName = parts["skn"];
			var expiry = long.Parse(parts["se"]);
			var signature = parts["sig"];
			var key = getKeyForUserName(keyName);
			if ( key == null )
				return false;
			if (DateTimeOffset.UtcNow > DateTimeOffset.FromUnixTimeSeconds(expiry))
				return false;
			var stringToSign = $"{uri}\n{expiry}";
			var hmac = new HMACSHA256(key);
			var computedSignature = Convert.ToBase64String(hmac.ComputeHash(Encoding.Unicode.GetBytes(stringToSign)));
			return WebUtility.UrlDecode(signature) == computedSignature;
		}


		[HttpPost("post/create")]
		public IActionResult CreatePost(PostViewModel post, string submit)
		{
			NBR.Entry entry = null;

			modelViewCreator.AddAllLanguages(post);
			if (submit == Constants.BlogPostAddCategoryAction)
			{
				return HandleNewCategory(post);
			}

			if (submit == Constants.UploadImageAction)
			{
				return HandleImageUpload(post);
			}

			ValidatePostName(post);
			if (!ModelState.IsValid)
			{
				return View(post);
			}
			if (!string.IsNullOrWhiteSpace(post.NewCategory))
			{
				ModelState.AddModelError(nameof(post.NewCategory),
					$"Please click 'Add' to add the category, \"{post.NewCategory}\" or clear the text before continuing");
				return View(post);
			}

			try
			{
				entry = mapper.Map<NBR.Entry>(post);

				entry.Initialize();
				entry.Author = httpContextAccessor.HttpContext.User.Identity.Name;
				entry.Language = post.Language;
				entry.Latitude = null;
				entry.Longitude = null;
				entry.CreatedUtc = entry.ModifiedUtc = dasBlogSettings.GetCreateTime(post.CreatedDateTime);

				var sts = blogManager.CreateEntry(entry);
				if (sts != NBR.EntrySaveState.Added)
				{
					post.EntryId = entry.EntryId;
					ModelState.AddModelError("", "Failed to create blog post. Please check Logs for more details.");
					return View(post);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(new EventDataItem(DasBlog.Services.ActivityLogs.EventCodes.Error, null, "Blog post create failed: {0}", ex.Message));
				ModelState.AddModelError("", "Failed to edit blog post. Please check Logs for more details.");
			}

			if (entry != null)
			{
				logger.LogInformation(new EventDataItem(DasBlog.Services.ActivityLogs.EventCodes.EntryAdded, null, "Blog post created: {0}", entry.Title));
			}

			BreakSiteCache();

			return LocalRedirect(string.Format("~/post/{0}/edit", entry.EntryId));
		}

		[HttpGet("post/{postid:guid}/delete")]
		public IActionResult DeletePost(Guid postid)
		{
			try
			{
				blogManager.DeleteEntry(postid.ToString());
			}
			catch (Exception ex)
			{
				logger.LogError(new EventDataItem(EventCodes.Error, null, "Blog post delete failed: {0} {1}", postid.ToString(), ex.Message));
				RedirectToAction("Error");
			}

			BreakSiteCache();

			return RedirectToAction("Index", "Home");
		}

		[AllowAnonymous]
		[HttpGet("{posttitle}/comments")]
		[HttpGet("{year}/{month}/{day}/{posttitle}/comments")]
		[HttpGet("{posttitle}/comments/{commentid:guid}")]
		[HttpGet("post/{posttitle}/comments/{commentid:guid}")]
		public IActionResult Comment(string posttitle, string day, string month, string year)
		{
			ListPostsViewModel lpvm = null;
			var postguid = Guid.Empty;

			Entry entry = ResolveEntryFromRequest(posttitle, day, month, year);
			if (entry != null)
			{
				lpvm = new ListPostsViewModel
				{
					Posts = new List<PostViewModel> { mapper.Map<PostViewModel>(entry) }
				};

				if (dasBlogSettings.SiteConfiguration.EnableComments)
				{
					var lcvm = new ListCommentsViewModel
					{
						Comments = blogManager.GetComments(entry.EntryId, false)
							.Select(comment => mapper.Map<CommentViewModel>(comment)).ToList(),
						PostId = entry.EntryId,
						PostDate = entry.CreatedUtc,
						CommentUrl = dasBlogSettings.GetCommentViewUrl(entry.EntryId),
						ShowComments = true,
						AllowComments = entry.AllowComments
					};

					lpvm.Posts.First().Comments = lcvm;
				}
			}

			return SinglePostView(lpvm);
		}

		public IActionResult CommentError(AddCommentViewModel comment, List<string> errors)
		{
			ListPostsViewModel lpvm = null;
			NBR.Entry entry = null;
			var postguid = Guid.Parse(comment.TargetEntryId);
			entry = blogManager.GetBlogPostByGuid(postguid);
			if (entry != null)
			{
				lpvm = new ListPostsViewModel
				{
					Posts = new List<PostViewModel> { mapper.Map<PostViewModel>(entry) }
				};

				if (dasBlogSettings.SiteConfiguration.EnableComments)
				{
					var lcvm = new ListCommentsViewModel
					{
						Comments = blogManager.GetComments(entry.EntryId, false)
							.Select(comment => mapper.Map<CommentViewModel>(comment)).ToList(),
						PostId = entry.EntryId,
						PostDate = entry.CreatedUtc,
						CommentUrl = dasBlogSettings.GetCommentViewUrl(comment.TargetEntryId),
						ShowComments = true,
						AllowComments = entry.AllowComments
					};

					if (comment != null)
						lcvm.CurrentComment = comment;
					lpvm.Posts.First().Comments = lcvm;
					if (errors != null && errors.Count > 0)
						lpvm.Posts.First().ErrorMessages = errors;
				}
			}

			return SinglePostView(lpvm);
		}



		private IActionResult Comment(string entryId)
		{
			return Comment(entryId, string.Empty, string.Empty, string.Empty);
		}

		[AllowAnonymous]
		[HttpPost("post/comments")]
		public IActionResult AddComment(AddCommentViewModel addcomment)
		{
			List<string> errors = new List<string>();

			if (!ModelState.IsValid)
			{
				errors.Add("[Some of your entries are invalid]");
			}

			if (!dasBlogSettings.SiteConfiguration.EnableComments)
			{
				errors.Add("Comments are disabled on the site.");
			}

            if(dasBlogSettings.SiteConfiguration.AllowMarkdownInComments)
            {
                var pipeline = new MarkdownPipelineBuilder().UseReferralLinks("nofollow").Build();
                addcomment.Content = Markdown.ToHtml(addcomment.Content, pipeline);
            }

			// Optional in case of Captcha. Commenting the settings in the config file 
			// Will disable this check. People will typically disable this when using captcha.
			if (!string.IsNullOrEmpty(dasBlogSettings.SiteConfiguration.CheesySpamQ) &&
				!string.IsNullOrEmpty(dasBlogSettings.SiteConfiguration.CheesySpamA) &&
				dasBlogSettings.SiteConfiguration.CheesySpamQ.Trim().Length > 0 &&
				dasBlogSettings.SiteConfiguration.CheesySpamA.Trim().Length > 0)
			{
				if (string.Compare(addcomment.CheesyQuestionAnswered, dasBlogSettings.SiteConfiguration.CheesySpamA,
					StringComparison.OrdinalIgnoreCase) != 0)
				{
					errors.Add("Answer to Spam Question is invalid. Please enter a valid answer for Spam Question and try again.");
				}
			}

			if (dasBlogSettings.SiteConfiguration.EnableCaptcha)
			{
				var recaptchaTask = recaptcha.Validate(Request);
				recaptchaTask.Wait();
				var recaptchaResult = recaptchaTask.Result;
				if ((!recaptchaResult.success || recaptchaResult.score != 0) &&
					  recaptchaResult.score < dasBlogSettings.SiteConfiguration.RecaptchaMinimumScore)
				{
					errors.Add("Unfinished Captcha. Please finish the captcha by clicking 'I'm not a robot' and try again.");
				}
			}

			if (errors.Count > 0)
			{
				return CommentError(addcomment, errors);
			}

			var commt = mapper.Map<NBR.Comment>(addcomment);
			commt.AuthorIPAddress = HttpContext.Connection.RemoteIpAddress.ToString();
			commt.AuthorUserAgent = HttpContext.Request.Headers["User-Agent"].ToString();
			commt.EntryId = Guid.NewGuid().ToString();
			commt.IsPublic = !dasBlogSettings.SiteConfiguration.CommentsRequireApproval;
			commt.CreatedUtc = commt.ModifiedUtc = DateTime.UtcNow;

			logger.LogInformation(new EventDataItem(EventCodes.CommentAdded, null, "Comment CONTENT DUMP", commt.Content));

			var state = blogManager.AddComment(addcomment.TargetEntryId, commt);

			if (state == NBR.CommentSaveState.Failed)
			{
				logger.LogError(new EventDataItem(EventCodes.CommentBlocked, null, "Failed to save comment: {0}", commt.TargetTitle));
				errors.Add("Failed to save comment.");
			}

			if (state == NBR.CommentSaveState.SiteCommentsDisabled)
			{
				logger.LogError(new EventDataItem(EventCodes.CommentBlocked, null, "Comments are closed for this post: {0}", commt.TargetTitle));
				errors.Add("Comments are closed for this post.");
			}

			if (state == NBR.CommentSaveState.PostCommentsDisabled)
			{
				logger.LogError(new EventDataItem(EventCodes.CommentBlocked, null, "Comment are currently disabled: {0}", commt.TargetTitle));
				errors.Add("Comment are currently disabled.");
			}

			if (state == NBR.CommentSaveState.NotFound)
			{
				logger.LogError(new EventDataItem(EventCodes.CommentBlocked, null, "Invalid Post Id: {0}", commt.TargetTitle));
				errors.Add("Invalid Post Id.");
			}

			if (errors.Count > 0)
			{
				return CommentError(addcomment, errors);
			}

			logger.LogInformation(new EventDataItem(EventCodes.CommentAdded, null, "Comment created on: {0}", commt.TargetTitle));
			BreakSiteCache();
			return Comment(addcomment.TargetEntryId);
		}

		[HttpDelete("post/{postid:guid}/comments/{commentid:guid}")]
		public IActionResult DeleteComment(Guid postid, Guid commentid)
		{
			var state = blogManager.DeleteComment(postid.ToString(), commentid.ToString());

			if (state == NBR.CommentSaveState.Failed)
			{
				logger.LogError(new EventDataItem(EventCodes.Error, null, "Delete comment failed: {0}", postid.ToString()));
				return StatusCode(500);
			}

			if (state == NBR.CommentSaveState.NotFound)
			{
				return NotFound();
			}

			logger.LogInformation(new EventDataItem(EventCodes.CommentDeleted, null, "Comment deleted on: {0}", postid.ToString()));

			BreakSiteCache();

			return Ok();
		}

		[HttpPatch("post/{postid:guid}/comments/{commentid:guid}")]
		public IActionResult ApproveComment(Guid postid, Guid commentid)
		{
			var state = blogManager.ApproveComment(postid.ToString(), commentid.ToString());

			if (state == NBR.CommentSaveState.Failed)
			{
				return StatusCode(500);
			}

			if (state == NBR.CommentSaveState.NotFound)
			{
				return NotFound();
			}

			logger.LogInformation(new EventDataItem(EventCodes.CommentApproved, null, "Comment approved on: {0}", postid.ToString()));

			BreakSiteCache();

			return Ok();
		}

		[AllowAnonymous]
		[HttpGet("post/category/{category}")]
		public IActionResult GetCategory(string category)
		{
			if (string.IsNullOrWhiteSpace(category))
			{
				return RedirectToAction("Index", "Home");
			}

			var lpvm = new ListPostsViewModel();
			lpvm.Posts = categoryManager.GetEntries(category, httpContextAccessor.HttpContext.Request.Headers["Accept-Language"])
								.Select(entry => mapper.Map<PostViewModel>(entry)).ToList();

			DefaultPage();

			ViewData[Constants.ShowPageControl] = false;
			return View(BLOG_PAGE, lpvm);
		}

		[AllowAnonymous]
		[HttpPost("post/search", Name = Constants.SearcherRouteName)]
		public IActionResult Search(string searchText)
		{
			if (string.IsNullOrWhiteSpace(searchText))
			{
				return RedirectToAction("Index", "Home");
			}

			var lpvm = new ListPostsViewModel();
			var entries = blogManager.SearchEntries(WebUtility.HtmlEncode(searchText), Request.Headers["Accept-Language"])?.Where(e => e.IsPublic)?.ToList();

			if (entries != null)
			{
				lpvm.Posts = entries.Select(entry => mapper.Map<PostViewModel>(entry)).ToList();
				ViewData[Constants.ShowPageControl] = false;

				logger.LogInformation(new EventDataItem(EventCodes.Search, null, "Search request: '{0}'", searchText));

				return View(BLOG_PAGE, lpvm);
			}

			return RedirectToAction("index", "home");
		}

		private IActionResult HandleNewCategory(PostViewModel post)
		{
			ModelState.ClearValidationState("");
			if (string.IsNullOrWhiteSpace(post.NewCategory))
			{
				ModelState.AddModelError(nameof(post.NewCategory),
					"To add a category you must enter some text in the box next to the 'Add' button before clicking 'Add'");
				return View(post);
			}

			var newCategory = post.NewCategory?.Trim();
			var newCategoryDisplayName = newCategory;
			var newCategoryUrl = NBR.Entry.InternalCompressTitle(newCategory);
			// Category names should not include special characters #200
			if (post.AllCategories.Any(c => c.CategoryUrl == newCategoryUrl))
			{
				ModelState.AddModelError(nameof(post.NewCategory), $"The category, {post.NewCategory}, already exists");
			}
			else
			{
				post.AllCategories.Add(new CategoryViewModel { Category = newCategoryDisplayName, CategoryUrl = newCategoryUrl, Checked = true });
				post.NewCategory = "";
				ModelState.Remove(nameof(post.NewCategory));    // ensure response refreshes page with view model's value
			}

			return View(post);
		}

		private IActionResult HandleImageUpload(PostViewModel post)
		{
			ModelState.ClearValidationState("");
			var fileName = post.Image?.FileName;
			if (string.IsNullOrEmpty(fileName))
			{
				ModelState.AddModelError(nameof(post.Image),
						$"You must select a file before clicking \"{Constants.UploadImageAction}\" to upload it");
				return View(post);
			}

			string fullimageurl = null;
			try
			{
				using (var s = post.Image.OpenReadStream())
				{
					fullimageurl = binaryManager.SaveFile(s, Path.GetFileName(fileName));
				}
			}
			catch (Exception e)
			{
				ModelState.AddModelError(nameof(post.Image), $"An error occurred while uploading image ({e.Message})");
				return View(post);
			}

			if (string.IsNullOrEmpty(fullimageurl))
			{
				ModelState.AddModelError(nameof(post.Image), "Failed to upload file - reason unknown");
				return View(post);
			}

			post.Content += string.Format("<p><img border=\"0\" src=\"{0}\"></p>", dasBlogSettings.RelativeToRoot(fullimageurl));
			ModelState.Remove(nameof(post.Content)); // ensure that model change is included in response
			return View(post);
		}

		private void ValidatePostName(PostViewModel post)
		{
			var dt = ValidatePostDate(post);
			if (!string.IsNullOrEmpty(post.Title))
			{
				var entry = blogManager.GetBlogPost(post.Title.Replace(" ", string.Empty), dt);

				if (entry != null && string.Compare(entry.EntryId, post.EntryId, true) > 0)
				{
					ModelState.AddModelError(string.Empty, "A post with this title already exists. Titles must be unique");
				}
			}
		}

		private DateTime? ValidateUniquePostDate(string year, string month, string day)
		{
			DateTime? LinkUniqueDate = null;

			if (dasBlogSettings.SiteConfiguration.EnableTitlePermaLinkUnique)
			{
				int.TryParse(string.Format("{0}{1}{2}", year, month, day), out var dayYear);

				if (dayYear > 0)
				{
					LinkUniqueDate = DateTime.ParseExact(dayYear.ToString(), "yyyyMMdd", null, DateTimeStyles.AdjustToUniversal);
				}
			}

			return LinkUniqueDate;
		}

		private DateTime? ValidatePostDate(PostViewModel postView)
		{
			if (!dasBlogSettings.SiteConfiguration.EnableTitlePermaLinkUnique)
			{
				return null;
			}

			return postView?.CreatedDateTime;
		}

		private void BreakSiteCache()
		{
			memoryCache.Remove(CACHEKEY_RSS);
			memoryCache.Remove(CACHEKEY_FRONTPAGE);
			memoryCache.Remove(CACHEKEY_ARCHIVE);
		}

	}
}
