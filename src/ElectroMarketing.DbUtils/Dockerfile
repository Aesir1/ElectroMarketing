# DU-5: migration job image. Build from the repository root: docker build -f src/ElectroMarketing.DbUtils/Dockerfile .
# Run: docker run --rm -e ConnectionStrings__app=... -e ConnectionStrings__auth=... -e ConnectionStrings__minio=... electromarketing-dbutils migrate --context All
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet publish src/ElectroMarketing.DbUtils/ElectroMarketing.DbUtils.csproj -c Release -a $TARGETARCH -o /app --no-self-contained

# aspnet, not runtime: the Auth project (AuthDbContext) depends on Microsoft.AspNetCore.App.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "ElectroMarketing.DbUtils.dll"]
CMD ["migrate", "--context", "All"]
