using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NLog;

using NLog.Config;
using NLog.Targets;
using NLog.Web;
using System.Security.Claims;

namespace Bookstore.Web
{
    public class Program
    {
        private const string AppName = "Bookstore";
        public static void Main(string[] args)
        {
            // -----------------------------------------------------------------------
            // Migrated from LoggingSetup.ConfigureLogging() (OWIN Startup.cs)
            // NLog is configured programmatically before the host is built so that
            // startup log messages are captured.
            // -----------------------------------------------------------------------
            var nlogConfig = new LoggingConfiguration();
            NLog.Targets.Target loggingTarget;

            var loggingServiceSetting = Environment.GetEnvironmentVariable("Services__LoggingService")
                                        ?? "local";

            if (loggingServiceSetting == "aws")
            {
                loggingTarget = new FileTarget("aws") { FileName = $"{AppName}-log.txt" };
            }
            else
            {
                loggingTarget = new DebuggerTarget();
            }

            nlogConfig.AddTarget("aws", loggingTarget);
            nlogConfig.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, loggingTarget));
            LogManager.Configuration = nlogConfig;

            var logger = LogManager.GetCurrentClassLogger();

            try
            {
                var builder = WebApplication.CreateBuilder(args);

                // -------------------------------------------------------------------
                // Migrated from ConfigurationSetup.ConfigureConfiguration() (OWIN Startup.cs)
                // AWS Systems Manager Parameter Store values are loaded into
                // builder.Configuration so the rest of startup can use IConfiguration.
                // -------------------------------------------------------------------
                LoadAwsParameterStoreConfiguration(builder.Configuration);

                // -------------------------------------------------------------------
                // Logging — wire NLog into the ASP.NET Core logging pipeline
                // (replaces LoggingSetup.ConfigureLogging()).
                // -------------------------------------------------------------------
                builder.Logging.ClearProviders();
                builder.Host.UseNLog();

                // -------------------------------------------------------------------
                // Autofac — replaces DependencyInjectionSetup.ConfigureDependencyInjection()
                // Use AutofacServiceProviderFactory so that all existing Autofac
                // registrations are kept intact. ContainerBuilder is configured in
                // the ConfigureContainer<ContainerBuilder> callback below.
                // -------------------------------------------------------------------
                builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

                // -------------------------------------------------------------------
                // MVC / Controllers with Views
                // Migrated from FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters)
                // Global HandleError and Authorize filters are registered here.
                // -------------------------------------------------------------------
                builder.Services.AddControllersWithViews(options =>
                {
                    // Equivalent to FilterConfig: HandleErrorAttribute is replaced by
                    // the built-in exception handler middleware (see app.UseExceptionHandler below).
                    // Global [Authorize] is preserved as a global filter.
                    options.Filters.Add(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));
                });

                // -------------------------------------------------------------------
                // Authentication
                // Migrated from AuthenticationConfig.ConfigureAuthentication(app)
                // Cognito (OpenIdConnect + Cookie) or local middleware depending on
                // the "Services/Authentication" configuration value.
                // -------------------------------------------------------------------
                var authenticationMode = builder.Configuration["Services/Authentication"] ?? "local";

