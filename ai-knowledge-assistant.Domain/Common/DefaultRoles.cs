namespace ai_knowledge_assistant.Domain.Common;

public static class DefaultRoles
{
    public static readonly Guid AdminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid UserRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public const string Admin = "Admin";

    public const string User = "User";
}
