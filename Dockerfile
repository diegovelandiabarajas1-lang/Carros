FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libicu74 && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY servidor/ /app/

RUN chmod +x /app/*.x86_64

CMD ["/bin/sh", "-c", "exec /app/*.x86_64 --headless"]