                if (authenticationMode == "aws")
                {
                    builder.Services
                        .AddAuthentication(options =>
                        {
                            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                        })
                        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
                        .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                        {
                            options.ClientId = builder.Configuration["Authentication/Cognito/LocalClientId"];
                            options.MetadataAddress = builder.Configuration["Authentication/Cognito/MetadataAddress"];
                            options.ResponseType = OpenIdConnectResponseType.Code;
                            options.UsePkce = true;
                            options.Scope.Clear();
                            options.Scope.Add("openid");
                            options.Scope.Add("profile");
                            options.SaveTokens = true;
                            options.UseTokenLifetime = false;
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                NameClaimType = "cognito:username",
                                RoleClaimType = "cognito:groups"
                            };

                            // Migrated from RedirectToIdentityProvider + AuthorizationCodeReceived notifications.
                            // Sets the redirect_uri to the /signin-oidc callback using IHttpContextAccessor.
                            options.Events = new OpenIdConnectEvents
                            {
                                OnRedirectToIdentityProvider = ctx =>
                                {
                                    var returnUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}/signin-oidc";
                                    ctx.ProtocolMessage.RedirectUri = returnUrl;
                                    return Task.CompletedTask;
                                },
                                OnAuthorizationCodeReceived = ctx =>
                                {
                                    var returnUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}/signin-oidc";
                                    ctx.TokenEndpointRequest!.RedirectUri = returnUrl;
                                    return Task.CompletedTask;
                                },
                                OnTokenValidated = async ctx =>
                                {
                                    // Resolve ICustomerService from the DI container (Autofac scope).
                                    var service = ctx.HttpContext.RequestServices.GetRequiredService<ICustomerService>();

                                    var identity = ctx.Principal?.Identity as ClaimsIdentity;
                                    if (identity != null)
                                    {
                                        var sub = identity.FindFirst("sub")?.Value ?? string.Empty;
                                        var name = identity.Name ?? string.Empty;
                                        var givenName = identity.FindFirst(c => c.Type.Contains("givenname"))?.Value ?? string.Empty;
                                        var surname = identity.FindFirst(c => c.Type.Contains("surname"))?.Value ?? string.Empty;

                                        var dto = new CreateOrUpdateCustomerDto(sub, name, givenName, surname);
                                        await service.CreateOrUpdateCustomerAsync(dto);
                                    }
                                }
                            };
                        });
                }
                else
                {
                    // Local authentication: cookie-only; the LocalAuthenticationMiddleware
                    // handles setting the ClaimsPrincipal (see middleware pipeline below).
                    builder.Services
                        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);

                    // LocalAuthenticationMiddleware is registered in the Autofac container
                    // (see ConfigureContainer) and added to the pipeline below.
                }

                // IHttpContextAccessor — needed by LocalAuthenticationMiddleware and
                // any code that previously relied on HttpContext.Current.
                builder.Services.AddHttpContextAccessor();

                // ===================================================================
                // Autofac ContainerBuilder callback
                // Migrated from DependencyInjectionSetup.ConfigureDependencyInjection()
                // All original Autofac registrations are preserved exactly.
                // ===================================================================
                builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
                {
                    // Services
                    containerBuilder.RegisterType<BookService>().As<IBookService>();
                    containerBuilder.RegisterType<OrderService>().As<IOrderService>();
                    containerBuilder.RegisterType<ReferenceDataService>().As<IReferenceDataService>();
                    containerBuilder.RegisterType<OfferService>().As<IOfferService>();
                    containerBuilder.RegisterType<CustomerService>().As<ICustomerService>();
                    containerBuilder.RegisterType<AddressService>().As<IAddressService>();
                    containerBuilder.RegisterType<ShoppingCartService>().As<IShoppingCartService>();
                    containerBuilder.RegisterType<ImageResizeService>().As<IImageResizeService>();

                    // DbContext — scoped per HTTP request (InstancePerLifetimeScope is the
                    // ASP.NET Core equivalent of InstancePerRequest when using Autofac).
                    var connectionString = builder.Configuration.GetConnectionString("BookstoreDatabaseConnection")
                                          ?? string.Empty;
                    containerBuilder.RegisterType<ApplicationDbContext>()
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

                    containerBuilder.RegisterGeneric(typeof(PaginatedList<>))
                        .As(typeof(IPaginatedList<>))
                        .InstancePerLifetimeScope();

                    // File service
                    if (builder.Configuration["Services/FileService"] == "aws")
                    {
                        containerBuilder.RegisterType<AmazonS3Client>().As(typeof(IAmazonS3));
                        containerBuilder.RegisterType<S3FileService>().As<IFileService>();
                    }
                    else
                    {
                        // IWebHostEnvironment is available from the builder; resolve webRootPath.
                        var webRootPath = builder.Environment.WebRootPath
                            ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
                        var contentPath = Path.Combine(webRootPath, "Content");
                        containerBuilder.RegisterInstance(new LocalFileService(contentPath)).As<IFileService>();
                    }

                    // Image validation service
                    if (builder.Configuration["Services/ImageValidationService"] == "aws")
                    {
                        containerBuilder.RegisterType<AmazonRekognitionClient>().As(typeof(IAmazonRekognition));
                        containerBuilder.RegisterType<RekognitionImageValidationService>().As<IImageValidationService>();
                    }
                    else
                    {
                        containerBuilder.RegisterType<LocalImageValidationService>().As<IImageValidationService>();
                    }

                    // Local authentication middleware (only registered when not using AWS Cognito).
                    if (builder.Configuration["Services/Authentication"] != "aws")
                    {
                        containerBuilder.RegisterType<LocalAuthenticationMiddleware>();
                    }
                });

                // ===================================================================
                // Build the application
                // ===================================================================
                var app = builder.Build();

                // -------------------------------------------------------------------
                // Exception handling
                // Migrated from Global.asax Application_Error and
                // FilterConfig HandleErrorAttribute.
                // -------------------------------------------------------------------
                if (app.Environment.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }
                else
                {
                    // app.UseExceptionHandler replaces Application_Error + HandleErrorAttribute.
                    app.UseExceptionHandler("/Home/Error");
                    app.UseHsts();
                }

                app.UseHttpsRedirection();
                app.UseStaticFiles();

                app.UseRouting();

                // -------------------------------------------------------------------
                // Authentication middleware
                // For AWS mode: standard ASP.NET Core Cookie + OIDC pipeline.
                // For local mode: LocalAuthenticationMiddleware sets the ClaimsPrincipal
                //   (replaces OWIN UseMiddlewareFromContainer<LocalAuthenticationMiddleware>()).
                // -------------------------------------------------------------------
                app.UseAuthentication();

                if (app.Configuration["Services/Authentication"] != "aws")
                {
                    app.UseMiddleware<LocalAuthenticationMiddleware>();
                }

                app.UseAuthorization();

                // -------------------------------------------------------------------
                // Routing
                // Migrated from RouteConfig.RegisterRoutes() and
                //   AreaRegistration.RegisterAllAreas() (Global.asax Application_Start).
                // Convention: {controller}/{action}/{id?} with Home/Index defaults.
                // -------------------------------------------------------------------
                app.MapAreaControllerRoute(
                    name: "Admin_default",
                    areaName: "Admin",
                    pattern: "Admin/{controller}/{action}/{id?}",
                    defaults: new { action = "Index" });

                app.MapDefaultControllerRoute(); // pattern: {controller=Home}/{action=Index}/{id?}

                app.Run();
            }
            catch (Exception ex)
            {
                // Log fatal startup errors (equivalent to Application_Error in Global.asax).
                logger.Error(ex, "Application startup failed.");
                throw;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        // ---------------------------------------------------------------------------
        // Migrated from ConfigurationSetup.ConfigureConfiguration() (OWIN Startup.cs)
        // Reads AWS Systems Manager Parameter Store values into IConfigurationBuilder
        // so they are available via IConfiguration throughout the application.
        // ---------------------------------------------------------------------------
        private static void LoadAwsParameterStoreConfiguration(IConfigurationManager configuration)
        {
            var rootPath = "/" + AppName;
            const string databasePath = "/Database";
            const string authenticationPath = "/Authentication";
            const string fileServicePath = "/Files";

            if (configuration["Services/Database"] == "aws")
            {
                using var client = new AmazonSimpleSystemsManagementClient();
                var request = new GetParameterRequest
                {
                    Name = $"{rootPath}{databasePath}/ConnectionStrings/BookstoreDatabaseConnection"
                };
                var response = client.GetParameterAsync(request).GetAwaiter().GetResult();
                var key = response.Parameter.Name
                    .Replace($"{rootPath}{databasePath}/", string.Empty);
                configuration[key] = response.Parameter.Value;
            }

            if (configuration["Services/Authentication"] == "aws")
            {
                using var client = new AmazonSimpleSystemsManagementClient();
                var request = new GetParametersByPathRequest
                {
                    Path = $"{rootPath}{authenticationPath}/",
                    Recursive = true
                };
                var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
                foreach (var parameter in response.Parameters)
                {
                    var key = parameter.Name.Replace($"{rootPath}/", string.Empty);
                    configuration[key] = parameter.Value;
                }
            }

            if (configuration["Services/FileService"] == "aws")
            {
                using var client = new AmazonSimpleSystemsManagementClient();
                var request = new GetParametersByPathRequest
                {
                    Path = $"{rootPath}{fileServicePath}/",
                    Recursive = true
                };
                var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
                foreach (var parameter in response.Parameters)
                {
                    var key = parameter.Name.Replace($"{rootPath}/", string.Empty);
                    configuration[key] = parameter.Value;
                }
            }
        }
    }
}
