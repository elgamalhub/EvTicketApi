using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using EvTicket.Api.Data;
using EvTicket.Api.Data.Seed;
using EvTicket.Api.Domain;
using EvTicket.Api.Hubs;
using EvTicket.Api.Middleware;
using EvTicket.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApi();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ---------- Database ----------
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        options.UseSqlServer(builder.Configuration.GetConnectionString("Default"));
    else
        options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=evticket.db");
});

// ---------- Identity ----------
builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 6;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// ---------- JWT ----------
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be configured with at least 32 characters (set Jwt__Key in production).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---------- App services ----------
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddHostedService<SeatHoldExpiryWorker>();

// ---------- Health ----------
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

// ---------- CORS ----------
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
var envOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS")?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
var allowedOrigins = origins.Concat(envOrigins).Distinct().ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        if (allowedOrigins.Length == 0) policy.AllowAnyOrigin();
        else policy.WithOrigins(allowedOrigins);
        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

// ---------- Rate limiting ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 12;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.Request.Path.StartsWithSegments("/health")) return RateLimitPartition.GetNoLimiter("health");
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 400,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

// ---------- Proxy headers (Docker / Railway / Render) ----------
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// ---------- Migrate + seed ----------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        if (app.Configuration.GetValue("Seed:Enabled", true))
            await DbSeeder.SeedAsync(services, app.Configuration, app.Logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed");
        throw;
    }
}

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("EvTicket API")
        .WithTheme(ScalarTheme.Moon)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
}

app.UseCors("web");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SeatHub>("/hubs/seats");
app.MapHealthChecks("/health");

// Serve the built Angular app when it has been copied to wwwroot (production image).
var spaIndex = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
if (File.Exists(spaIndex))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program;
