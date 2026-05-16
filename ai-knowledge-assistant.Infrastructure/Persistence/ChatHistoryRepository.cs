using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ai_knowledge_assistant.Infrastructure.Persistence;

public sealed class ChatHistoryRepository : IChatHistoryRepository
{
    private readonly ApplicationDbContext _context;

    public ChatHistoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChatMessage>> GetUserHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ChatMessages
            .AsNoTracking()
            .Where(message => message.UserId == userId)
            .OrderByDescending(message => message.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
