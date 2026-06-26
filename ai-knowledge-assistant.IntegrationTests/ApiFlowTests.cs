using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ai_knowledge_assistant.Application.DTOs.Auth;
using ai_knowledge_assistant.Application.DTOs.Chat;
using ai_knowledge_assistant.Application.DTOs.Conversations;
using ai_knowledge_assistant.Application.DTOs.Documents;
using ai_knowledge_assistant.IntegrationTests.TestSupport;

namespace ai_knowledge_assistant.IntegrationTests;

public sealed class ApiFlowTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public ApiFlowTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_login_and_refresh_token_flow_succeeds()
    {
        var client = _factory.CreateClient();

        var email = $"user-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!"));
        await EnsureSuccessAsync(registerResponse);
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        await EnsureSuccessAsync(loginResponse);
        var loggedIn = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(loggedIn!.RefreshToken));
        await EnsureSuccessAsync(refreshResponse);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(registered);
        Assert.NotEqual(loggedIn.RefreshToken, refreshed!.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
    }

    [Fact]
    public async Task Document_upload_and_listing_succeeds_for_authenticated_user()
    {
        var client = _factory.CreateClient();
        await AuthenticateAsync(client);

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4 fake test pdf"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "test.pdf");

        var uploadResponse = await client.PostAsync("/api/documents/upload", content);
        await EnsureSuccessAsync(uploadResponse);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<DocumentResponse>();

        var listResponse = await client.GetAsync("/api/documents/my-documents");
        await EnsureSuccessAsync(listResponse);
        var documents = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<DocumentResponse>>();

        Assert.Equal("test.pdf", uploaded!.OriginalFileName);
        Assert.Contains(documents!, document => document.Id == uploaded.Id);
    }

    [Fact]
    public async Task Conversation_chat_and_feedback_flow_succeeds()
    {
        var client = _factory.CreateClient();
        await AuthenticateAsync(client);

        var conversationResponse = await client.PostAsJsonAsync("/api/conversations", new ConversationCreateRequest("Test conversation"));
        await EnsureSuccessAsync(conversationResponse);
        var conversation = await conversationResponse.Content.ReadFromJsonAsync<ConversationResponse>();

        var chatResponse = await client.PostAsJsonAsync(
            "/api/chat/ask",
            new ChatAskRequest("What does the uploaded document say?", conversation!.Id));
        await EnsureSuccessAsync(chatResponse);
        var chat = await chatResponse.Content.ReadFromJsonAsync<ChatResponse>();

        var feedbackResponse = await client.PostAsJsonAsync(
            $"/api/chat/messages/{chat!.AssistantMessageId}/feedback",
            new ChatFeedbackRequest(5, "Helpful"));
        await EnsureSuccessAsync(feedbackResponse);

        Assert.Equal(conversation.Id, chat.ConversationId);
        Assert.NotEmpty(chat.Citations);
    }

    [Fact]
    public async Task Admin_analytics_requires_admin_role()
    {
        var client = _factory.CreateClient();
        await AuthenticateAsync(client);

        var forbiddenResponse = await client.GetAsync("/api/admin/analytics/overview");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        await AuthenticateAsync(client, "admin@example.com", "Password123!");
        var adminResponse = await client.GetAsync("/api/admin/analytics/overview");
        await EnsureSuccessAsync(adminResponse);
    }

    private static async Task AuthenticateAsync(
        HttpClient client,
        string? email = null,
        string password = "Password123!")
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";

        if (email != "admin@example.com")
        {
            var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
            await EnsureSuccessAsync(registerResponse);
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        await EnsureSuccessAsync(loginResponse);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
    }
}
