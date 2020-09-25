@echo off

set _visualStudioPath=C:\Program Files\Microsoft Visual Studio\2019\Community
set _msbuildPath="%_visualStudioPath%\MSBuild\Current\Bin"

%_msbuildPath%\msbuild "..\src\CommandLine.sln" /t:Build /p:Configuration=Debug /v:m /m

"..\src\CommandLine\bin\Debug\net48\roslynator" fix "..\src\Core\Core.csproj" ^
 --msbuild-path %_msbuildPath% ^
 --supported-diagnostics RCS1016 RCS0052 ^
 --projects Core ^
 --analyzer-assemblies ^
  "..\src\Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.CSharp.Analyzers.dll" ^
  "..\src\Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.CSharp.Analyzers.CodeFixes.dll" ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.dll" ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.CodeFixes.dll" ^
 --verbosity d ^
 --file-log "roslynator.log" ^
 --file-log-verbosity diag

pause
