using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_knowledge_assistant.Infrastructure.Persistence;

public sealed class ChatFeedbackRepository : IChatFeedbackRepository
{
    private readonly ApplicationDbContext _context;

    public ChatFeedbackRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<ChatMessage?> GetOwnedAssistantMessageAsync(
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        return _context.ChatMessages
            .Include(message => message.Conversation)
            .FirstOrDefaultAsync(
                message => message.Id == messageId
                    && message.Role == ChatMessageRole.Assistant
                    && message.Conversation != null
                    && message.Conversation.UserId == userId,
                cancellationToken);
    }

    public async Task<ChatFeedback> AddAsync(ChatFeedback feedback, CancellationToken cancellationToken = default)
    {
        _context.ChatFeedback.Add(feedback);
        await _context.SaveChangesAsync(cancellationToken);
        return feedback;
    }
}
