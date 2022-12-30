using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DasBlog.Core.Extensions;
using DasBlog.Services;
using newtelligence.DasBlog.Runtime;

namespace DasBlog.Web.Models.BlogViewModels
{
    public class CategoryPostItem
    {
		public string Category { get; set; }

		public string BlogTitle { get; set; }

		public string BlogId { get; set; }

		public DateTime Date { get; set; }

		public static CategoryPostItem CreateFromEntry(Entry entry, IDasBlogSettings dasBlogSettings)
		{
			return new CategoryPostItem
			{
				Category = entry.GetSplitCategories().FirstOrDefault(),
				BlogTitle = !string.IsNullOrEmpty(entry.Title) ? entry.Title :
								   (!string.IsNullOrEmpty(entry.Description) ? entry.Description.CutLongString(80) :
									  entry.Content.StripHTMLFromText().CutLongString(80)),
				BlogId = dasBlogSettings.GeneratePostUrl(entry),
				Date = entry.CreatedLocalTime
				
			};
		}
	}
}
