# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
WORKDIR /src/src/IForm.Web
RUN dotnet publish IForm.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

RUN mkdir -p /app/storage /app/logs

COPY --from=build /app/publish .
COPY src/IForm.Web/storage /app/storage

ENV ASPNETCORE_ENVIRONMENT=Production

CMD ["sh", "-c", "export ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080}; mkdir -p /app/data /app/logs; dotnet IForm.Web.dll"]
