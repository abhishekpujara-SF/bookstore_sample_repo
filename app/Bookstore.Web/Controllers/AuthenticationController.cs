using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace Bookstore.Web.Controllers
{
    public class AuthenticationController : Controller
    {
        private readonly IConfiguration _configuration;

        public AuthenticationController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Login(string redirectUri = null)
        {
            if (string.IsNullOrWhiteSpace(redirectUri)) return RedirectToAction("Index", "Home");

            return Redirect(redirectUri);
        }

        public async Task<IActionResult> LogOut()
        {
            return _configuration["Services:Authentication"] == "aws"
                ? CognitoSignOut()
                : await LocalSignOutAsync();
        }

        private async Task<IActionResult> LocalSignOutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }

        private IActionResult CognitoSignOut()
        {
            var domain = _configuration["Authentication:Cognito:CognitoDomain"];
            var clientId = _configuration["Authentication:Cognito:LocalClientId"];
            var logoutUri = $"{Request.Scheme}://{Request.Host}/";

            return Redirect($"{domain}/logout?client_id={clientId}&logout_uri={logoutUri}");
        }
    }
}
