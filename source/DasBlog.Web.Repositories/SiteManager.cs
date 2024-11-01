﻿using DasBlog.Core.Services.GoogleSiteMap;
using DasBlog.Managers.Interfaces;
using DasBlog.Services;
using newtelligence.DasBlog.Runtime;
using System;
using System.IO;

namespace DasBlog.Managers
{
    public class SiteManager : ISiteManager
    {
        private readonly IBlogDataService dataService;
        private readonly ILoggingDataService loggingDataService;
        private readonly IDasBlogSettings dasBlogSettings;

        public SiteManager(IDasBlogSettings settings)
        {
            dasBlogSettings = settings;

			loggingDataService = LoggingDataServiceFactory.GetService(Path.Combine(dasBlogSettings.WebRootDirectory, dasBlogSettings.SiteConfiguration.LogDir));
			dataService = BlogDataServiceFactory.GetService(Path.Combine(dasBlogSettings.WebRootDirectory, dasBlogSettings.SiteConfiguration.ContentDir), dasBlogSettings.RelativeToRoot, loggingDataService);
		}

        public UrlSet GetGoogleSiteMap()
        {
            var root = new UrlSet();
            root.url = new urlCollection();

            //Default first...
            var basePage = new Url(dasBlogSettings.GetBaseUrl(), DateTime.UtcNow, ChangeFreq.daily, 1.0M);
            root.url.Add(basePage);

            var archivePage = new Url(dasBlogSettings.RelativeToRoot("archive"), DateTime.UtcNow, ChangeFreq.daily, 1.0M);
            root.url.Add(archivePage);

			var categorpage = new Url(dasBlogSettings.RelativeToRoot("category"), DateTime.UtcNow, ChangeFreq.daily, 1.0M);
			root.url.Add(categorpage);

			//All Pages
			var entryCache = dataService.GetEntries(false);
            foreach (var e in entryCache)
            {
                if (e.IsPublic)
                {
                    //Start with a RARE change freq...newer posts are more likely to change more often.
                    // The older a post, the less likely it is to change...
                    var freq = ChangeFreq.daily;

                    //new stuff?
                    if (e.CreatedUtc < DateTime.UtcNow.AddMonths(-9))
                    {
                        freq = ChangeFreq.yearly;
                    }
                    else if (e.CreatedUtc < DateTime.UtcNow.AddDays(-30))
                    {
                        freq = ChangeFreq.monthly;
                    }
                    else if (e.CreatedUtc < DateTime.UtcNow.AddDays(-7))
                    {
                        freq = ChangeFreq.weekly;
                    }
                    if (e.CreatedUtc > DateTime.UtcNow.AddDays(-2))
                    {
                        freq = ChangeFreq.hourly;
                    }

                    //Add comments pages, since comments have indexable content...
                    // Only add comments if we aren't showing comments on permalink pages already
                    if (dasBlogSettings.SiteConfiguration.ShowCommentsWhenViewingEntry == false)
                    {
                        var commentPage = new Url(dasBlogSettings.GetCommentViewUrl(e.CompressedTitle), e.CreatedUtc, freq, 0.7M);
                        root.url.Add(commentPage);
                    }

                    //then add permalinks
                    var permaPage = new Url(dasBlogSettings.RelativeToRoot(dasBlogSettings.GeneratePostUrl(e)), e.CreatedUtc, freq, 0.9M);
                    root.url.Add(permaPage);
                }
            }

            //All Categories
            var catCache = dataService.GetCategories();
            foreach (var cce in catCache)
            {
                if (cce.IsPublic)
                {
					var catname = Entry.InternalCompressTitle(cce.Name, dasBlogSettings.SiteConfiguration.TitlePermalinkSpaceReplacement).ToLower();
					var caturl = new Url(dasBlogSettings.GetCategoryViewUrl(catname), DateTime.UtcNow, ChangeFreq.weekly, 0.6M);
                    root.url.Add(caturl);
                }
            }

            return root;
        }
    }
}

