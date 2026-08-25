FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0

# SkiaSharp native dependencies for Linux
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 \
    libharfbuzz0b \
    libfreetype6 \
    libjpeg62-turbo \
    libpng16-16 \
    libtiff5 \
    libwebp7 \
    libgif7 \
    fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# Create directories for runtime data
RUN mkdir -p /app/data /app/wwwroot/posters /app/wwwroot/templates /app/wwwroot/logos

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV UseSqlite=true

ENTRYPOINT ["dotnet", "DailyPosterGenerator.dll"]
