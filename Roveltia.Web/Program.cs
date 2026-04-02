using Microsoft.EntityFrameworkCore;
using Roveltia.Web.Components;
using Roveltia.Web.Data;
using Roveltia.Web.Services;

namespace Roveltia.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddDbContext<RoveltiaDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddScoped<IWaitlistSignupService, WaitlistSignupService>();

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();

        if (app.Configuration.GetValue("Database:AutoMigrate", false))
        {
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<RoveltiaDbContext>().Database.Migrate();
        }

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}
