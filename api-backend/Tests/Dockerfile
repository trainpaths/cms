FROM mcr.microsoft.com/dotnet/sdk:10.0 AS test

WORKDIR /src

COPY api-backend/api-backend.csproj api-backend/
COPY api-backend/Tests/Tests.csproj api-backend/Tests/

RUN dotnet restore api-backend/Tests/Tests.csproj

COPY api-backend/ api-backend/
# Opts dotnet test into Microsoft.Testing.Platform (required by xunit.v3 4.x).
COPY global.json ./

WORKDIR /src/api-backend/Tests
ENTRYPOINT ["dotnet", "test", "--project", "Tests.csproj", "--no-restore"]
