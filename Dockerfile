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

# Copiamos el servidor exportado (binario + carpeta data_Vortice_linux_x86_64 + .pck).
COPY servidor/ /app/

RUN chmod +x /app/juego.x86_64

# El modo "servidor dedicado" arranca solo como servidor y lee el puerto (PORT) que da Render.
CMD ["/app/juego.x86_64", "--headless"]
