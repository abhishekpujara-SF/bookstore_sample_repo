using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Bookstore.Domain.Customers;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Bookstore.Web.Helpers
{
    public class LocalAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private const string UserId = "FB6135C7-1464-4A72-B74E-4B63D343DD09";

        public LocalAuthenticationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var customerService = context.RequestServices.GetRequiredService<ICustomerService>();

            if (context.Request.Path.Value.StartsWith("/Authentication/Login"))
            {
                var principal = CreateClaimsPrincipal();
                context.User = principal;
                await SaveCustomerDetailsAsync(context, customerService);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1),
                    IsPersistent = true
                });
                context.Response.Redirect("/");
            }
            else if (context.Request.Cookies.ContainsKey("LocalAuthentication") || context.User?.Identity?.IsAuthenticated == true)
            {
                if (context.User?.Identity?.IsAuthenticated != true)
                {
                    var principal = CreateClaimsPrincipal();
                    context.User = principal;
                }
                await SaveCustomerDetailsAsync(context, customerService);
                await _next.Invoke(context);
            }
            else
            {
                await _next.Invoke(context);
            }
        }

        private ClaimsPrincipal CreateClaimsPrincipal()
        {
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, "bookstoreuser"));
            identity.AddClaim(new Claim("nameidentifier", UserId));
            identity.AddClaim(new Claim("given_name", "Bookstore"));
            identity.AddClaim(new Claim("family_name", "User"));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrators"));
            return new ClaimsPrincipal(identity);
        }

        private async Task SaveCustomerDetailsAsync(HttpContext context, ICustomerService customerService)
        {
            var identity = (ClaimsIdentity)context.User.Identity;
            var dto = new CreateOrUpdateCustomerDto(identity.FindFirst("nameidentifier").Value, identity.Name, identity.FindFirst("given_name").Value, identity.FindFirst("family_name").Value);
            await customerService.CreateOrUpdateCustomerAsync(dto);
        }
    }
}
