using AutoMapper;
using DasBlog.Core.Security;
using DasBlog.Managers.Interfaces;
using DasBlog.Services;
using DasBlog.Services.ActivityLogs;
using DasBlog.Services.ConfigFile.Interfaces;
using DasBlog.Services.Users;
using DasBlog.Web.Identity;
using DasBlog.Web.Models.AccountViewModels;
using DasBlog.Web.Services;
using DasBlog.Web.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DasBlog.Web.Controllers
{
	[Authorize]
	public class AccountController : DasBlogBaseController
	{
		private const string KEY_RETURNURL = "ReturnUrl";
		private readonly ILogger<AccountController> logger;
		private readonly IMapper mapper;
		private readonly SignInManager<DasBlogUser> signInManager;
		private readonly UserManager<DasBlogUser> userManager;
		private readonly IFirstRunService firstRunService;
		private readonly IUserService userService;
		private readonly ISiteSecurityManager siteSecurityManager;
		private readonly ISiteSecurityConfig siteSecurityConfig;
		private readonly IDasBlogSettings settings;

		public AccountController(UserManager<DasBlogUser> userManager, SignInManager<DasBlogUser> signInManager,
							IMapper mapper, ILogger<AccountController> logger, IDasBlogSettings settings,
							IFirstRunService firstRunService, IUserService userService,
							ISiteSecurityManager siteSecurityManager, ISiteSecurityConfig siteSecurityConfig)
							: base(settings)
		{
			this.signInManager = signInManager;
			this.userManager = userManager;
			this.logger = logger;
			this.mapper = mapper;
			this.firstRunService = firstRunService;
			this.userService = userService;
			this.siteSecurityManager = siteSecurityManager;
			this.siteSecurityConfig = siteSecurityConfig;
			this.settings = settings;
		}

		[HttpGet]
		[AllowAnonymous]
		public IActionResult Login(string returnUrl = null)
		{
			if (firstRunService.IsSetupRequired())
			{
				return RedirectToAction(nameof(Setup));
			}

			var callbackUrl = Url.Action(nameof(MicrosoftCallback), new
			{
				returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
			});

			return Challenge(
				new AuthenticationProperties { RedirectUri = callbackUrl },
				MicrosoftAuthenticationDefaults.Scheme);
		}

		[HttpGet]
		[AllowAnonymous]
		public async Task<IActionResult> MicrosoftCallback(string returnUrl = null)
		{
			var externalIdentity = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
			var principal = externalIdentity.Principal;
			var tenantId = principal?.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid")
				?? principal?.FindFirstValue("tid");
			var objectId = principal?.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
				?? principal?.FindFirstValue("oid");

			if (!externalIdentity.Succeeded
				|| !string.Equals(tenantId, MicrosoftAuthenticationPolicy.TenantId, StringComparison.OrdinalIgnoreCase)
				|| !string.Equals(objectId, MicrosoftAuthenticationPolicy.AllowedObjectId, StringComparison.OrdinalIgnoreCase))
			{
				logger.LogWarning(new EventDataItem(EventCodes.SecurityFailure, null,
					"Rejected Microsoft identity {tenantId}/{objectId}", tenantId, objectId));
				await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
				return Forbid();
			}

			var localUser = settings.GetUserByEmail(MicrosoftAuthenticationPolicy.LocalUserEmail);
			var user = await userManager.FindByEmailAsync(MicrosoftAuthenticationPolicy.LocalUserEmail);
			if (localUser == null || !localUser.Active || user == null || !await signInManager.CanSignInAsync(user))
			{
				logger.LogWarning("The configured dasBlog user {email} is missing, inactive, or cannot sign in.", MicrosoftAuthenticationPolicy.LocalUserEmail);
				await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
				return Forbid();
			}

			await signInManager.SignInAsync(user, isPersistent: false);
			await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
			logger.LogInformation(new EventDataItem(EventCodes.SecuritySuccess, null,
				"{email} logged in with Microsoft", MicrosoftAuthenticationPolicy.LocalUserEmail));

			return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Home"));
		}

		[HttpGet]
		[AllowAnonymous]
		public IActionResult AccessDenied()
		{
			return StatusCode(StatusCodes.Status403Forbidden);
		}

		[HttpGet]
		public async Task<IActionResult> Logout()
		{
			var userName = HttpContext.User.Identity?.Name ?? "Unknown";

			await signInManager.SignOutAsync();

			logger.LogInformation(new EventDataItem(EventCodes.SecuritySuccess, null, "{email} logged out successfully", userName));

			return RedirectToAction("Index", "Home");
		}

		[HttpGet]
		public IActionResult Register(string returnUrl)
		{
			ViewData[KEY_RETURNURL] = returnUrl;

			return RedirectToAction("Index", "Home");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Register(RegisterViewModel model, string returnUrl = null)
		{
			if (ModelState.IsValid)
			{
				var user = mapper.Map<DasBlogUser>(model);
				var result = await userManager.CreateAsync(user, model.Password);
				if (!result.Succeeded)
				{
					foreach (var item in result.Errors)
					{
						ModelState.AddModelError(item.Code, item.Description);
					}
				}
			}

			return View(model);
		}

		[HttpGet]
		[AllowAnonymous]
		public IActionResult Setup()
		{
			if (!firstRunService.IsSetupRequired())
			{
				return NotFound();
			}

			DefaultPage("Setup");
			return View(new SetupViewModel());
		}

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public IActionResult Setup(SetupViewModel model)
		{
			if (!firstRunService.IsSetupRequired())
			{
				return NotFound();
			}

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var users = userService.GetAllUsers().ToList();
			var admin = users.FirstOrDefault(u => u.Role == Role.Admin);
			var originalEmail = admin?.EmailAddress ?? string.Empty;

			if (admin == null)
			{
				admin = new User { Role = Role.Admin, Active = true };
			}

			admin.EmailAddress = model.Email.Trim();
			admin.DisplayName = model.DisplayName.Trim();
			admin.Active = true;
			admin.Password = siteSecurityManager.HashPassword(model.Password);

			userService.AddOrReplaceUser(admin, originalEmail);

			// Refresh the cached SecurityConfiguration.Users list so the freshly-saved
			// admin is visible to DasBlogUserStore.FindByNameAsync on the next login.
			siteSecurityConfig.Users = userService.GetAllUsers().ToList();

			logger.LogInformation(new EventDataItem(EventCodes.SecuritySuccess, null,
				"First-run setup completed for {email}", admin.EmailAddress));

			return RedirectToAction(nameof(Login));
		}
	}
}
