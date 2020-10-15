@echo off

"C:\Program Files\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild" "..\src\Tests\Formatting.Analyzers.Tests\Formatting.Analyzers.Tests.csproj" ^
 /t:Build ^
 /p:Configuration=Debug,RunCodeAnalysis=false ^
 /v:minimal ^
 /m

if errorlevel 1 (
 pause
 exit
)

dotnet test -c Debug --no-build "..\src\Tests\Formatting.Analyzers.Tests\Formatting.Analyzers.Tests.csproj"

if errorlevel 1 (
 pause
 exit
)

echo OK
pause
