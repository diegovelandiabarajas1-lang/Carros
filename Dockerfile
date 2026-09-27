FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Librerias base que necesita el runtime .NET del servidor de Godot.
# Si al arrancar Render muestra un error de .NET, agrega  dotnet-runtime-8.0  a esta lista.
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    libicu74 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Servidor exportado (binario + .pck + carpeta data_Vortice_linuxbsd_x86_64).
COPY servidor/ /app/

# El ejecutable se llama Carro.x86_64 (el glob evita romperse si cambia el nombre).
RUN chmod +x /app/*.x86_64

# Arranca como servidor dedicado headless; lee el PORT que da Render.
CMD ["/app/Carro.x86_64", "--headless", "--server"]
