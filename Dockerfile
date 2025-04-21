# مرحله Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# کپی فایل solution و پروژه
COPY Solvix.Server.sln .
COPY Solvix.Server/*.csproj ./Solvix.Server/

# restore با مسیر درست
RUN dotnet restore ./Solvix.Server/Solvix.Server.csproj

# کپی کل پروژه
COPY . .

# Build & Publish
WORKDIR /src/Solvix.Server
RUN dotnet publish -c Release -o /app/publish

# مرحله Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Railway پورت 8080 می‌خواد
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Solvix.Server.dll"]
