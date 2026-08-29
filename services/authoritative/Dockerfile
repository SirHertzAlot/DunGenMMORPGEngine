FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Pull latest security updates in the build stage to avoid vulnerable toolchain packages.
RUN apt-get update \
	&& apt-get -y upgrade \
	&& rm -rf /var/lib/apt/lists/*

# copy csproj and restore
COPY Authoritative.csproj ./
RUN dotnet restore

# copy rest
COPY . ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Apply OS-level security updates and install Docker CLI so ContainerHealthService
# can run `docker logs` via the mounted host socket (/var/run/docker.sock).
RUN apt-get update \
		&& apt-get -y upgrade \
		&& for pkg in libgcrypt20 libicu74 tar passwd shadow; do \
				 if apt-cache show "$pkg" >/dev/null 2>&1; then \
					 apt-get install --only-upgrade -y "$pkg"; \
				 fi; \
			 done \
		&& apt-get install -y --no-install-recommends docker.io \
		&& rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./
ENV RABBITMQ_HOST=rabbitmq
ENTRYPOINT ["dotnet", "Authoritative.dll"]
