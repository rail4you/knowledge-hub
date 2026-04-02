FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && \
    apt-get install -y nodejs && \
    rm -rf /var/lib/apt/lists/*
RUN apt-get update && apt-get install -y openjdk-17-jre-headless && \
    rm -rf /var/lib/apt/lists/*
COPY bin/Release/net10.0/publish/ /app/
COPY utils/ /app/utils/
WORKDIR /app
RUN mkdir -p /app/uploads /tmp/office /tmp/opendataloader && \
    chmod +x /app/utils/pdf-parser/document-parser.jar
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "KnowledgeHub.HttpApi.Host.dll"]