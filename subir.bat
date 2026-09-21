@echo off
echo ========================================
echo   Subiendo el proyecto del juego a GitHub
echo ========================================
echo.

cd /d "D:\Carros\nuevo-proyecto-de-juego"

echo [1/6] Inicializando git...
git init
git branch -M main

echo [2/6] Configurando identidad de git...
git config user.email "diego.velandia.barajas1@gmail.com"
git config user.name "diegovelandiabarajas1-lang"

echo [3/6] Conectando con tu repositorio de GitHub...
git remote remove origin 2>nul
git remote add origin https://github.com/diegovelandiabarajas1-lang/Carros.git

echo [4/6] Agregando todos los archivos (puede tardar un poco)...
git add -A

echo [5/6] Creando el commit...
git commit -m "Subir proyecto del juego con servidor"

echo [6/6] Subiendo a GitHub (el servidor pesa 73 MB, ten paciencia)...
git push -u origin main --force

echo.
echo ========================================
echo   Termino. Mira arriba si aparece algun error.
echo   Si dice "Writing objects: 100%%" y luego "main -^> main", quedo bien.
echo ========================================
pause
