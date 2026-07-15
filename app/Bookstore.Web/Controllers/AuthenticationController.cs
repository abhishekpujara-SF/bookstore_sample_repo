using System;
using BobsBookstoreClassic.Data;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Bookstore.Web.Controllers
{
    public class AuthenticationController : Controller
    {
        public ActionResult Login(string redirectUri = null)
        {
            if (string.IsNullOrWhiteSpace(redirectUri))
                return RedirectToAction("Index", "Home");
            return Redirect(redirectUri);
        }

        public async Task<ActionResult> LogOut()
        {
            return BookstoreConfiguration.GetSetting("Services/Authentication") == "aws" ? await CognitoSignOut() : await LocalSignOut();
        }

        private async Task<ActionResult> LocalSignOut()
        {
            if (Request.Cookies.ContainsKey("LocalAuthentication"))
            {
                Response.Cookies.Delete("LocalAuthentication");
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }

        private async Task<ActionResult> CognitoSignOut()
        {
            if (Request.Cookies.ContainsKey(".AspNet.Cookies"))
            {
                Response.Cookies.Delete(".AspNet.Cookies");
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var domain = BookstoreConfiguration.GetSetting("Authentication/Cognito/CognitoDomain");
            var clientId = BookstoreConfiguration.GetSetting("Authentication/Cognito/LocalClientId");
            var logoutUri = $"{Request.Scheme}://{Request.Host}/";
            return Redirect($"{domain}/logout?client_id={clientId}&logout_uri={logoutUri}");
        }
    }
}
