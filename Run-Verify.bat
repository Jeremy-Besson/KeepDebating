@echo off
setlocal

cd /d "%~dp0" || exit /b 1

echo ==========================================
echo Running solution verification checks...
echo ==========================================
echo.

echo [1/2] BackEnd verification
pushd backend || goto :fail

dotnet restore .\BackEnd.csproj
if errorlevel 1 goto :fail_pop

dotnet build .\BackEnd.csproj -c Release --no-incremental -warnaserror -v minimal
if errorlevel 1 goto :fail_pop

popd

echo.
echo [2/2] FrontEnd verification
pushd frontend || goto :fail

npm ci
if errorlevel 1 goto :fail_pop

npm run verify
if errorlevel 1 goto :fail_pop

popd

echo.
echo ==========================================
echo Verification succeeded.
echo ==========================================
exit /b 0

:fail_pop
popd

:fail
echo.
echo ==========================================
echo Verification failed.
echo ==========================================
exit /b 1
