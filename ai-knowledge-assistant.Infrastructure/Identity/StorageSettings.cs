namespace ai_knowledge_assistant.Infrastructure.Identity;

public sealed class StorageSettings
{
    public const string SectionName = "Storage";

    public string UploadsPath { get; init; } = "uploads";
}
