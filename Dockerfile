# ---- Compilación ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/LiquidacionSueldos.Web/LiquidacionSueldos.Web.csproj src/LiquidacionSueldos.Web/
RUN dotnet restore src/LiquidacionSueldos.Web/LiquidacionSueldos.Web.csproj
COPY src/ src/
RUN dotnet publish src/LiquidacionSueldos.Web/LiquidacionSueldos.Web.csproj -c Release -o /app/publish --no-restore

# ---- Ejecución ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=build /app/publish .
# Railway define PORT; Program.cs lo toma. Por defecto la imagen escucha en 8080.
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "LiquidacionSueldos.Web.dll"]
