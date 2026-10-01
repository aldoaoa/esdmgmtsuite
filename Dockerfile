# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 1. Copiar el archivo de solución
COPY ESDSuite.slnx ./

# 2. Copiar los archivos .csproj de cada capa respetando la estructura de carpetas
COPY ESDSuite.Core/ESDSuite.Core.csproj ./ESDSuite.Core/
COPY ESDSuite.Services/ESDSuite.Services.csproj ./ESDSuite.Services/
COPY ESDSuite.Web/ESDSuite.Web.csproj ./ESDSuite.Web/

# 3. Restaurar las dependencias de toda la solución
RUN dotnet restore ESDSuite.slnx

# 4. Copiar el resto del código fuente
COPY . ./

# 5. Compilar y publicar el proyecto web (el que tiene tu Program.cs)
WORKDIR /app/ESDSuite.Web
RUN dotnet publish ESDSuite.Web.csproj -c Release -o /app/out

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copiar los archivos compilados de la etapa anterior
COPY --from=build /app/out .

# Configurar el puerto interno de la aplicación
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Ejecutar el backend
ENTRYPOINT ["dotnet", "ESDSuite.Web.dll"]
