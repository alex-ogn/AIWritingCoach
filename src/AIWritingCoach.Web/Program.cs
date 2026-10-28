using System.Diagnostics;
using AIWritingCoach.Core.Users;
using AIWritingCoach.Infrastructure;
using AIWritingCoach.Infrastructure.Data;
using AIWritingCoach.Web;
using AIWritingCoach.Web.Components;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var appOptions = builder.Configuration.GetSection(AppOptions.SectionName).Get<AppOptions>() ?? new AppOptions();
builder.Services.AddSingleton(appOptions);

switch (appOptions.Mode)
{
    case AppMode.Personal:
        // One fixed local user, no login; never reachable from the network.
        LocalhostBinding.Enforce(builder, appOptions.Port);
        var personalUser = builder.Configuration.GetSection("App:PersonalUser").Get<PersonalUserOptions>() ?? new PersonalUserOptions();
        builder.Services.AddSingleton(personalUser);
        builder.Services.AddSingleton<ICurrentUser, PersonalCurrentUser>();
        break;

    case AppMode.MultiUser:
        // Planned: ASP.NET Core Identity / OIDC with a claims-based ICurrentUser, usage limits and payments.
        // The schema (Identity tables, UserId on all user data) is already in place.
        throw new NotSupportedException("App:Mode 'MultiUser' is not implemented yet. Use 'Personal'.");

    default:
        throw new InvalidOperationException($"Unknown App:Mode '{appOptions.Mode}'.");
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
// Bottom-right so notifications never cover the Write toolbar.
builder.Services.AddMudServices(o => o.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(Cultures.Default)
    .AddSupportedCultures(Cultures.Supported)
    .AddSupportedUICultures(Cultures.Supported));

app.UseAntiforgery();

app.MapStaticAssets();

// Sets the culture cookie and returns to the page (UI language switch in Settings).
app.MapGet("/culture/set", (string culture, string? redirectUri, HttpContext context) =>
{
    if (Cultures.Supported.Contains(culture))
    {
        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });
    }

    return Results.LocalRedirect(string.IsNullOrEmpty(redirectUri) || !redirectUri.StartsWith('/') ? "/" : redirectUri);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (appOptions.OpenBrowser)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = app.Urls.FirstOrDefault() ?? $"http://localhost:{appOptions.Port}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    });
}

await app.RunAsync();
