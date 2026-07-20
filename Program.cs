using LibrarySystem.Data;
using LibrarySystem.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var appSettingsPath = Path.Combine(AppContext.BaseDirectory.Split(new string[] { "\\bin\\" }, StringSplitOptions.None)[0], "appsettings.json");

if (!File.Exists(appSettingsPath))
{
    var defaultJson = @"{
  ""ConnectionStrings"": {
    ""LibraryDB"": ""Server=.\\SQLEXPRESS;Database=LibraryDb;Trusted_Connection=True;TrustServerCertificate=True;""
  },
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning""
    }
  },
  ""AllowedHosts"": ""*""
}";
    File.WriteAllText(appSettingsPath, defaultJson);
}

var builder = WebApplication.CreateBuilder(args);

// 🛡️ SECURITY REGISTRY: API Throttling (Rate Limiting) Configuration
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 20,             
                Window = TimeSpan.FromMinutes(1)
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// 🛡️ SECURITY REGISTRY: JWT Authentication Setup for Microservices
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "ScanexusEngine",
        ValidAudience = "ScanexusStudents",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("Scanexus_Secure_Enterprise_Secret_Key_2026_JWT"))
    };
});

builder.Services.AddControllersWithViews();
builder.Services.AddControllers();

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LibraryDB")));

builder.Services.AddScoped<ILibraryService, LibraryService>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();


app.UseRateLimiter();
app.UseAuthentication();   
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Student}/{action=Login}/{id?}");

app.MapControllers();

app.Run();