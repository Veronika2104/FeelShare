# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/out

# ---- run stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/out .
# небольшой старт-скрипт будет выставлять нужный порт от Render
COPY start.sh /app/start.sh
RUN chmod +x /app/start.sh
# никаких EXPOSE не нужно — Render сам пробросит порт
CMD ["/app/start.sh"]
