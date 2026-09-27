@echo off
chcp 65001 >nul
echo ==========================================
echo   Limpiar carpeta del proyecto (seguro)
echo ==========================================
echo.
cd /d "D:\Carros\nuevo-proyecto-de-juego"

set "REV=D:\Carros\_revisar_borrar"
if not exist "%REV%" mkdir "%REV%"

echo [1] Borrando caches regenerables (Godot y Visual Studio los reconstruyen)...
if exist ".godot" ( rmdir /s /q ".godot" && echo     - .godot borrado )
if exist ".vs"    ( rmdir /s /q ".vs"    && echo     - .vs borrado )

echo.
echo [2] Moviendo compilados y la carpeta anidada a "%REV%"
echo     (REVERSIBLE: revisa esa carpeta y borrala tu cuando estes seguro)...
if exist "Exportable"     ( move /y "Exportable" "%REV%\" >nul     && echo     - Exportable\ movido )
if exist "Exportable.zip" ( move /y "Exportable.zip" "%REV%\" >nul && echo     - Exportable.zip movido )
if exist "Carros"         ( move /y "Carros" "%REV%\" >nul         && echo     - Carros\ ^(repo anidado^) movido )

echo.
echo ==========================================
echo   Listo. Caches borrados; compilados y 'Carros' movidos a:
echo     %REV%
echo   Revisa esa carpeta y borrala cuando estes seguro.
echo   (Godot reconstruye .godot solo al abrir el proyecto.)
echo   Si .vs no se borro, cierra Visual Studio y corre esto otra vez.
echo ==========================================
pause
