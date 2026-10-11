FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY src/PaymentsAPI/PaymentsAPI.csproj src/PaymentsAPI/
RUN dotnet restore src/PaymentsAPI/PaymentsAPI.csproj
COPY src/PaymentsAPI/ src/PaymentsAPI/
RUN dotnet publish src/PaymentsAPI/PaymentsAPI.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaymentsAPI.dll"]
