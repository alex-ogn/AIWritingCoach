using System.Net;
using AIWritingCoach.Core.Users;

namespace AIWritingCoach.Web;

public sealed class AppOptions
{
    public const string SectionName = "App";

    public AppMode Mode { get; set; } = AppMode.Personal;

    /// <summary>Port used in Personal mode when no URL is configured.</summary>
    public int Port { get; set; } = 5155;

    /// <summary>Open the default browser on startup (for the single-file desktop build).</summary>
    public bool OpenBrowser { get; set; }
}

public static class Cultures
{
    public const string Default = "en";

    public static readonly string[] Supported = ["en", "bg"];
}

/// <summary>Personal mode has no authentication, so it must only ever listen on loopback.</summary>
public static class LocalhostBinding
{
    public static void Enforce(WebApplicationBuilder builder, int defaultPort)
    {
        var config = builder.Configuration;

        if (!string.IsNullOrWhiteSpace(config["http_ports"]) || !string.IsNullOrWhiteSpace(config["https_ports"]))
        {
            throw new InvalidOperationException(
                "Personal mode binds to localhost only; HTTP_PORTS/HTTPS_PORTS bind to all interfaces. Use ASPNETCORE_URLS=http://localhost:<port>.");
        }

        var urls = (config["urls"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(config.GetSection("Kestrel:Endpoints").GetChildren().Select(e => e["Url"]).OfType<string>())
            .ToList();

        var nonLocal = urls.Where(u => !IsLoopback(u)).ToList();
        if (nonLocal.Count > 0)
        {
            throw new InvalidOperationException(
                $"Personal mode binds to localhost only, but these URLs were configured: {string.Join(", ", nonLocal)}. " +
                "Switch App:Mode to MultiUser to expose the app on a network.");
        }

        if (urls.Count == 0)
        {
            builder.WebHost.UseUrls($"http://localhost:{defaultPort}");
        }
    }

    private static bool IsLoopback(string url)
    {
        // Kestrel accepts wildcards like http://*:5000 or http://+:5000, which Uri cannot parse.
        if (!Uri.TryCreate(url.Replace("://*", "://wildcard").Replace("://+", "://wildcard"), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsLoopback || (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip));
    }
}

/// <summary>Marker type for the shared <c>Resources/SharedResource*.resx</c> UI strings.</summary>
public sealed class SharedResource;
