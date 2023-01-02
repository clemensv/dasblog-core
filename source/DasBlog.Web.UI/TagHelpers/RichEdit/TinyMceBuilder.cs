using DasBlog.Services;
using DasBlog.Web.Settings;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DasBlog.Web.TagHelpers.RichEdit
{
	public class TinyMceBuilder : IRichEditBuilder
	{
		private readonly IDasBlogSettings dasBlogSettings;
		private const string TINY_MCE_SERVICE_URL = "https://cdn.tiny.cloud/1/{0}/tinymce/6/tinymce.min.js";
		
		public TinyMceBuilder(IDasBlogSettings dasBlogSettings)
		{
			this.dasBlogSettings = dasBlogSettings;
		}
		
		public void ProcessControl(RichEditTagHelper tagHelper, TagHelperContext context, TagHelperOutput output)
		{
			output.TagName = "textarea";
			output.TagMode = TagMode.StartTagAndEndTag;
			output.Attributes.SetAttribute("comment", "a rich-edit-scripts element should be included on the page");
			output.Attributes.SetAttribute("id", tagHelper.Id);
			output.Attributes.SetAttribute("name", tagHelper.Name);
			output.Attributes.SetAttribute("style", "height: 100%; width: 99%; min-height: 360px");

		}

		public void ProcessScripts(RichEditScriptsTagHelper tagHelper, TagHelperContext context, TagHelperOutput output)
		{
			output.TagName = "script";
			output.TagMode = TagMode.StartTagAndEndTag;
			output.Attributes.SetAttribute("src", string.Format(TINY_MCE_SERVICE_URL, dasBlogSettings.SiteConfiguration.TinyMCEApiKey));
			output.Attributes.SetAttribute("type", "text/javascript");
			output.Attributes.SetAttribute("language", "javascript");
			string initScriptTemplate = @"
					<script>
					tinymce.init({{
						selector: '#{0}',
						plugins: 'code textpattern image link quickbars paste autosave',
                        menubar: 'edit insert view format tools',
						toolbar: 'undo redo | image link | styleselect | bold italic | alignleft aligncenter alignright alignjustify | outdent indent | fullscreen',
						relative_urls : false,
						remove_script_host : false,
						document_base_url : '" + dasBlogSettings.GetBaseUrl() + @"'
					}});
					</script>";
		string htmlContent = string.Format(initScriptTemplate, tagHelper.ControlId);
			output.PostElement.SetHtmlContent(htmlContent);
		}
	}
}
