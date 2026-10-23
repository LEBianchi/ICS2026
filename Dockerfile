# syntax=docker/dockerfile:1

# Etapa 1: compilar y publicar con el SDK completo
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Dsw2025Tpi.Api/Dsw2025Tpi.Api.csproj -c Release -o /app/publish

# Etapa 2: imagen liviana, solo con el runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Dsw2025Tpi.Api.dll"]