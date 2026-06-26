using ai_knowledge_assistant.Application.DTOs.Admin;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Enums;
using ai_knowledge_assistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class AdminAnalyticsService : IAdminAnalyticsService
{
    private static readonly TimeSpan ActiveUserWindow = TimeSpan.FromDays(30);
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminAnalyticsService> _logger;

    public AdminAnalyticsService(ApplicationDbContext context, ILogger<AdminAnalyticsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin analytics overview requested");

        var totalUsers = await _context.Users.AsNoTracking().CountAsync(cancellationToken);
        var activeUsers = await CountActiveUsersAsync(cancellationToken);
        var totalDocuments = await ActiveDocuments.CountAsync(cancellationToken);
        var indexedDocuments = await ActiveDocuments.CountAsync(document => document.Status == DocumentStatus.Indexed, cancellationToken);
        var failedDocuments = await ActiveDocuments.CountAsync(document => document.Status == DocumentStatus.Failed, cancellationToken);
        var totalConversations = await ActiveConversations.CountAsync(cancellationToken);
        var totalChatMessages = await _context.ChatMessages.AsNoTracking().CountAsync(cancellationToken);
        var averageFeedbackRating = await GetAverageFeedbackRatingAsync(cancellationToken);

        return new AnalyticsOverviewResponse(
            totalUsers,
            activeUsers,
            totalDocuments,
            indexedDocuments,
            failedDocuments,
            totalConversations,
            totalChatMessages,
            averageFeedbackRating);
    }

    public async Task<UserAnalyticsResponse> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin user analytics requested");
        var now = DateTime.UtcNow;
        var totalUsers = await _context.Users.AsNoTracking().CountAsync(cancellationToken);

        return new UserAnalyticsResponse(
            totalUsers,
            await CountActiveUsersAsync(cancellationToken),
            await _context.Users.AsNoTracking().CountAsync(user => user.CreatedAt >= now.AddDays(-7), cancellationToken),
            await _context.Users.AsNoTracking().CountAsync(user => user.CreatedAt >= now.AddDays(-30), cancellationToken));
    }

    public async Task<DocumentAnalyticsResponse> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin document analytics requested");

        var failures = await ActiveDocuments
            .Where(document => document.Status == DocumentStatus.Failed)
            .OrderByDescending(document => document.ProcessedAt ?? document.UploadedAt)
            .Take(10)
            .Select(document => new DocumentProcessingFailureResponse(
                document.Id,
                document.OriginalFileName,
                document.ErrorMessage,
                document.UploadedAt,
                document.ProcessedAt))
            .ToListAsync(cancellationToken);

        return new DocumentAnalyticsResponse(
            await ActiveDocuments.CountAsync(cancellationToken),
            await ActiveDocuments.CountAsync(document => document.Status == DocumentStatus.Pending, cancellationToken),
            await ActiveDocuments.CountAsync(document => document.Status == DocumentStatus.Processing, cancellationToken),
            await ActiveDocuments.CountAsync(document => document.Status == DocumentStatus.Indexed, cancellationToken),
            await ActiveDocuments.CountAsync(document => document.Status == DocumentStatus.Failed, cancellationToken),
            await GetMostUsedDocumentsInCitationsAsync(cancellationToken),
            failures);
    }

    public async Task<ChatAnalyticsResponse> GetChatsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin chat analytics requested");
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        return new ChatAnalyticsResponse(
            await ActiveConversations.CountAsync(cancellationToken),
            await ActiveConversations.CountAsync(conversation => conversation.IsArchived, cancellationToken),
            await _context.ChatMessages.AsNoTracking().CountAsync(cancellationToken),
            await _context.ChatMessages.AsNoTracking().CountAsync(message => message.Role == ChatMessageRole.User, cancellationToken),
            await _context.ChatMessages.AsNoTracking().CountAsync(message => message.Role == ChatMessageRole.Assistant, cancellationToken),
            await ActiveConversations.CountAsync(conversation => conversation.CreatedAt >= sevenDaysAgo, cancellationToken));
    }

    public async Task<FeedbackAnalyticsResponse> GetFeedbackAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin feedback analytics requested");

        var breakdown = await _context.ChatFeedback
            .AsNoTracking()
            .GroupBy(feedback => feedback.Rating)
            .OrderBy(group => group.Key)
            .Select(group => new FeedbackRatingBreakdownResponse(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        return new FeedbackAnalyticsResponse(
            await _context.ChatFeedback.AsNoTracking().CountAsync(cancellationToken),
            await GetAverageFeedbackRatingAsync(cancellationToken),
            await _context.ChatFeedback.AsNoTracking().CountAsync(feedback => feedback.Rating >= 4, cancellationToken),
            await _context.ChatFeedback.AsNoTracking().CountAsync(feedback => feedback.Rating <= 2, cancellationToken),
            breakdown);
    }

    private IQueryable<Domain.Entities.Document> ActiveDocuments =>
        _context.Documents.AsNoTracking().Where(document => !document.IsDeleted);

    private IQueryable<Domain.Entities.Conversation> ActiveConversations =>
        _context.Conversations.AsNoTracking().Where(conversation => !conversation.IsDeleted);

    private async Task<int> CountActiveUsersAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.Subtract(ActiveUserWindow);
        return await _context.Users
            .AsNoTracking()
            .CountAsync(user =>
                user.CreatedAt >= cutoff ||
                user.Documents.Any(document => !document.IsDeleted && document.UploadedAt >= cutoff) ||
                user.Conversations.Any(conversation => !conversation.IsDeleted && conversation.UpdatedAt >= cutoff) ||
                user.RefreshTokens.Any(refreshToken => refreshToken.CreatedAt >= cutoff),
                cancellationToken);
    }

    private async Task<double?> GetAverageFeedbackRatingAsync(CancellationToken cancellationToken)
    {
        if (!await _context.ChatFeedback.AsNoTracking().AnyAsync(cancellationToken))
        {
            return null;
        }

        return Math.Round(await _context.ChatFeedback.AsNoTracking().AverageAsync(feedback => feedback.Rating, cancellationToken), 2);
    }

    private async Task<IReadOnlyCollection<MostUsedDocumentResponse>> GetMostUsedDocumentsInCitationsAsync(CancellationToken cancellationToken)
    {
        var documents = await ActiveDocuments
            .Select(document => new { document.Id, document.OriginalFileName })
            .ToListAsync(cancellationToken);

        if (documents.Count == 0)
        {
            return [];
        }

        var assistantMessages = await _context.ChatMessages
            .AsNoTracking()
            .Where(message => message.Role == ChatMessageRole.Assistant)
            .Select(message => message.Content)
            .ToListAsync(cancellationToken);

        return documents
            .Select(document => new MostUsedDocumentResponse(
                document.Id,
                document.OriginalFileName,
                assistantMessages.Count(message =>
                    message.Contains(document.OriginalFileName, StringComparison.OrdinalIgnoreCase))))
            .Where(document => document.CitationCount > 0)
            .OrderByDescending(document => document.CitationCount)
            .ThenBy(document => document.OriginalFileName)
            .Take(10)
            .ToList();
    }
}
