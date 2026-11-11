FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/HotelPms.Api/HotelPms.Api.csproj backend/HotelPms.Api/
COPY backend/src/LotusPms.Domain/LotusPms.Domain.csproj backend/src/LotusPms.Domain/
COPY backend/src/LotusPms.Application/LotusPms.Application.csproj backend/src/LotusPms.Application/
COPY backend/src/LotusPms.Infrastructure/LotusPms.Infrastructure.csproj backend/src/LotusPms.Infrastructure/
RUN dotnet restore backend/HotelPms.Api/HotelPms.Api.csproj
COPY backend/HotelPms.Api/ backend/HotelPms.Api/
COPY backend/src/ backend/src/
WORKDIR /src/backend/HotelPms.Api
RUN dotnet publish HotelPms.Api.csproj --configuration Release --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "HotelPms.Api.dll"]
