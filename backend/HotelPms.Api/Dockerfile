FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/HotelPms.Api/HotelPms.Api.csproj backend/HotelPms.Api/
RUN dotnet restore backend/HotelPms.Api/HotelPms.Api.csproj
COPY backend/HotelPms.Api/ backend/HotelPms.Api/
WORKDIR /src/backend/HotelPms.Api
RUN dotnet publish HotelPms.Api.csproj --configuration Release --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "HotelPms.Api.dll"]
