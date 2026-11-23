FROM node:22.14.0-bookworm-slim AS client
WORKDIR /client
COPY project-web-connectingbook.client/package*.json ./
RUN npm ci
COPY project-web-connectingbook.client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0.203-bookworm-slim AS build
WORKDIR /src
COPY Project-Web-ConnectingBook.Server/Project-Web-ConnectingBook.Server.csproj Project-Web-ConnectingBook.Server/
COPY Project-Web-ConnectingBook.Server/packages.lock.json Project-Web-ConnectingBook.Server/
RUN dotnet restore Project-Web-ConnectingBook.Server/Project-Web-ConnectingBook.Server.csproj -p:BuildClient=false
COPY Project-Web-ConnectingBook.Server/ Project-Web-ConnectingBook.Server/
RUN dotnet publish Project-Web-ConnectingBook.Server/Project-Web-ConnectingBook.Server.csproj -c Release -p:BuildClient=false -o /publish --no-restore
COPY --from=client /client/dist/ /publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:9.0.15-bookworm-slim AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg libfontconfig1 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /publish/ ./
RUN mkdir -p /app/.local && chown -R app:app /app
USER app
EXPOSE 8443
ENTRYPOINT ["dotnet", "Project-Web-ConnectingBook.Server.dll"]
