@echo off
rem Baut Mausi.exe mit dem C#-Compiler aus .NET Framework 4.x (steckt in Windows, keine IDE noetig)
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo csc.exe nicht gefunden - .NET Framework 4.8 fehlt?
  exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:src\mausi.ico /out:Mausi.exe src\mausi.cs
if errorlevel 1 exit /b 1
echo Fertig: Mausi.exe
