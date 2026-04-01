# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM registry.minnaloushe.net/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
VOLUME /data


# This stage is used to build the service project
FROM registry.minnaloushe.net/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["NuGet.config", "."]
COPY ["src/Templates/Template.WebApi.Host/Template.WebApi.Host.csproj", "src/Templates/Template.WebApi.Host/"]
RUN dotnet restore "./src/Templates/Template.WebApi.Host/Template.WebApi.Host.csproj"
COPY . .
WORKDIR "/src/src/Templates/Template.WebApi.Host"
RUN dotnet build "./Template.WebApi.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Template.WebApi.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Template.WebApi.Host.dll"]