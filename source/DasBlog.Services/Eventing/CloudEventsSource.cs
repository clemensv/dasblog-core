#region Copyright (c) 2023 dasBlog Authors
/*
// Copyright (c) 2023, dasBlog Authors
//  
// Redistribution and use in source and binary forms, with or without modification, are permitted 
// provided that the following conditions are met: 
//  
// (1) Redistributions of source code must retain the above copyright notice, this list of 
// conditions and the following disclaimer. 
// (2) Redistributions in binary form must reproduce the above copyright notice, this list of 
// conditions and the following disclaimer in the documentation and/or other materials 
// provided with the distribution. 
// (3) Neither the name of the newtelligence AG nor the names of its contributors may be used 
// to endorse or promote products derived from this software without specific prior 
// written permission.
//      
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS 
// OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY 
// AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR 
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL 
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER 
// IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT 
// OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// -------------------------------------------------------------------------
//
// For portions of this software, the some additional copyright notices may apply 
// which can either be found in the license.txt file included in the source distribution
// or following this notice. 
*/
#endregion


using System;
using System.Net.Http;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Http;
using CloudNative.CloudEvents.SystemTextJson;
using Microsoft.Extensions.Logging;
using newtelligence.DasBlog.Runtime;

namespace DasBlog.Services.Eventing
{
	public class CloudEventsSource : ICloudEventsSource
	{
		private readonly IDasBlogSettings dasBlogSettings;
		private readonly ILogger<ICloudEventsSource> logger;

		public CloudEventsSource(IDasBlogSettings dasBlogSettings, ILogger<ICloudEventsSource> logger)
		{
			this.dasBlogSettings = dasBlogSettings;
			this.logger = logger;
		}

		public async Task RaiseCloudEventAsync(CloudEvent cloudEvent)
		{
			if (dasBlogSettings.SiteConfiguration.EnableCloudEvents &&
				 dasBlogSettings.SiteConfiguration.CloudEventsTargets != null)
			{
				foreach (var target in dasBlogSettings.SiteConfiguration.CloudEventsTargets)
				{
					if (!string.IsNullOrEmpty(target.Uri))
					{
						try
						{
							var content = cloudEvent.ToHttpContent(ContentMode.Structured, new JsonEventFormatter());
							var uriBuilder = new UriBuilder(target.Uri);
							if (target.Headers != null)
							{
								foreach (var header in target.Headers)
								{
									if (!string.IsNullOrEmpty(header.Name))
									{
										content.Headers.Add(header.Name, header.Value);
									}
								}
							}
							if (target.QueryArgs != null)
							{
								foreach (var queryArgs in target.QueryArgs)
								{
									uriBuilder.Query = (string.IsNullOrEmpty(uriBuilder.Query) ? string.Empty : uriBuilder.Query + "&") + queryArgs.Name + "=" + queryArgs.Value;
								}
							}
							var httpClient = new HttpClient();
							var result = await httpClient.PostAsync(uriBuilder.Uri, content);
						}
						catch (HttpRequestException httpException)
						{
							logger.LogError(httpException, $"error raising CloudEvent of type {cloudEvent.Type}");
						}
						catch (Exception ex)
						{
							logger.LogError(ex, "Failed to post CloudEvent");
						}
					}
				}
			}
		}

		private EntryCloudEventData MapEntryToCloudEventData(Entry entry)
		{
			var data = new EntryCloudEventData
			{
				Id = entry.EntryId,
				Title = entry.Title,
				CreatedUtc = entry.CreatedUtc,
				ModifiedUtc = entry.ModifiedUtc,
				Tags = entry.Categories,
				Description = entry.Description,
				PermaLink = dasBlogSettings.GetPermaLinkUrl(entry.EntryId),
				DetailsLink = dasBlogSettings.GetRssEntryUrl(entry.EntryId),
				IsPublic = entry.IsPublic,
				Author = entry.Author,
				Longitude = entry.Longitude,
				Latitude = entry.Latitude
			};
			return data;
		}

