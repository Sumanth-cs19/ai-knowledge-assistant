using System.Threading.Channels;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class DocumentProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();

    public ValueTask QueueAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return _queue.Writer.WriteAsync(documentId, cancellationToken);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }
}
