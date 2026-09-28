using Autofac;
using Autofac.Extensions.DependencyInjection;
using Amazon.Rekognition;
using Amazon.S3;
using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
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
using NLog.Web;
using System.Reflection;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// TODO configuration-migration: Replace BookstoreConfiguration with IConfiguration
// Logging setup - migrated from LoggingSetup.ConfigureLogging()
builder.Host.UseNLog();

// Autofac configuration
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    // TODO configuration-migration: Read connection string from IConfiguration
    var connectionString = "Server=(localdb)\\mssqllocaldb;Database=BookstoreDb;Trusted_Connection=True;";
    // var connectionString = BookstoreConfiguration.GetConnectionString("BookstoreDatabaseConnection");
    
    containerBuilder.RegisterType<ApplicationDbContext>()
        .WithParameter("connectionString", connectionString)
        .InstancePerLifetimeScope();

    // Services
    containerBuilder.RegisterType<BookService>().As<IBookService>();
    containerBuilder.RegisterType<OrderService>().As<IOrderService>();
    containerBuilder.RegisterType<ReferenceDataService>().As<IReferenceDataService>();
    containerBuilder.RegisterType<OfferService>().As<IOfferService>();
    containerBuilder.RegisterType<CustomerService>().As<ICustomerService>();
    containerBuilder.RegisterType<AddressService>().As<IAddressService>();
    containerBuilder.RegisterType<ShoppingCartService>().As<IShoppingCartService>();
    containerBuilder.RegisterType<ImageResizeService>().As<IImageResizeService>();

    // Repositories
    containerBuilder.RegisterType<CustomerRepository>().As<ICustomerRepository>();
    containerBuilder.RegisterType<AddressRepository>().As<IAddressRepository>();
    containerBuilder.RegisterType<BookRepository>().As<IBookRepository>();
    containerBuilder.RegisterType<OfferRepository>().As<IOfferRepository>();
    containerBuilder.RegisterType<ShoppingCartRepository>().As<IShoppingCartRepository>();
    containerBuilder.RegisterType<OrderRepository>().As<IOrderRepository>();
    containerBuilder.RegisterType<ReferenceDataRepository>().As<IReferenceDataRepository>();

    containerBuilder.RegisterGeneric(typeof(PaginatedList<>)).As(typeof(IPaginatedList<>)).InstancePerLifetimeScope();

    // TODO configuration-migration: Replace BookstoreConfiguration.GetSetting with IConfiguration
    var fileService = "local"; // BookstoreConfiguration.GetSetting("Services/FileService");
    if (fileService == "aws")
    {
        containerBuilder.RegisterType<AmazonS3Client>().As<IAmazonS3>();
        containerBuilder.RegisterType<S3FileService>().As<IFileService>();
    }
    else
    {
        var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Content");
        containerBuilder.RegisterInstance(new LocalFileService(webRootPath)).As<IFileService>();
    }

    var imageValidationService = "local"; // BookstoreConfiguration.GetSetting("Services/ImageValidationService");
    if (imageValidationService == "aws")
    {
        containerBuilder.RegisterType<AmazonRekognitionClient>().As<IAmazonRekognition>();
        containerBuilder.RegisterType<RekognitionImageValidationService>().As<IImageValidationService>();
    }
    else
    {
        containerBuilder.RegisterType<LocalImageValidationService>().As<IImageValidationService>();
    }
});

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor(); // Required for replacing HttpContext.Current

// TODO auth-security-migration: Configure authentication properly
var authenticationMode = "local"; // BookstoreConfiguration.GetSetting("Services/Authentication");
if (authenticationMode == "aws")
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        // TODO configuration-migration: Read from IConfiguration
        options.ClientId = ""; // BookstoreConfiguration.GetSetting("Authentication/Cognito/LocalClientId");
        options.MetadataAddress = ""; // BookstoreConfiguration.GetSetting("Authentication/Cognito/MetadataAddress");
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.SaveTokens = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "cognito:username",
            RoleClaimType = "cognito:groups"
        };
        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = async context =>
            {
                var customerService = context.HttpContext.RequestServices.GetRequiredService<ICustomerService>();
                var identity = (ClaimsIdentity)context.Principal!.Identity!;
                
                var dto = new CreateOrUpdateCustomerDto(
                    identity.FindFirst("sub")!.Value,
                    identity.Name!,
                    identity.FindFirst(c => c.Type.Contains("givenname"))!.Value,
                    identity.FindFirst(c => c.Type.Contains("surname"))!.Value);

                await customerService.CreateOrUpdateCustomerAsync(dto);
            }
        };
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline - migrated from Global.asax.cs Application_Start
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Application_Error equivalent - see Global.asax.cs logging
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exceptionFeature != null)
        {
            logger.LogError(exceptionFeature.Error, "Unhandled exception");
        }
        context.Response.Redirect("/Home/Error");
    });
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

if (authenticationMode == "aws")
{
    app.UseAuthentication();
}
else
{
    // TODO auth-security-migration: Migrate LocalAuthenticationMiddleware to ASP.NET Core middleware
    // app.UseMiddleware<LocalAuthenticationMiddleware>();
}

app.UseAuthorization();

// Route configuration - migrated from RouteConfig.RegisterRoutes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Area routing - migrated from AreaRegistration.RegisterAllAreas()
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
