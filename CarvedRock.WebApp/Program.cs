using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using CarvedRock.Core;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Exceptions;
using Serilog.Enrichers.Span;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();

builder.Host.UseSerilog((context, loggerConfig) => { 
    loggerConfig
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.WithProperty("Application", Assembly.GetExecutingAssembly().GetName().Name ?? "UI")
    .Enrich.WithExceptionDetails()
    .Enrich.FromLogContext()
    .Enrich.With<ActivityEnricher>()
    .WriteTo.Seq("http://localhost:5341")
    .WriteTo.Console()
    .WriteTo.Debug();
});

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "oidc";    
})
.AddCookie("Cookies")
.AddOpenIdConnect("oidc", options =>
{
    options.Authority = "https://demo.duendesoftware.com";
    options.ClientId = "interactive.confidential";
    options.ClientSecret = "secret";
    options.ResponseType = "code";
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("api");
    options.Scope.Add("offline_access");
    options.GetClaimsFromUserInfoEndpoint = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = "email"
    };    
    options.SaveTokens = true;
});
builder.Services.AddOpenIdConnectAccessTokenManagement();

//builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorPages();
builder.Services.AddUserAccessTokenHttpClient("backend", configureClient: client =>
{
    client.BaseAddress = new Uri("https://localhost:7213");
});

builder.Services.AddWebOptimizer(pipeline =>
{
    pipeline.AddCssBundle("/css/bundled.css",
        "/css/main.css",
        "/css/custom.css");
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    System.Net.ServicePointManager.ServerCertificateValidationCallback += 
        (sender, certificate, chain, sslPolicyErrors) => true;
}

app.UseExceptionHandler("/Error");

//app.UseResponseCompression();
app.UseWebOptimizer();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<UserScopeMiddleware>();
app.UseSerilogRequestLogging();
app.UseAuthorization();
app.MapRazorPages().RequireAuthorization();

app.Run();
