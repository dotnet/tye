FROM mcr.microsoft.com/dotnet/core/runtime:3.1
WORKDIR /app

# We need `ps`
RUN apt-get update \
  && apt-get install -y procps \
  && rm -rf /var/lib/apt/lists/*

COPY . .
ENTRYPOINT ["dotnet", "tye-diag-agent.dll"]