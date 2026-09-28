using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
// TODO mvc-webapi-migration: Replace System.Web.Optimization bundling
using Microsoft.AspNetCore.Routing;
using NLog;

namespace Bookstore.Web
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_Error()
        {
            var ex = Server.GetLastError();
            var logger = LogManager.GetCurrentClassLogger();

            logger.Error(ex);
        }
    }
}
