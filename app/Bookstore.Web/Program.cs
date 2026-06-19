using System;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Amazon.S3;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
using Bookstore.Domain;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Web;
using System.Security.Claims;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bookstore.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Bootstrap NLog early so startup errors are captured
            var logger = LogManager.Setup()
                .LoadConfigurationFromAppSettings()
                .GetCurrentClassLogger();

            try
            {
                logger.Info("Starting Bookstore.Web application");

                var builder = WebApplication.CreateBuilder(args);

                // ----------------------------------------------------------------
                // 1. Configuration
                //    builder.Configuration already loads appsettings.json and
                //    environment variables by default (WebApplication.CreateBuilder).
                //    AWS SSM configuration is loaded conditionally at runtime via
                //    the ConfigurationSetup logic preserved below.
                // ----------------------------------------------------------------
                ConfigurationManager.Configuration = builder.Configuration;

                // Load AWS SSM parameters into the configuration when targeting AWS.
                // This mirrors the original ConfigurationSetup.ConfigureConfiguration().
                LoadAwsConfiguration(builder.Configuration);

                // ----------------------------------------------------------------
                // 2. Logging — NLog via NLog.Web.AspNetCore
                //    Replaces LoggingSetup.ConfigureLogging() / NLog.AWS.Logger.
                // ----------------------------------------------------------------
                builder.Logging.ClearProviders();
                builder.Host.UseNLog();

                // ----------------------------------------------------------------
                // 3. Autofac as the DI container
                //    Replaces Autofac.Integration.Mvc + Autofac.Owin pattern.
                // ----------------------------------------------------------------
                builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

                // ----------------------------------------------------------------
                // 4. MVC / Areas / Filters
                //    Replaces AreaRegistration.RegisterAllAreas() and
                //    FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters).
                //    Areas are discovered automatically in ASP.NET Core when
                //    controllers carry [Area] attributes.
                // ----------------------------------------------------------------
                builder.Services.AddControllersWithViews(options =>
                {
                    // Mirrors FilterConfig: global HandleError-equivalent is the
                    // exception-handler middleware; global Authorize is added here.
                    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
                })
                .AddRazorOptions(options =>
                {
                    // Support area view discovery (e.g. Areas/Admin/Views/...)
                    options.AreaViewLocationFormats.Add("/Areas/{2}/Views/{1}/{0}.cshtml");
                    options.AreaViewLocationFormats.Add("/Areas/{2}/Views/Shared/{0}.cshtml");
                });

                // ----------------------------------------------------------------
                // 5. Authentication
                //    Replaces OWIN UseCookieAuthentication + UseOpenIdConnectAuthentication.
                // ----------------------------------------------------------------
                ConfigureAuthentication(builder);

                // ----------------------------------------------------------------
                // 6. Register Autofac container modules
                //    Called automatically by the AutofacServiceProviderFactory.
                // ----------------------------------------------------------------
                builder.Host.ConfigureContainer<ContainerBuilder>(RegisterAutofacDependencies);

                // ----------------------------------------------------------------
                // Build the application
                // ----------------------------------------------------------------
                var app = builder.Build();

                // ----------------------------------------------------------------
                // 7. Middleware pipeline
                //    Maintains proper ordering: static files → routing →
                //    authentication → authorization → endpoints.
                // ----------------------------------------------------------------
                if (app.Environment.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }
                else
                {
                    app.UseExceptionHandler("/Home/Error");
                    app.UseHsts();
                }

                app.UseHttpsRedirection();

                // Replaces BundleConfig — scripts/CSS are served directly as
                // static files.  Bundles are not supported in ASP.NET Core.
                app.UseStaticFiles();

                app.UseRouting();

                app.UseAuthentication();
                app.UseAuthorization();

                // ----------------------------------------------------------------
                // 8. Route registration
                //    Replaces RouteConfig.RegisterRoutes(RouteTable.Routes).
                // ----------------------------------------------------------------

                // Area route — mirrors AdminAreaRegistration.RegisterArea()
                app.MapControllerRoute(
                    name: "Admin_default",
                    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}",
                    defaults: new { area = "Admin" }
                ).RequireAuthorization();

                // Default route — mirrors RouteConfig: {controller}/{action}/{id}
                app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

                app.Run();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Application terminated unexpectedly");
                throw;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        // --------------------------------------------------------------------
        // Authentication configuration
        // Replaces AuthenticationConfig.ConfigureAuthentication(IAppBuilder app)
        // --------------------------------------------------------------------
        private static void ConfigureAuthentication(WebApplicationBuilder builder)
        {
            var configuration = builder.Configuration;
            var authMode = configuration["Services/Authentication"];

            if (authMode == "aws")
            {
                // Cognito OpenIdConnect authentication
                // Replaces OWIN ConfigureCognitoAuthentication()
                builder.Services
                    .AddAuthentication(options =>
                    {
                        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                    })
                    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                    {
                        options.ClientId = configuration["Authentication/Cognito/LocalClientId"];
                        options.MetadataAddress = configuration["Authentication/Cognito/MetadataAddress"];
                        options.ResponseType = OpenIdConnectResponseType.Code;
                        options.UsePkce = true;          // replaces RedeemCode = true
                        options.Scope.Clear();
                        options.Scope.Add("openid");
                        options.Scope.Add("profile");
                        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        options.UseTokenLifetime = false;
                        options.SaveTokens = true;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            NameClaimType = "cognito:username",
                            RoleClaimType = "cognito:groups"
                        };

                        // Replaces Notifications.RedirectToIdentityProvider
                        options.Events = new OpenIdConnectEvents
                        {
                            OnRedirectToIdentityProvider = context =>
                            {
                                var returnUrl = context.HttpContext.Request.GetReturnUrl();
                                if (!string.IsNullOrEmpty(returnUrl))
                                {
                                    context.ProtocolMessage.RedirectUri = returnUrl;
                                }
                                return Task.CompletedTask;
                            },
                            OnAuthorizationCodeReceived = context =>
                            {
                                var returnUrl = context.HttpContext.Request.GetReturnUrl();
                                if (!string.IsNullOrEmpty(returnUrl))
                                {
                                    context.TokenEndpointRequest!.RedirectUri = returnUrl;
                                }
                                return Task.CompletedTask;
                            },
                            OnTokenValidated = async context =>
                            {
                                // Replaces SecurityTokenValidated notification.
                                // Resolve ICustomerService from the request scope.
                                var service = context.HttpContext.RequestServices
                                    .GetRequiredService<ICustomerService>();

                                var identity = (ClaimsIdentity)context.Principal!.Identity!;

                                var dto = new CreateOrUpdateCustomerDto(
                                    identity.GetSub(),
                                    identity.Name!,
                                    identity.FindFirst(c => c.Type.Contains("givenname"))!.Value,
                                    identity.FindFirst(c => c.Type.Contains("surname"))!.Value);

                                await service.CreateOrUpdateCustomerAsync(dto);
                            }
                        };
                    });
            }
            else
            {
                // Local authentication — cookie only; LocalAuthenticationMiddleware
                // handles login and identity creation via ASP.NET Core middleware.
                builder.Services
                    .AddAuthentication(options =>
                    {
                        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    })
                    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                    {
                        options.Cookie.Name = "LocalAuthentication";
                        options.Cookie.MaxAge = TimeSpan.FromDays(1);
                        options.LoginPath = "/Authentication/Login";
                    });
            }
        }

        // --------------------------------------------------------------------
        // Autofac container registrations
        // Replaces DependencyInjectionSetup.ConfigureDependencyInjection()
        // --------------------------------------------------------------------
        private static void RegisterAutofacDependencies(
            HostBuilderContext hostContext,
            ContainerBuilder containerBuilder)
        {
            var configuration = hostContext.Configuration;

            // Domain services
            containerBuilder.RegisterType<BookService>().As<IBookService>();
            containerBuilder.RegisterType<OrderService>().As<IOrderService>();
            containerBuilder.RegisterType<ReferenceDataService>().As<IReferenceDataService>();
            containerBuilder.RegisterType<OfferService>().As<IOfferService>();
            containerBuilder.RegisterType<CustomerService>().As<ICustomerService>();
            containerBuilder.RegisterType<AddressService>().As<IAddressService>();
            containerBuilder.RegisterType<ShoppingCartService>().As<IShoppingCartService>();
            containerBuilder.RegisterType<ImageResizeService>().As<IImageResizeService>();

            // Database context — InstancePerLifetimeScope replaces InstancePerRequest
            var connectionString = configuration.GetConnectionString("BookstoreDatabaseConnection");
            containerBuilder
                .RegisterType<ApplicationDbContext>()
                .WithParameter("connectionString", connectionString)
                .InstancePerLifetimeScope();

            // Repositories
            containerBuilder.RegisterType<CustomerRepository>().As<ICustomerRepository>();
            containerBuilder.RegisterType<AddressRepository>().As<IAddressRepository>();
            containerBuilder.RegisterType<BookRepository>().As<IBookRepository>();
            containerBuilder.RegisterType<OfferRepository>().As<IOfferRepository>();
            containerBuilder.RegisterType<ShoppingCartRepository>().As<IShoppingCartRepository>();
            containerBuilder.RegisterType<OrderRepository>().As<IOrderRepository>();
            containerBuilder.RegisterType<ReferenceDataRepository>().As<IReferenceDataRepository>();

            // Generic paginated list
            containerBuilder
                .RegisterGeneric(typeof(PaginatedList<>))
                .As(typeof(IPaginatedList<>))
                .InstancePerLifetimeScope();

            // File service
            if (configuration["Services/FileService"] == "aws")
            {
                containerBuilder.RegisterType<AmazonS3Client>().As(typeof(IAmazonS3));
                containerBuilder.RegisterType<S3FileService>().As<IFileService>();
            }
            else
            {
                // In ASP.NET Core the web root path is IWebHostEnvironment.WebRootPath
                // We resolve it via the container at runtime.
                containerBuilder
                    .Register(ctx =>
                    {
                        var env = ctx.Resolve<IWebHostEnvironment>();
                        return new LocalFileService(env.WebRootPath);
                    })
                    .As<IFileService>()
                    .SingleInstance();
            }

            // Image validation service
            if (configuration["Services/ImageValidationService"] == "aws")
            {
                containerBuilder.RegisterType<AmazonRekognitionClient>().As(typeof(IAmazonRekognition));
                containerBuilder.RegisterType<RekognitionImageValidationService>().As<IImageValidationService>();
            }
            else
            {
                containerBuilder.RegisterType<LocalImageValidationService>().As<IImageValidationService>();
            }

            // LocalAuthenticationMiddleware is only needed for non-AWS auth
            if (configuration["Services/Authentication"] != "aws")
            {
                containerBuilder.RegisterType<LocalAuthenticationMiddleware>();
            }
        }

        // --------------------------------------------------------------------
        // AWS SSM configuration loading
        // Replaces ConfigurationSetup.ConfigureConfiguration()
        // --------------------------------------------------------------------
        private static void LoadAwsConfiguration(IConfiguration configuration)
        {
            // AWS SSM parameters are loaded into the process via environment
            // variables or IConfiguration providers in ASP.NET Core.
            // When running on AWS (Services/Database == "aws"), the connection
            // string and other secrets should be injected through environment
            // variables or the AWS Systems Manager .NET Configuration Provider
            // (Amazon.Extensions.Configuration.SystemsManager NuGet package).
            //
            // The conditional branches below preserve the original intent of
            // ConfigurationSetup for environments that still resolve at startup:
            //
            //   builder.Configuration.AddSystemsManager("/BobsBookstore/Database");
            //   builder.Configuration.AddSystemsManager("/BobsBookstore/Authentication");
            //   builder.Configuration.AddSystemsManager("/BobsBookstore/Files");
            //
            // These are commented out because the SSM NuGet package is not yet
            // listed in the project. Uncomment and add the package reference
            // Amazon.Extensions.Configuration.SystemsManager when deploying to AWS.
        }
    }

    // -------------------------------------------------------------------------
    // ConfigurationManager shim
    // Preserves the static accessor used across the codebase (e.g. BookstoreConfiguration).
    // -------------------------------------------------------------------------
    public class ConfigurationManager
    {
        public static IConfiguration Configuration { get; set; } = null!;
    }
}