		private EntryCommentCloudEventData MapCommentToCloudEventData(Entry entry, Comment comment)
		{
			var data = new EntryCommentCloudEventData
			{
				PostId = entry.EntryId,
				CommentId = comment.EntryId,
				PostTitle = entry.Title,
				CreatedUtc = entry.CreatedUtc,
				ModifiedUtc = entry.ModifiedUtc,
				Content = comment.Content,
				PermaLink = dasBlogSettings.GetEntryCommentsRssUrl(entry.EntryId) + "#" + comment.EntryId,
				PostDetailsLink = dasBlogSettings.GetRssEntryUrl(entry.EntryId),
				SpamState = comment.SpamState,
				IsPublic = comment.IsPublic,
				Referer = comment.Referer,
				Author = comment.Author ?? comment.AuthorEmail
			};
			return data;
		}

		public async Task RaisePostUpdatedCloudEventAsync(Entry entry)
		{
			if (!dasBlogSettings.SiteConfiguration.EnableCloudEvents)
				return;

			var cloudEvent = CreateCloudEventForEntry(entry);
			cloudEvent.Type = "dasblog.post.updated";
			await RaiseCloudEventAsync(cloudEvent);
		}

		public async Task RaisePostDeletedCloudEventAsync(Entry entry)
		{
			if (!dasBlogSettings.SiteConfiguration.EnableCloudEvents)
				return;

			var cloudEvent = CreateCloudEventForEntry(entry);
			cloudEvent.Type = "dasblog.post.deleted";
			await RaiseCloudEventAsync(cloudEvent);
		}

		public async Task RaisePostCreatedCloudEventAsync(Entry entry)
		{
			if (!dasBlogSettings.SiteConfiguration.EnableCloudEvents)
				return;

			var cloudEvent = CreateCloudEventForEntry(entry);
			cloudEvent.Type = "dasblog.post.created";
			await RaiseCloudEventAsync(cloudEvent);
		}

		private CloudEvent CreateCloudEventForEntry(Entry entry)
		{
			var ext = CloudEventAttribute.CreateExtension("tags", CloudEventAttributeType.String);
			var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0, new[] { ext })
			{
				Source = new Uri(dasBlogSettings.GetBaseUrl()),
				Subject = dasBlogSettings.GetPermaLinkUrl(entry.EntryId),
				Data = MapEntryToCloudEventData(entry),
				Id = Guid.NewGuid().ToString(),
				Time = DateTime.UtcNow,
			};
			if (!string.IsNullOrEmpty(entry.Categories))
			{
				cloudEvent.SetAttributeFromString("tags", entry.Categories);
			}
			return cloudEvent;
		}

		private CloudEvent CreateCloudEventForComment(Entry entry, Comment comment)
		{
			var ext = CloudEventAttribute.CreateExtension("tags", CloudEventAttributeType.String);
			var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0, new[] { ext })
			{
				Source = new Uri(dasBlogSettings.GetBaseUrl()),
				Subject = dasBlogSettings.GetEntryCommentsRssUrl(entry.EntryId) + "#" + comment.EntryId,
				Data = MapCommentToCloudEventData(entry, comment),
				Id = Guid.NewGuid().ToString(),
				Time = DateTime.UtcNow,
			};
			if (!string.IsNullOrEmpty(entry.Categories))
			{
				cloudEvent.SetAttributeFromString("tags", entry.Categories);
			}
			return cloudEvent;
		}

		public async Task RaiseCommentAddedCloudEventAsync(Entry entry, Comment comment)
		{
			if (!dasBlogSettings.SiteConfiguration.EnableCloudEvents)
				return;

			var cloudEvent = CreateCloudEventForComment(entry, comment);
			cloudEvent.Type = "dasblog.post.commentadded";
			await RaiseCloudEventAsync(cloudEvent);
		}

		public async Task RaiseCommentApprovedCloudEventAsync(Entry entry, Comment comment)
		{
			if (!dasBlogSettings.SiteConfiguration.EnableCloudEvents)
				return;

			var cloudEvent = CreateCloudEventForComment(entry, comment);
			cloudEvent.Type = "dasblog.post.commentapproved";
			await RaiseCloudEventAsync(cloudEvent);
		}

		public async Task RaiseCommentDeletedCloudEventAsync(Entry entry, Comment comment)
		{
			if (!dasBlogSettings.SiteConfiguration.EnableCloudEvents)
				return;

			var cloudEvent = CreateCloudEventForComment(entry, comment);
			cloudEvent.Type = "dasblog.post.commentdeleted";
			await RaiseCloudEventAsync(cloudEvent);
		}
	}
}
