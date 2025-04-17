# Use the official .NET 9 runtime image for the final image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

# Set the working directory in the container
WORKDIR /app

# Copy the published app from the build stage
COPY /bin/publish .

# Expose the port the app will run on
EXPOSE 80

# Instruct ASP.NET core to bind to port 80
ENV ASPNETCORE_HTTP_PORTS=80

# Set the entry point for the application
ENTRYPOINT ["dotnet", "NetDocuments.dll"]
