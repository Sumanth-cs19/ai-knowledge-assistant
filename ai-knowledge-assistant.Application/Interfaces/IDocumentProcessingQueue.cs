namespace ai_knowledge_assistant.Application.Interfaces;

public interface IDocumentProcessingQueue
{
    ValueTask QueueAsync(Guid documentId, CancellationToken cancellationToken = default);

    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default);
}
