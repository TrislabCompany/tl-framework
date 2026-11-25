# The test host as an image, for the performance and security runs in CI (README.md, "Run it as a
# container"). Same shape as a product's API image (tl-product-saas): stage 1 publishes with the
# .NET SDK, stage 2 is the chiseled ASP.NET runtime with a non-root user, on port 8080. Both base
# images are pinned by digest; Renovate keeps the digests current. Never pushed, never deployed.
# The build context is the repository root:
#   docker build -f src/Tests/TestHost/Dockerfile -t tl-test-host .

FROM mcr.microsoft.com/dotnet/sdk:11.0.100-resolute@sha256:2794448a07118a6eaf00f29ab5d2bf44928517ce5ff86505a63a69230374f599 AS build
WORKDIR /repo
# The whole of src/ but the other tests (Dockerfile.dockerignore): Directory.Build.props and
# Directory.Packages.props hold the versions, and the host references every module.
COPY global.json ./
COPY src/ src/
RUN dotnet restore src/Tests/TestHost/TestHost.csproj
# IsPublishable is false in the project so nothing publishes it by accident; this build asks for it.
RUN dotnet publish src/Tests/TestHost/TestHost.csproj --configuration Release --no-restore \
    -p:IsPublishable=true --output /app

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-resolute-chiseled@sha256:045ba65412b8edcc2f70a779163bf4353d450f72535a0dddf6188d6d945bfaef
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Trislab.TestHost.dll"]
