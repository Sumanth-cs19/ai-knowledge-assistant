using System.Diagnostics;
using ai_knowledge_assistant.Application.Common;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Enums;
using ai_knowledge_assistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class DocumentProcessingWorker : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new(Observability.ActivitySourceName);
    private readonly IDocumentProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentProcessingWorker> _logger;

    public DocumentProcessingWorker(
        IDocumentProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var documentId = await _queue.DequeueAsync(stoppingToken);
            await ProcessAsync(documentId, stoppingToken);
        }
    }

    private async Task ProcessAsync(Guid documentId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("document.process");
        activity?.SetTag("document.id", documentId);
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var indexingService = scope.ServiceProvider.GetRequiredService<IDocumentIndexingService>();

        var document = await context.Documents.FirstOrDefaultAsync(document => document.Id == documentId, cancellationToken);
        if (document is null || document.IsDeleted)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Document processing started for {DocumentId}", documentId);
            document.Status = DocumentStatus.Processing;
            document.ErrorMessage = null;
            await context.SaveChangesAsync(cancellationToken);

            await indexingService.IndexAsync(document, cancellationToken);

            document.Status = DocumentStatus.Indexed;
            document.ProcessedAt = DateTime.UtcNow;
            document.ErrorMessage = null;
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Document processing completed for {DocumentId}", documentId);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _logger.LogError(exception, "Document processing failed for {DocumentId}", documentId);
            document.Status = DocumentStatus.Failed;
            document.ErrorMessage = exception.Message;
            document.ProcessedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }
}
