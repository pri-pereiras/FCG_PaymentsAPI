FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY src/Payments.Domain/Payments.Domain.csproj src/Payments.Domain/
COPY src/Payments.Application/Payments.Application.csproj src/Payments.Application/
COPY src/Payments.Infrastructure/Payments.Infrastructure.csproj src/Payments.Infrastructure/
COPY src/Payments.API/Payments.API.csproj src/Payments.API/
RUN dotnet restore src/Payments.API/Payments.API.csproj
COPY src/ src/
RUN dotnet publish src/Payments.API/Payments.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Payments.API.dll"]
