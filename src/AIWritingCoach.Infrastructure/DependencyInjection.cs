using AIWritingCoach.Core.Ai;
using AIWritingCoach.Core.Diff;
using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Core.Services;
using AIWritingCoach.Core.Usage;
using AIWritingCoach.Infrastructure.Ai;
using AIWritingCoach.Infrastructure.Data;
using AIWritingCoach.Infrastructure.Diff;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIWritingCoach.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers database, repositories, AI pipeline and diff services. <c>ICurrentUser</c> is registered by the host.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddAppDatabase(configuration);
        services.AddSingleton<DataSeeder>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IRubricRepository, RubricRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IPromptTemplateRepository, PromptTemplateRepository>();
        services.AddScoped<IUsageLogger, UsageLogger>();

        services.AddSingleton<ITextDiffService, DiffPlexTextDiffService>();
        services.AddSingleton<IPromptRenderer, PromptRenderer>();
        services.AddScoped<WritingCoachService>();

        services.AddOpenRouterAi(configuration);
        return services;
    }

    private static void AddOpenRouterAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenRouterOptions>().Bind(configuration.GetSection(OpenRouterOptions.SectionName));
        services.AddHttpClient(OpenRouterPricing.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddSingleton<IModelPricing, OpenRouterPricing>();
        services.AddSingleton<OpenRouterRoutingChatClient>();

        // Scoped so usage rows are attributed to the current user.
        // Order (outer → inner): logging → retry/fallback → per-attempt usage logging → OpenRouter.
        services.AddScoped<IChatClient>(sp =>
            new ChatClientBuilder(sp.GetRequiredService<OpenRouterRoutingChatClient>())
                .UseLogging(sp.GetRequiredService<ILoggerFactory>())
                .Use(inner => new ModelFallbackChatClient(
                    inner,
                    sp.GetRequiredService<IOptions<OpenRouterOptions>>().Value,
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<ModelFallbackChatClient>>()))
                .Use(inner => new UsageLoggingChatClient(inner, sp.GetRequiredService<IUsageLogger>(), sp.GetRequiredService<IModelPricing>()))
                .Build(sp));

        services.AddScoped<IAiReviewService, AiReviewService>();
    }
}
