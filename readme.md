# IIIF Auth 2

IIIF Auth v2 handles [IIIF Authorization Flow API 2.0](https://iiif.io/api/auth/2.0/) requests and is an implmementation of [DLCS RFC 012](https://github.com/dlcs/protagonist/blob/31bcd7db4d4856620b44b03e63d91d11e6832c62/docs/rfcs/012-auth-service.md)

## Running

There is a Dockerfile and docker-compose file for running app:

```bash
# build docker image
docker build -t iiif-auth-2:local .  

# run image
docker run -it --rm \
    --name iiif-auth-2 \
    -p "8014:80" \
    iiif-auth-2:local

# run via docker-compose
docker compose up
```

## Migrations

```bash
cd src/IIIFAuth2

dotnet ef migrations add "initial" -p IIIFAuth2.API -o Data/Migrations
```