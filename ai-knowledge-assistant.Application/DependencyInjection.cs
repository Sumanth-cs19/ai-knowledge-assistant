using ai_knowledge_assistant.Application.Features.Auth;
using ai_knowledge_assistant.Application.Features.Admin;
using ai_knowledge_assistant.Application.Features.Chat;
using ai_knowledge_assistant.Application.Features.Conversations;
using ai_knowledge_assistant.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ai_knowledge_assistant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IChatFeedbackService, ChatFeedbackService>();
        services.AddScoped<IConversationService, ConversationService>();

        return services;
    }
}
