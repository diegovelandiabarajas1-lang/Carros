@echo off
echo ==========================================
echo   Subir proyecto a GitHub (intento 2)
echo ==========================================
echo.
 
cd /d "D:\Carros\nuevo-proyecto-de-juego"
 
echo [1] Activando la sesion guardada de GitHub...
git config --global credential.helper manager
 
echo [2] Conectando con tu repositorio...
git remote remove origin 2>nul
git remote add origin https://github.com/diegovelandiabarajas1-lang/Carros.git
 
echo [3] Preparando los archivos...
git add -A
git commit -m "Subir proyecto del juego con servidor" 2>nul
 
echo.
echo [4] Subiendo a GitHub...
echo     * Si se abre el NAVEGADOR pidiendo autorizar, dale AUTORIZAR / CONTINUE.
echo     * El servidor pesa 73 MB, puede tardar varios minutos. Ten paciencia.
echo.
git push -u origin main --force
 
echo.
echo ==========================================
echo   Termino. Mira arriba si dice "main -^> main" (=quedo bien)
echo   o si volvio a pedir usuario/contrasena (avisame).
echo ==========================================
pause