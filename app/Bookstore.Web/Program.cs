using System.IO;
using System.Reflection;
using Amazon.Rekognition;
using Amazon.S3;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using BobsBookstoreClassic.Data;
using Bookstore.Data;
using Microsoft.EntityFrameworkCore;
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
using NLog;
using NLog.Web;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── NLog integration ───────────────────────────────────────────────
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // ─── Autofac DI container ───────────────────────────────────────────
    builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
    builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        // Domain services
        containerBuilder.RegisterType<BookService>().As<IBookService>();
        containerBuilder.RegisterType<OrderService>().As<IOrderService>();
        containerBuilder.RegisterType<ReferenceDataService>().As<IReferenceDataService>();
        containerBuilder.RegisterType<OfferService>().As<IOfferService>();
        containerBuilder.RegisterType<CustomerService>().As<ICustomerService>();
        containerBuilder.RegisterType<AddressService>().As<IAddressService>();
        containerBuilder.RegisterType<ShoppingCartService>().As<IShoppingCartService>();
        containerBuilder.RegisterType<ImageResizeService>().As<IImageResizeService>();

        // Data access
        var connectionString = BookstoreConfiguration.GetConnectionString("BookstoreDatabaseConnection");
        containerBuilder.Register(_ =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(connectionString);
            return new ApplicationDbContext(optionsBuilder.Options);
        }).InstancePerLifetimeScope();

        containerBuilder.RegisterType<CustomerRepository>().As<ICustomerRepository>();
        containerBuilder.RegisterType<AddressRepository>().As<IAddressRepository>();
        containerBuilder.RegisterType<BookRepository>().As<IBookRepository>();
        containerBuilder.RegisterType<OfferRepository>().As<IOfferRepository>();
        containerBuilder.RegisterType<ShoppingCartRepository>().As<IShoppingCartRepository>();
        containerBuilder.RegisterType<OrderRepository>().As<IOrderRepository>();
        containerBuilder.RegisterType<ReferenceDataRepository>().As<IReferenceDataRepository>();
        containerBuilder.RegisterGeneric(typeof(PaginatedList<>)).As(typeof(IPaginatedList<>)).InstancePerLifetimeScope();

        // File service (AWS S3 or local)
        if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
        {
            containerBuilder.RegisterType<AmazonS3Client>().As<IAmazonS3>();
            containerBuilder.RegisterType<S3FileService>().As<IFileService>();
        }
        else
        {
            var webRootPath = builder.Environment.WebRootPath
                ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
            containerBuilder.RegisterInstance(new LocalFileService(webRootPath)).As<IFileService>();
        }

        // Image validation service (AWS Rekognition or local)
        if (BookstoreConfiguration.GetSetting("Services/ImageValidationService") == "aws")
        {
            containerBuilder.RegisterType<AmazonRekognitionClient>().As<IAmazonRekognition>();
            containerBuilder.RegisterType<RekognitionImageValidationService>().As<IImageValidationService>();
        }
        else
        {
            containerBuilder.RegisterType<LocalImageValidationService>().As<IImageValidationService>();
        }
    });

    // ─── Initialize legacy BookstoreConfiguration singleton ─────────────
    BookstoreConfiguration.Initialize(builder.Configuration);

    // ─── Configuration setup (AWS SSM parameters) ───────────────────────
    Bookstore.Web.ConfigurationSetup.ConfigureConfiguration();

    // ─── MVC services ───────────────────────────────────────────────────
    builder.Services.AddControllersWithViews(options =>
    {
        // Global authorization filter (equivalent to FilterConfig: AuthorizeAttribute)
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
    });

    builder.Services.AddHttpContextAccessor();

    // ─── Authentication ─────────────────────────────────────────────────
    if (builder.Configuration["Services/Authentication"] == "aws")
    {
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.MetadataAddress = BookstoreConfiguration.GetSetting("Authentication/MetadataAddress");
            options.ClientId = BookstoreConfiguration.GetSetting("Authentication/ClientId");
            options.ClientSecret = BookstoreConfiguration.GetSetting("Authentication/ClientSecret");
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
        });
    }
    else
    {
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie();
    }

    // ─── Build the application ──────────────────────────────────────────
    var app = builder.Build();

    // ─── Middleware pipeline ────────────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication();

    // Local authentication middleware (when not using AWS Cognito)
    if (app.Configuration["Services/Authentication"] != "aws")
    {
        app.UseMiddleware<LocalAuthenticationMiddleware>();
    }

    app.UseAuthorization();

    // ─── Endpoint routing ───────────────────────────────────────────────
    // Area routing (Admin area)
    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    // Default route
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    // NLog: catch setup errors
    logger.Error(ex, "Application stopped because of an exception during startup.");
    throw;
}
finally
{
    LogManager.Shutdown();
}
