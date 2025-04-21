FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src


COPY Solvix.Server/*.csproj ./Solvix.Server/
RUN dotnet restore ./Solvix.Server.csproj

COPY . .


WORKDIR /src/Solvix.Server
RUN dotnet publish -c Release -o /app/publish


FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Solvix.Server.dll"]
