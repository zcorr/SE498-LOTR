using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using web_server.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register API client with base address
var lotrApiBaseUrl = builder.Configuration["LotrApi:BaseUrl"] ?? "http://localhost:5030";
builder.Services.AddHttpClient<ILotrApiClient, LotrApiClient>(client =>
{
    client.BaseAddress = new Uri(lotrApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// Jurassic movie service — used to render sponsored "banner ads" on the LOTR pages.
// Cross-origin from the browser is blocked by the Jurassic API's CORS allow-list,
// so the LOTR web server proxies it server-side via /api/banner-ads/movies.
var jurassicBaseUrl = builder.Configuration["JurassicApi:BaseUrl"] ?? "http://localhost:5081";
builder.Services.AddHttpClient<IJurassicAdsClient, JurassicAdsClient>(client =>
{
    client.BaseAddress = new Uri(jurassicBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(2);
});

// Register auth service
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICharacterSheetService, CharacterSheetService>();

// Configure JWT authentication
var jwtSecret = builder.Configuration["Jwt:Secret"];

if(string.IsNullOrWhiteSpace(jwtSecret)) {
	jwtSecret = "Cool_Mega_Secret_Key_For_JWT_Token_Generation";
}
var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        // Read JWT token from cookies instead of Authorization header
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies["AuthToken"];
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
{
    var cs = sp.GetRequiredService<IConfiguration>()
        .GetConnectionString("UsersConnection");
    if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException("ConnectionStrings:UsersConnection is required.");
    return NpgsqlDataSource.Create(cs);
});

var app = builder.Build();

// Seed the default admin user on startup
using (var scope = app.Services.CreateScope())
{
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.SeedDefaultUserAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Important: order matters here
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Map API controllers
app.MapControllers();

app.Run();


public partial class Program;
