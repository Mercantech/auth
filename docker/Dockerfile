FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Auth.API/Auth.API.csproj Auth.API/
RUN dotnet restore Auth.API/Auth.API.csproj
COPY src/Auth.API/ Auth.API/
WORKDIR /src/Auth.API
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Auth.API.dll"]
