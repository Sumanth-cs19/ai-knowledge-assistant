namespace ai_knowledge_assistant.Application.DTOs.Chat;

public sealed record ChatAskRequest(
    string Question,
    Guid? ConversationId = null,
    Guid? DocumentId = null,
    IReadOnlyCollection<Guid>? SelectedDocumentIds = null);
