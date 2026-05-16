FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["ai-knowledge-assistant.Api/ai-knowledge-assistant.Api.csproj", "ai-knowledge-assistant.Api/"]
COPY ["ai-knowledge-assistant.Application/ai-knowledge-assistant.Application.csproj", "ai-knowledge-assistant.Application/"]
COPY ["ai-knowledge-assistant.Domain/ai-knowledge-assistant.Domain.csproj", "ai-knowledge-assistant.Domain/"]
COPY ["ai-knowledge-assistant.Infrastructure/ai-knowledge-assistant.Infrastructure.csproj", "ai-knowledge-assistant.Infrastructure/"]
RUN dotnet restore "ai-knowledge-assistant.Api/ai-knowledge-assistant.Api.csproj"

COPY . .
RUN dotnet publish "ai-knowledge-assistant.Api/ai-knowledge-assistant.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ai-knowledge-assistant.Api.dll"]
