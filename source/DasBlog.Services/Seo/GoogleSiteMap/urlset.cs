using System;
using System.Xml.Serialization;

namespace DasBlog.Services.Seo.GoogleSiteMap
{
	/// <remarks/>
	[System.Xml.Serialization.XmlTypeAttribute(TypeName="urlset", Namespace = "http://www.google.com/schemas/sitemap/0.84")]
	[System.Xml.Serialization.XmlRootAttribute(ElementName="urlset", Namespace = "http://www.google.com/schemas/sitemap/0.84", IsNullable = false)]
	public class UrlSet
	{

		public UrlSet() { }

		/// <remarks/>
		[System.Xml.Serialization.XmlElementAttribute("url")]
		public urlCollection url;
	}
}
