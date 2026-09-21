# Paso 3 — Subir el servidor a internet (Render)

Hola Diego. El código ya tiene todo lo necesario (modo servidor dedicado, latido, volver-al-lobby).
Aquí está el camino para que el juego funcione **por internet de verdad**. Lo dejé lo más simple posible.

Ya te dejé listos en el proyecto estos archivos (no los tienes que tocar):
- `Dockerfile` — le dice a Render cómo correr tu servidor.
- `.dockerignore` — para que la subida sea liviana.
- `render.yaml` — configuración opcional del servicio.

Solo faltan **4 pasos** (los hacemos juntos cuando vuelvas y prendas la PC):

---

## Paso A — Exportar el servidor para Linux (en Godot, tu PC)

1. En Godot: **Proyecto → Exportar…**
2. **Añadir…** → **Linux**.
3. Con ese preset seleccionado, ve a la pestaña **"Recursos"** (Resources).
4. En **"Modo de exportación"** elige **"Exportar como servidor dedicado"**
   (Export as dedicated server).
   - Esto activa la característica `dedicated_server`, que es justo lo que mi código detecta
     para arrancar como servidor. ✅
5. Abajo, en **"Exportar proyecto…"**:
   - Crea una carpeta llamada **`servidor`** DENTRO del proyecto.
   - Guarda el archivo como **`servidor/juego.x86_64`** (ese nombre exacto, el Dockerfile lo busca así).
   - **Desmarca** "Exportar con depuración" (queremos versión release).
6. Exporta. En la carpeta `servidor/` deberían quedar:
   - `juego.x86_64` (el servidor)
   - `data_Vortice_linux_x86_64/` (carpeta con el runtime .NET)
   - (quizás un `juego.pck`)

> ⚠️ Importante: los 3 (o 2) archivos/carpeta de `servidor/` deben quedar juntos y **subirse a GitHub**.

---

## Paso B — Subir el proyecto a GitHub

1. Crea un repositorio nuevo en tu GitHub (privado está bien).
2. Sube **todo el proyecto**, incluyendo la carpeta **`servidor/`** y el **`Dockerfile`**.
3. Revisa que `servidor/` **NO** esté ignorada en `.gitignore` (tiene que subirse sí o sí).

> Yo te puedo ayudar con esto: cuando vuelvas, dime "sube el proyecto a GitHub" y uso la
> herramienta de GitHub para hacerlo (revisa que no haya secretos, hace el commit y el push).

---

## Paso C — Crear el servicio en Render

1. Entra a **render.com** e inicia sesión (o crea cuenta — **no pide tarjeta**).
2. **New +** → **Web Service**.
3. Conecta tu repositorio de GitHub.
4. Render detecta el `Dockerfile` solo. Configura:
   - **Name:** `derby-servidor` (o el que quieras).
   - **Instance Type / Plan:** **Free**.
   - Lo demás, por defecto.
5. **Create Web Service.** Render construye y despliega (tarda unos minutos la primera vez).
6. Cuando termine, en la pestaña **Logs** deberías ver:
   ```
   [SERVIDOR] Escuchando WebSocket en el puerto 10000. Esperando jugadores...
   ```
   Si ves eso, ¡el servidor está vivo en internet! 🎉

---

## Paso D — Conectarse a jugar

1. Render te da una dirección, algo como: `https://derby-servidor.onrender.com`
2. En el juego: **Multijugador → Unirse**, y en la casilla escribe (¡con `wss://`!):
   ```
   wss://derby-servidor.onrender.com
   ```
3. Dale **Unirse**. Deberías entrar solo a la Arena y aparecer tu carro.
4. Pásale esa misma dirección a un amigo (en otra red/ciudad) y que se una igual.
   ¡Ya están jugando por internet! 🌍🎮

---

## Notas del plan gratis de Render (para que no te asustes)

- El servidor **se duerme** tras ~15 min sin nadie conectado. La primera conexión después
  tarda **~50 segundos** en despertarlo — es normal, ten paciencia en el primer intento.
- El **latido** que programamos hace que, si el servidor se cae o se duerme, los clientes
  **vuelvan solos al lobby** en vez de trabarse.
- WebSocket funciona en el plan gratis. ✅
- Solo se puede un servicio web gratis corriendo a la vez (más que suficiente por ahora).

---

## Si algo falla (mándame esto y lo arreglo)

- **El deploy falla diciendo que no detecta el puerto** → revisa en los Logs si aparece
  "Escuchando WebSocket en el puerto...". Mándame los logs completos.
- **Error de .NET al arrancar** (algo de "dotnet" o "libicu") → en el `Dockerfile`, en la línea
  de instalación, agrega `dotnet-runtime-8.0` a la lista. Si no sabes, mándame el log y lo hago yo.
- **Los clientes no se conectan** → confirma que usas `wss://` (no `http://` ni `ws://`) y la
  dirección exacta que te dio Render.

---

## Cuando vuelvas

Dime por dónde vas (¿ya exportaste? ¿ya está en GitHub?) y seguimos desde ahí.
Yo ya dejé el código y los archivos de despliegue listos. Solo falta exportar, subir y desplegar. 💪
