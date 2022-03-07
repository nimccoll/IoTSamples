#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:3.1 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY ["DPSDeviceClientSample/DPSDeviceClientSample.csproj", "DPSDeviceClientSample/"]
RUN dotnet restore "DPSDeviceClientSample/DPSDeviceClientSample.csproj"
COPY . .
WORKDIR "/src/DPSDeviceClientSample"
RUN dotnet build "DPSDeviceClientSample.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DPSDeviceClientSample.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DPSDeviceClientSample.dll"]