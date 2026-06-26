using Bookstore.Domain.Customers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bookstore.Web.Helpers
{
    public class LocalAuthenticationMiddleware
    {
        private const string UserId = "FB6135C7-1464-4A72-B74E-4B63D343DD09";
        private readonly RequestDelegate _next;

        public LocalAuthenticationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ICustomerService customerService)
        {
            if (context.Request.Path.StartsWithSegments("/Authentication/Login"))
            {
                var principal = CreateClaimsPrincipal();
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                await SaveCustomerAsync(customerService);
                context.Response.Redirect("/");
                return;
            }

            await _next(context);
        }

        private static ClaimsPrincipal CreateClaimsPrincipal()
        {
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, "bookstoreuser"));
            identity.AddClaim(new Claim("nameidentifier", UserId));
            identity.AddClaim(new Claim("given_name", "Bookstore"));
            identity.AddClaim(new Claim("family_name", "User"));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrators"));
            return new ClaimsPrincipal(identity);
        }

        private static async Task SaveCustomerAsync(ICustomerService customerService)
        {
            var dto = new CreateOrUpdateCustomerDto(UserId, "bookstoreuser", "Bookstore", "User");
            await customerService.CreateOrUpdateCustomerAsync(dto);
        }
    }
}
