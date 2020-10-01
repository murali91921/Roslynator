@echo off

set _visualStudioPath=C:\Program Files\Microsoft Visual Studio\2019\Community
set _msbuildPath="%_visualStudioPath%\MSBuild\Current\Bin"

rem %_msbuildPath%\msbuild "..\src\CommandLine.sln" /t:Build /p:Configuration=Debug /v:m /m

"..\src\CommandLine\bin\Debug\net48\roslynator" fix "..\src\Roslynator.sln" ^
 --msbuild-path %_msbuildPath% ^
 --supported-diagnostics RCS0054 ^
 --analyzer-assemblies ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.dll" ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.CodeFixes.dll" ^
 --verbosity d ^
 --file-log "roslynator.log" ^
 --file-log-verbosity diag

pause
