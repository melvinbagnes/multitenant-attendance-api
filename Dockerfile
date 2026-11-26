FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AttendanceApi.slnx ./
COPY src/AttendanceApi/AttendanceApi.csproj src/AttendanceApi/
COPY tests/AttendanceApi.Tests/AttendanceApi.Tests.csproj tests/AttendanceApi.Tests/
RUN dotnet restore AttendanceApi.slnx

COPY . .
RUN dotnet publish src/AttendanceApi/AttendanceApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "AttendanceApi.dll"]
