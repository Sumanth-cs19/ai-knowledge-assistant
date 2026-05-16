using ai_knowledge_assistant.Application.Features.Auth;
using ai_knowledge_assistant.Application.Features.Chat;
using ai_knowledge_assistant.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ai_knowledge_assistant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}
