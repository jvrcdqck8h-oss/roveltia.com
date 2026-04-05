using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Roveltia.Web.Data;
using Roveltia.Web.Components;
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
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
            .SetApplicationName("Roveltia");
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
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        app.MapPost("/api/admin/waitlist/send", SendWaitlistCampaignAsync);

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
