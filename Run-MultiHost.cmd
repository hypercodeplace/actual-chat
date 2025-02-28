@echo off
dotnet build

set ASPNETCORE_ENVIRONMENT=Development

set ASPNETCORE_URLS=http://localhost:7080/
start cmd /C timeout 5 ^& start http://localhost:7080/"
start cmd /C dotnet run --no-launch-profile --project src/dotnet/Host/Host.csproj

set ASPNETCORE_URLS=http://localhost:7081/
start cmd /C timeout 5 ^& start http://localhost:7081/"
start cmd /C dotnet run --no-launch-profile --project src/dotnet/Host/Host.csproj
