using ai_knowledge_assistant.Application.DTOs.Common;
using ai_knowledge_assistant.Application.DTOs.Conversations;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;

namespace ai_knowledge_assistant.Application.Features.Conversations;

public sealed class ConversationService : IConversationService
{
    private const int MaxPageSize = 100;
    private readonly IConversationRepository _conversationRepository;

    public ConversationService(IConversationRepository conversationRepository)
    {
        _conversationRepository = conversationRepository;
    }

    public async Task<ConversationResponse> CreateAsync(
        Guid userId,
        ConversationCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            UserId = userId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "New conversation" : request.Title.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _conversationRepository.AddAsync(conversation, cancellationToken);
        return ToResponse(conversation);
    }

    public async Task<PagedResponse<ConversationResponse>> GetConversationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        var (safePage, safePageSize) = NormalizePaging(page, pageSize);
        var conversations = await _conversationRepository.GetOwnedConversationsAsync(
            userId,
            safePage,
            safePageSize,
            cancellationToken);

        return new PagedResponse<ConversationResponse>(
            conversations.Items.Select(ToResponse).ToList(),
            conversations.Page,
            conversations.PageSize,
            conversations.TotalCount);
    }

    public async Task<ConversationResponse> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var conversation = await GetOwnedConversationAsync(userId, id, cancellationToken);
        return ToResponse(conversation);
    }

    public async Task<ConversationResponse> UpdateAsync(
        Guid userId,
        Guid id,
        ConversationUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Title)] = ["Conversation title is required."]
            });
        }

        var conversation = await GetOwnedConversationAsync(userId, id, cancellationToken);
        conversation.Title = request.Title.Trim();
        conversation.UpdatedAt = DateTime.UtcNow;
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        return ToResponse(conversation);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var conversation = await GetOwnedConversationAsync(userId, id, cancellationToken);
        conversation.IsDeleted = true;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
    }

    public async Task ArchiveAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var conversation = await GetOwnedConversationAsync(userId, id, cancellationToken);
        conversation.IsArchived = true;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
    }

    public async Task<PagedResponse<ChatMessageResponse>> GetMessagesAsync(
        Guid userId,
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        var (safePage, safePageSize) = NormalizePaging(page, pageSize);
        var messages = await _conversationRepository.GetOwnedMessagesAsync(
            userId,
            conversationId,
            safePage,
            safePageSize,
            cancellationToken);

        return new PagedResponse<ChatMessageResponse>(
            messages.Items.Select(ToMessageResponse).ToList(),
            messages.Page,
            messages.PageSize,
            messages.TotalCount);
    }

    private async Task<Conversation> GetOwnedConversationAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        ValidateUser(userId);

        var conversation = await _conversationRepository.GetOwnedAsync(userId, id, cancellationToken);
        return conversation ?? throw new NotFoundException("Conversation was not found.");
    }

    private static void ValidateUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedRequestException("Authenticated user id is missing or invalid.");
        }
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        return (Math.Max(page, 1), Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, MaxPageSize));
    }

    private static ConversationResponse ToResponse(Conversation conversation)
    {
        return new ConversationResponse(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            conversation.IsArchived);
    }

    private static ChatMessageResponse ToMessageResponse(ChatMessage message)
    {
        return new ChatMessageResponse(
            message.Id,
            message.ConversationId,
            message.Role,
            message.Content,
            message.TokenCount,
            message.CreatedAt);
    }
}
