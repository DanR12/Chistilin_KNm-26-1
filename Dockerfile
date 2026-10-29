FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/OrderFlow.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:5000
ENV ORDERFLOW_VARIANT=1
EXPOSE 5000
ENTRYPOINT ["dotnet", "OrderFlow.Api.dll"]
