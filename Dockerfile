# Build stage image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
WORKDIR "/src"
# Obligation calculation tests start MongoDB through Testcontainers. A docker
# build has no Docker daemon, so those tests cannot run here. The pull request
# workflow runs the full suite with dotnet test, where Docker is available.
RUN dotnet build WasteBatteriesObligationsBe.slnx -c Debug \
 && dotnet tests/WasteBatteriesObligationsBe.Tests.Unit/bin/Debug/net10.0/WasteBatteriesObligationsBe.Tests.Unit.dll \
 && dotnet tests/WasteBatteriesObligationsBe.Tests.Integration/bin/Debug/net10.0/WasteBatteriesObligationsBe.Tests.Integration.dll \
      -namespace- WasteBatteriesObligationsBe.Tests.Integration.ObligationCalculation
RUN dotnet publish src/WasteBatteriesObligationsBe -c Release -o /app/publish /p:UseAppHost=false

# Final production image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

USER root
# Add curl to template, CDP PLATFORM HEALTHCHECK REQUIREMENT
RUN apt update && \
    apt install curl -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8085
ENTRYPOINT ["dotnet", "WasteBatteriesObligationsBe.dll"]
