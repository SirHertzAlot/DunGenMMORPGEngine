FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj and restore
COPY Authoritative.csproj ./
RUN dotnet restore

# copy rest
COPY . ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./
ARG APP_UID=1654
RUN mkdir -p /app/data && chown -R $APP_UID:0 /app
ENV RABBITMQ_HOST=rabbitmq
ENV AUTHORITATIVE_DATA_DIR=/app/data
ENV AUTHORITATIVE_DIAGNOSTIC_HTTP_PREFIXES=http://*:8080/
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Authoritative.dll"]
