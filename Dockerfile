FROM mcr.microsoft.com/dotnet/core/sdk:2.2-alpine3.9 as sdk
WORKDIR /sdk
RUN apk add bash && \
    apk add --no-cache mono \
       --repository http://dl-cdn.alpinelinux.org/alpine/edge/testing && \
    apk add --no-cache yarn \
       --repository http://dl-cdn.alpinelinux.org/alpine/edge/testing && \
    apk add --no-cache --virtual=.build-dependencies ca-certificates && \
    cert-sync /etc/ssl/certs/ca-certificates.crt && \
    apk del .build-dependencies
COPY .paket/ .paket/
COPY paket.dependencies .
COPY paket.lock .
RUN mono .paket/paket.exe restore

FROM sdk as build
WORKDIR /build
COPY ./ ./
RUN rm -rf ./obj ./src/*/bin ./src/*/obj ./.fake
RUN dotnet restore build.proj; dotnet fake build
RUN dotnet publish -c Release -o /build/deploy ./src/Server/Server.fsproj
RUN cp -r ./src/Client/deploy ./deploy/public

FROM mcr.microsoft.com/dotnet/core/aspnet:2.2
WORKDIR /app
ENV PRODUCTION 1
ENV SERVER_PORT 80
COPY --from=build /build/deploy .
CMD dotnet ./Server.dll

