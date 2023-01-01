using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using DasBlog.Services;
using DasBlog.Services.ConfigFile;
using DasBlog.Services.Rss.Rss20;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Server.IIS.Core;
using Newtonsoft.Json;
using HtmlAgilityPack;

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


		public List<string> ErrorMessages { get; set; }

		internal void InjectCategoryLinks(IDasBlogSettings dasBlogSettings)
		{
			Content = Regex.Replace(Content, @"(?<!=['""])#(\w+)", $"<a href=\"{dasBlogSettings.RelativeToRoot("category/")}$1\">#$1 </a>");
		}
		public List<string> ErrorMessages { get; set; }
	}
}
