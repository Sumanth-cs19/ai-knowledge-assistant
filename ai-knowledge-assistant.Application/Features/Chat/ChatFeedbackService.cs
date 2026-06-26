using ai_knowledge_assistant.Application.DTOs.Chat;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ai_knowledge_assistant.Application.Features.Chat;

public sealed class ChatFeedbackService : IChatFeedbackService
{
    private readonly IChatFeedbackRepository _feedbackRepository;
    private readonly ILogger<ChatFeedbackService> _logger;

    public ChatFeedbackService(IChatFeedbackRepository feedbackRepository, ILogger<ChatFeedbackService> logger)
    {
        _feedbackRepository = feedbackRepository;
        _logger = logger;
    }

    public async Task<ChatFeedbackResponse> SubmitAsync(
        Guid userId,
        Guid messageId,
        ChatFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(userId, request);

        var message = await _feedbackRepository.GetOwnedAssistantMessageAsync(userId, messageId, cancellationToken)
            ?? throw new NotFoundException("Chat message was not found.");

        var feedback = new ChatFeedback
        {
            ChatMessageId = message.Id,
            UserId = userId,
            Rating = request.Rating,
            Comment = request.Comment?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _feedbackRepository.AddAsync(feedback, cancellationToken);
        _logger.LogInformation("Feedback submitted for message {MessageId} by user {UserId}", messageId, userId);

        return new ChatFeedbackResponse(feedback.Id, feedback.ChatMessageId, feedback.Rating, feedback.Comment, feedback.CreatedAt);
    }

    private static void Validate(Guid userId, ChatFeedbackRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (userId == Guid.Empty)
        {
            errors[nameof(userId)] = ["An authenticated user is required."];
        }

        if (request.Rating < 1 || request.Rating > 5)
        {
            errors[nameof(request.Rating)] = ["Rating must be between 1 and 5."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
