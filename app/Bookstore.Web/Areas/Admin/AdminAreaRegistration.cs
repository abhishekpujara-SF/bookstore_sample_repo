// [PORT-TODO: ASP.NET Core does not use AreaRegistration classes. The Admin area route
// "Admin/{controller}/{action}/{id}" with defaults { action = "Index", id = optional }
// and namespace "Bookstore.Web.Areas.Admin.Controllers" must be registered in Program.cs as:
//   app.MapControllerRoute(
//       name: "Admin_default",
//       pattern: "Admin/{controller=Home}/{action=Index}/{id?}");
// Additionally, each controller in the Admin area must have [Area("Admin")] attribute.
// This file is retained as a reference for the route configuration that must be moved to Program.cs.]

namespace Bookstore.Web.Areas
{
    // This class is intentionally left as a placeholder.
    // In ASP.NET Core, area routing is configured via endpoint routing in Program.cs
    // and the [Area("Admin")] attribute on controllers.
    // See the PORT-TODO comment above for the route that must be registered.
}
