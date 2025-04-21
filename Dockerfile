FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR src


COPY .sln .
COPY Solvix.Server.csproj .Solvix.Server
RUN dotnet restore

COPY . .
WORKDIR srcSolvix.Server
RUN dotnet publish -c Release -o apppublish


FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR app
COPY --from=build apppublish .

ENV ASPNETCORE_URLS=http+8080
EXPOSE 8080

ENTRYPOINT [dotnet, Solvix.Server.dll]
