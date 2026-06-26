using ai_knowledge_assistant.Application.DTOs.Common;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ai_knowledge_assistant.Infrastructure.Persistence;

public sealed class ConversationRepository : IConversationRepository
{
    private readonly ApplicationDbContext _context;

    public ConversationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Conversation> AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public Task<Conversation?> GetOwnedAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Conversations
            .FirstOrDefaultAsync(
                conversation => conversation.Id == id
                    && conversation.UserId == userId
                    && !conversation.IsDeleted,
                cancellationToken);
    }

    public async Task<PagedResponse<Conversation>> GetOwnedConversationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.UserId == userId && !conversation.IsDeleted)
            .OrderByDescending(conversation => conversation.UpdatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<Conversation>(items, page, pageSize, totalCount);
    }

    public async Task<PagedResponse<ChatMessage>> GetOwnedMessagesAsync(
        Guid userId,
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var conversationExists = await _context.Conversations.AnyAsync(
            conversation => conversation.Id == conversationId
                && conversation.UserId == userId
                && !conversation.IsDeleted,
            cancellationToken);

        if (!conversationExists)
        {
            return new PagedResponse<ChatMessage>([], page, pageSize, 0);
        }

        var query = _context.ChatMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<ChatMessage>(items, page, pageSize, totalCount);
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        _context.Conversations.Update(conversation);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddMessagesAsync(
        Conversation conversation,
        IReadOnlyCollection<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        _context.Conversations.Update(conversation);
        _context.ChatMessages.AddRange(messages);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
