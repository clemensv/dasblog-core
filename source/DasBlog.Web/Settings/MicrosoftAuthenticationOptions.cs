using System.ComponentModel.DataAnnotations;

namespace DasBlog.Web.Settings
{
	public sealed class MicrosoftAuthenticationOptions
	{
		public const string SectionName = "MicrosoftAuthentication";

		[Required]
		public string TenantId { get; set; }

		[Required]
		public string ClientId { get; set; }

		[Required]
		public string ClientSecret { get; set; }
	}

	public static class MicrosoftAuthenticationPolicy
	{
		public const string TenantId = "7b95e31b-8675-4ce7-9dce-e5ee14ca323c";
		public const string ClientId = "4e97287b-77c6-4104-b406-9fb682173d7f";
		public const string AllowedObjectId = "b6249109-e1a4-42d5-ab31-eb882e3be801";
		public const string LocalUserEmail = "clemens@vasters.com";
	}

	public static class MicrosoftAuthenticationDefaults
	{
		public const string Scheme = "Microsoft";
	}
}