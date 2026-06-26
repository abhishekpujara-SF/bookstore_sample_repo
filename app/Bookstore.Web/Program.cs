using Amazon.Rekognition;
using Amazon.S3;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using NLog;
using NLog.Web;
using System.IO;

LogManager.Setup().LoadConfigurationFromFile("nlog.config");
var logger = LogManager.GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    var authMode = builder.Configuration["Services:Authentication"];
    var fileMode = builder.Configuration["Services:FileService"];
    var dbMode = builder.Configuration["Services:Database"];

    if (authMode == "aws" || fileMode == "aws" || dbMode == "aws")
    {
        builder.Configuration.AddSystemsManager($"/{Bookstore.Common.Constants.AppName}");
    }

    builder.Services.AddControllersWithViews(options =>
    {
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        options.Filters.Add(new AuthorizeFilter(policy));
    });

    if (authMode == "aws")
    {
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.ClientId = builder.Configuration["Authentication:Cognito:LocalClientId"];
                options.MetadataAddress = builder.Configuration["Authentication:Cognito:MetadataAddress"];
                options.ResponseType = "code";
                options.UsePkce = true;
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.SaveTokens = true;
                options.UseTokenLifetime = false;
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    NameClaimType = "cognito:username",
                    RoleClaimType = "cognito:groups"
                };
                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var service = context.HttpContext.RequestServices.GetRequiredService<ICustomerService>();
                        var identity = (System.Security.Claims.ClaimsIdentity)context.Principal.Identity;
                        var dto = new CreateOrUpdateCustomerDto(
                            identity.GetSub(),
                            identity.Name ?? string.Empty,
                            identity.FindFirst(c => c.Type.Contains("givenname"))?.Value ?? string.Empty,
                            identity.FindFirst(c => c.Type.Contains("surname"))?.Value ?? string.Empty);
                        await service.CreateOrUpdateCustomerAsync(dto);
                    }
                };
            });
    }
    else
    {
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie();
    }

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("BookstoreDatabaseConnection")));

    builder.Services.AddScoped<IBookService, BookService>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
    builder.Services.AddScoped<IOfferService, OfferService>();
    builder.Services.AddScoped<ICustomerService, CustomerService>();
    builder.Services.AddScoped<IAddressService, AddressService>();
    builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
    builder.Services.AddScoped<IImageResizeService, ImageResizeService>();

    builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
    builder.Services.AddScoped<IAddressRepository, AddressRepository>();
    builder.Services.AddScoped<IBookRepository, BookRepository>();
    builder.Services.AddScoped<IOfferRepository, OfferRepository>();
    builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

    if (fileMode == "aws")
    {
        builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
        builder.Services.AddScoped<IFileService, S3FileService>();
    }
    else
    {
        builder.Services.AddSingleton<IFileService>(
            new LocalFileService(Path.Combine(builder.Environment.ContentRootPath, "Content")));
    }

    if (builder.Configuration["Services:ImageValidationService"] == "aws")
    {
        builder.Services.AddSingleton<IAmazonRekognition, AmazonRekognitionClient>();
        builder.Services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
    }
    else
    {
        builder.Services.AddScoped<IImageValidationService, LocalImageValidationService>();
    }

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "Content")),
        RequestPath = "/Content"
    });

    app.UseRouting();
    app.UseAuthentication();

    if (authMode != "aws")
    {
        app.UseMiddleware<LocalAuthenticationMiddleware>();
    }

    app.UseAuthorization();

    app.MapAreaControllerRoute("admin", "Admin", "Admin/{controller=Dashboard}/{action=Index}/{id?}");
    app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Application stopped due to exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}
