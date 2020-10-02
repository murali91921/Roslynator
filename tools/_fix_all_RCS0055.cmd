@echo off

set _visualStudioPath=C:\Program Files\Microsoft Visual Studio\2019\Community
set _msbuildPath="%_visualStudioPath%\MSBuild\Current\Bin"

%_msbuildPath%\msbuild "..\src\CommandLine.sln" /t:Build /p:Configuration=Debug /v:m /m

"..\src\CommandLine\bin\Debug\net48\roslynator" fix "E:\Projects\DotMarkdown\DotMarkdown.sln" ^
 --msbuild-path %_msbuildPath% ^
 --properties "CodeAnalysisRuleSet=E:\Projects\Roslynator\src\global.ruleset" ^
 --supported-diagnostics RCS0055 ^
 --analyzer-assemblies ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.dll" ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.CodeFixes.dll" ^
 --verbosity d ^
 --file-log "roslynator.log" ^
 --file-log-verbosity diag

"..\src\CommandLine\bin\Debug\net48\roslynator" fix "E:\Projects\LinqToRegex\src\LinqToRegex.sln" ^
 --msbuild-path %_msbuildPath% ^
 --properties "CodeAnalysisRuleSet=E:\Projects\Roslynator\src\global.ruleset" ^
 --supported-diagnostics RCS0055 ^
 --analyzer-assemblies ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.dll" ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.CodeFixes.dll" ^
 --verbosity d ^
 --file-log "roslynator.log" ^
 --file-log-verbosity diag

"..\src\CommandLine\bin\Debug\net48\roslynator" fix "E:\Projects\Orang\src\Orang.sln" ^
 --msbuild-path %_msbuildPath% ^
 --properties "CodeAnalysisRuleSet=E:\Projects\Roslynator\src\global.ruleset" ^
 --supported-diagnostics RCS0055 ^
 --analyzer-assemblies ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.dll" ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.CodeFixes.dll" ^
 --verbosity d ^
 --file-log "roslynator.log" ^
 --file-log-verbosity diag

"..\src\CommandLine\bin\Debug\net48\roslynator" fix "E:\Projects\Snippetica\Snippetica.sln" ^
 --msbuild-path %_msbuildPath% ^
 --properties "CodeAnalysisRuleSet=E:\Projects\Roslynator\src\global.ruleset" ^
 --supported-diagnostics RCS0055 ^
 --analyzer-assemblies ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.dll" ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.CodeFixes.dll" ^
 --verbosity d ^
 --file-log "roslynator.log" ^
 --file-log-verbosity diag

"..\src\CommandLine\bin\Debug\net48\roslynator" fix "E:\Projects\SnippetManager\src\SnippetManager.sln" ^
 --msbuild-path %_msbuildPath% ^
 --properties "CodeAnalysisRuleSet=E:\Projects\Roslynator\src\global.ruleset" ^
 --supported-diagnostics RCS0055 ^
 --analyzer-assemblies ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.dll" ^
  "..\src\Formatting.Analyzers.CodeFixes\bin\Debug\netstandard2.0\Roslynator.Formatting.Analyzers.CodeFixes.dll" ^
 --verbosity d ^
 --file-log "roslynator.log" ^
 --file-log-verbosity diag

pause
