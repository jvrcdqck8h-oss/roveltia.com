using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using System.Text;
using Roveltia.Web.Data;
using Roveltia.Web.Components;
using Roveltia.Web.Security;
using Roveltia.Web.Services;

namespace Roveltia.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
        }

        var dataProtectionKeysPath = ResolveDataProtectionKeysPath(builder);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddRoveltiaDatabase(builder.Configuration);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMemoryCache();
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
            .SetApplicationName("Roveltia");
        builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto |
                ForwardedHeaders.XForwardedHost;

            // Reverse proxy runs outside the container, so trust forwarded headers explicitly.
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });
        builder.Services.Configure<WaitlistEmailOptions>(builder.Configuration.GetSection("Email"));
        builder.Services.Configure<CampaignAdminOptions>(builder.Configuration.GetSection("Admin"));
        builder.Services.AddRateLimiter(options =>
        {
            var securityOptions = builder.Configuration.GetSection("Security").Get<SecurityOptions>() ?? new SecurityOptions();
            var adminRequestsPerMinute = Math.Max(1, securityOptions.Admin.RequestsPerMinute);

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = static (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                return ValueTask.CompletedTask;
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var key = GetRequestClientIp(httpContext) ?? "global";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"global:{key}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            options.AddPolicy("adminCampaign", httpContext =>
            {
                var key = GetRequestClientIp(httpContext) ?? "admin";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"admin:{key}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = adminRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });
        });
        builder.Services.AddScoped<IWaitlistCampaignService, WaitlistCampaignService>();
        builder.Services.AddScoped<IWaitlistEmailSender, WaitlistCampaignService>();

        var app = builder.Build();
        app.ApplyDatabaseMigrationsIfEnabled();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseForwardedHeaders();
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "base-uri 'self'; " +
                "object-src 'none'; " +
                "frame-ancestors 'none'; " +
                "form-action 'self'; " +
                "img-src 'self' data: https:; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "font-src 'self' https://fonts.gstatic.com data:; " +
                "script-src 'self' 'unsafe-inline'; " +
                "connect-src 'self' ws: wss:; " +
                "frame-src https://www.youtube-nocookie.com; " +
                "upgrade-insecure-requests";

            if (context.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
            }

            await next();
        });
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();
        app.UseRateLimiter();
        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        app.MapPost("/api/admin/waitlist/send", SendWaitlistCampaignAsync)
            .RequireRateLimiting("adminCampaign");

        app.Run();
    }

    private static string ResolveDataProtectionKeysPath(WebApplicationBuilder builder)
    {
        var configuredPath = builder.Configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return EnsureWritableDirectory(configuredPath);
        }

        var defaultPath = builder.Environment.IsDevelopment()
            ? Path.Combine(builder.Environment.ContentRootPath, ".data-protection-keys")
            : "/var/roveltia/data-protection-keys";

        try
        {
            return EnsureWritableDirectory(defaultPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackPath = Path.Combine(builder.Environment.ContentRootPath, ".data-protection-keys");
            Console.WriteLine($"DataProtection keys path '{defaultPath}' is not writable. Falling back to '{fallbackPath}'.");
            return EnsureWritableDirectory(fallbackPath);
        }
    }

    private static string EnsureWritableDirectory(string path)
    {
        Directory.CreateDirectory(path);

        var probeFile = Path.Combine(path, ".write-test");
        File.WriteAllText(probeFile, "ok");
        File.Delete(probeFile);

        return path;
    }

    private static async Task<IResult> SendWaitlistCampaignAsync(
        HttpRequest request,
        SendWaitlistCampaignRequest payload,
        IWaitlistCampaignService campaignService,
        IOptions<CampaignAdminOptions> adminOptions,
        CancellationToken cancellationToken)
    {
        var configuredApiKey = adminOptions.Value.CampaignApiKey;

        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            return Results.Problem(
                title: "Campaign sending is not configured.",
                detail: "Set Admin:CampaignApiKey before using this endpoint.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!request.Headers.TryGetValue("X-Api-Key", out var providedApiKey) ||
            !SecureEquals(configuredApiKey, providedApiKey.ToString()))
        {
            return Results.Unauthorized();
        }

        if (request.ContentType is not null &&
            !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new SendWaitlistCampaignResult(
                Success: false,
                MatchedRecipients: 0,
                SentCount: 0,
                FailedCount: 0,
                DryRun: payload.DryRun,
                Message: "Requests must use application/json.",
                FailedRecipients: []));
        }

        var result = await campaignService.SendCampaignAsync(payload, cancellationToken);

        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(result);
    }

    private static bool SecureEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string? GetRequestClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();
}

internal static class DatabaseConfiguration
{
    public static IServiceCollection AddRoveltiaDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var defaultConnection = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContextFactory<RoveltiaDbContext>(options => options.UseSqlServer(defaultConnection));
        services.AddScoped<IWaitlistSignupService, WaitlistSignupService>();

        return services;
    }

    public static WebApplication ApplyDatabaseMigrationsIfEnabled(this WebApplication app)
    {
        var autoMigrate = app.Configuration.GetValue<bool>("Database:AutoMigrate");

        if (!autoMigrate)
        {
            return app;
        }

        using var scope = app.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RoveltiaDbContext>>();
        using var dbContext = dbContextFactory.CreateDbContext();
        dbContext.Database.Migrate();
        EnsureWaitlistSchema(dbContext);

        return app;
    }

    private static void EnsureWaitlistSchema(RoveltiaDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('WaitlistSignups', 'UnsubscribeToken') IS NULL
            BEGIN
                ALTER TABLE [WaitlistSignups]
                ADD [UnsubscribeToken] nvarchar(64) NULL;
            END
            """);

        dbContext.Database.ExecuteSqlRaw("""
            UPDATE [WaitlistSignups]
            SET [UnsubscribeToken] = LOWER(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''))
            WHERE [UnsubscribeToken] IS NULL OR [UnsubscribeToken] = '';
            """);

        dbContext.Database.ExecuteSqlRaw("""
            IF EXISTS (
                SELECT 1
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'[WaitlistSignups]')
                  AND name = 'UnsubscribeToken'
                  AND is_nullable = 1
            )
            BEGIN
                ALTER TABLE [WaitlistSignups]
                ALTER COLUMN [UnsubscribeToken] nvarchar(64) NOT NULL;
            END
            """);

        dbContext.Database.ExecuteSqlRaw("""
            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'IX_WaitlistSignups_UnsubscribeToken'
                  AND object_id = OBJECT_ID(N'[WaitlistSignups]')
            )
            BEGIN
                CREATE UNIQUE INDEX [IX_WaitlistSignups_UnsubscribeToken]
                ON [WaitlistSignups]([UnsubscribeToken]);
            END
            """);
    }
}
