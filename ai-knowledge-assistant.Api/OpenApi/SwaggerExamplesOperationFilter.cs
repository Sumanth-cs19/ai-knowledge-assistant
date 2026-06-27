using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ai_knowledge_assistant.Api.OpenApi;

public sealed class SwaggerExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath?.Split('?')[0] ?? string.Empty;
        var method = context.ApiDescription.HttpMethod;

        if (method == "POST" && path.EndsWith("/auth/register", StringComparison.OrdinalIgnoreCase))
        {
            SetJsonRequestExample(operation, new OpenApiObject
            {
                ["email"] = new OpenApiString("developer@example.com"),
                ["password"] = new OpenApiString("StrongPass123!")
            });
            SetAuthResponseExample(operation, "201");
        }
        else if (method == "POST" && path.EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            SetJsonRequestExample(operation, new OpenApiObject
            {
                ["email"] = new OpenApiString("developer@example.com"),
                ["password"] = new OpenApiString("StrongPass123!")
            });
            SetAuthResponseExample(operation, "200");
        }
        else if (method == "POST" && path.EndsWith("/documents/upload", StringComparison.OrdinalIgnoreCase))
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Description = "Upload one PDF or DOCX file using the field name 'file'.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Required = new HashSet<string> { "file" },
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["file"] = new()
                                {
                                    Type = "string",
                                    Format = "binary",
                                    Description = "A non-empty .pdf or .docx file."
                                }
                            }
                        }
                    }
                }
            };
        }
        else if (method == "POST" && path.EndsWith("/chat/ask", StringComparison.OrdinalIgnoreCase))
        {
            SetJsonRequestExample(operation, new OpenApiObject
            {
                ["question"] = new OpenApiString("What are the main points in my uploaded document?"),
                ["conversationId"] = new OpenApiNull()
            });
        }
        else if (method == "POST"
                 && path.Contains("/chat/messages/", StringComparison.OrdinalIgnoreCase)
                 && path.EndsWith("/feedback", StringComparison.OrdinalIgnoreCase))
        {
            SetJsonRequestExample(operation, new OpenApiObject
            {
                ["rating"] = new OpenApiInteger(5),
                ["comment"] = new OpenApiString("The answer was accurate and the citations were useful.")
            });
        }
    }

    private static void SetJsonRequestExample(OpenApiOperation operation, IOpenApiAny example)
    {
        if (operation.RequestBody?.Content.TryGetValue("application/json", out var mediaType) == true)
        {
            mediaType.Example = example;
        }
    }

    private static void SetAuthResponseExample(OpenApiOperation operation, string statusCode)
    {
        if (operation.Responses.TryGetValue(statusCode, out var response)
            && response.Content.TryGetValue("application/json", out var mediaType))
        {
            mediaType.Example = new OpenApiObject
            {
                ["accessToken"] = new OpenApiString("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."),
                ["refreshToken"] = new OpenApiString("sample-refresh-token"),
                ["email"] = new OpenApiString("developer@example.com"),
                ["accessTokenExpiresAt"] = new OpenApiString("2026-06-27T12:15:00Z"),
                ["refreshTokenExpiresAt"] = new OpenApiString("2026-07-04T12:00:00Z")
            };
        }
    }
}
