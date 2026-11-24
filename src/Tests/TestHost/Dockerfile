# The test host as an image, for the performance and security runs in CI (README.md, "Run it as a
# container"). Same shape as a product's API image (tl-product-saas): stage 1 publishes with the
# .NET SDK, stage 2 is the chiseled ASP.NET runtime with a non-root user, on port 8080. Both base
# images are pinned by digest; Renovate keeps the digests current. Never pushed, never deployed.
# The build context is the repository root:
#   docker build -f src/Tests/TestHost/Dockerfile -t tl-test-host .

FROM mcr.microsoft.com/dotnet/sdk:10.0.404-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /repo
# The whole of src/ but the other tests (Dockerfile.dockerignore): Directory.Build.props and
# Directory.Packages.props hold the versions, and the host references every module.
COPY global.json ./
COPY src/ src/
RUN dotnet restore src/Tests/TestHost/TestHost.csproj
# IsPublishable is false in the project so nothing publishes it by accident; this build asks for it.
RUN dotnet publish src/Tests/TestHost/TestHost.csproj --configuration Release --no-restore \
    -p:IsPublishable=true --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled@sha256:7c625e44e5d588b4d7b2a913e94ca4bd5098ce47442da404829e4c7936a764e1
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Trislab.TestHost.dll"]
