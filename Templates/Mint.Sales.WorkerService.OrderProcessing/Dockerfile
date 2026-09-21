# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY . .
RUN dotnet restore Mint.Sales.WorkerService.OrderProcessing.sln
RUN dotnet publish \
    src/Mint.Sales.WorkerService.OrderProcessing.Worker/Mint.Sales.WorkerService.OrderProcessing.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd --system app && useradd --system --gid app --create-home app
COPY --from=build --chown=app:app /app/publish .

USER app
ENV DOTNET_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "Mint.Sales.WorkerService.OrderProcessing.Worker.dll"]
