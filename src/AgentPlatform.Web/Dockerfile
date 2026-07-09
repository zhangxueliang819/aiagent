FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["src/AgentPlatform.Web/AgentPlatform.Web.csproj", "src/AgentPlatform.Web/"]
COPY ["src/AgentPlatform.Application/AgentPlatform.Application.csproj", "src/AgentPlatform.Application/"]
COPY ["src/AgentPlatform.Core/AgentPlatform.Core.csproj", "src/AgentPlatform.Core/"]
COPY ["src/AgentPlatform.Infrastructure/AgentPlatform.Infrastructure.csproj", "src/AgentPlatform.Infrastructure/"]
COPY ["src/AgentPlatform.AgentEngine/AgentPlatform.AgentEngine.csproj", "src/AgentPlatform.AgentEngine/"]
COPY ["src/AgentPlatform.ModelProviders/AgentPlatform.ModelProviders.csproj", "src/AgentPlatform.ModelProviders/"]
COPY ["src/AgentPlatform.Session/AgentPlatform.Session.csproj", "src/AgentPlatform.Session/"]
COPY ["src/AgentPlatform.Memory/AgentPlatform.Memory.csproj", "src/AgentPlatform.Memory/"]
COPY ["src/AgentPlatform.Billing/AgentPlatform.Billing.csproj", "src/AgentPlatform.Billing/"]
RUN dotnet restore "src/AgentPlatform.Web/AgentPlatform.Web.csproj"

COPY . .
WORKDIR "/src/src/AgentPlatform.Web"
RUN dotnet build "AgentPlatform.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AgentPlatform.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AgentPlatform.Web.dll"]
