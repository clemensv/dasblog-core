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
using System.Text.Json.Serialization;
using newtelligence.DasBlog.Runtime;

namespace DasBlog.Services.Eventing
{
	public class EntryCloudEventData
	{
		[JsonPropertyName("id")]
		public string Id { get; internal set; }
		[JsonPropertyName("title")]
		public string Title { get; internal set; }
		[JsonPropertyName("createdUtc")]
		public DateTime CreatedUtc { get; internal set; }
		[JsonPropertyName("modifiedUtc")]
		public DateTime ModifiedUtc { get; internal set; }
		[JsonPropertyName("tags")]
		public string Tags { get; internal set; }
		[JsonPropertyName("description")]
		public string Description { get; internal set; }
		[JsonPropertyName("permaLink")]
		public string PermaLink { get; internal set; }
		[JsonPropertyName("detailsLink")]
		public string DetailsLink { get; internal set; }
		[JsonPropertyName("isPublic")]
		public bool IsPublic { get; internal set; }
		[JsonPropertyName("author")]
		public string Author { get; internal set; }
		[JsonPropertyName("longitude")]
		public double? Longitude { get; internal set; }
		[JsonPropertyName("latitude")]
		public double? Latitude { get; internal set; }
	}

	public class EntryCommentCloudEventData
	{
		[JsonPropertyName("id")]
		public string PostId { get; internal set; }
		[JsonPropertyName("postTitle")]
		public string PostTitle { get; internal set; }
		[JsonPropertyName("commentId")]
		public string CommentId { get; internal set; }
		[JsonPropertyName("content")]
		public string Content { get; internal set; }
		[JsonPropertyName("createdUtc")]
		public DateTime CreatedUtc { get; internal set; }
		[JsonPropertyName("modifiedUtc")]
		public DateTime ModifiedUtc { get; internal set; }
		[JsonPropertyName("permaLink")]
		public string PermaLink { get; internal set; }
		[JsonPropertyName("postDetailsLink")]
		public string PostDetailsLink { get; internal set; }
		[JsonPropertyName("isPublic")]
		public bool IsPublic { get; internal set; }
		[JsonPropertyName("author")]
		public string Author { get; internal set; }
		[JsonPropertyName("spamState")]
		public SpamState SpamState { get; internal set; }
		[JsonPropertyName("referer")]
		public string Referer { get; internal set; }
	}
}
