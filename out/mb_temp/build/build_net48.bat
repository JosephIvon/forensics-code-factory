@echo off
rem ===== Windows 自带 .NET Framework 编译（零安装验证） =====
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
cd /d %~dp0
if not exist ..\bin mkdir ..\bin
"%CSC%" /nologo /target:exe /out:..\bin\ModbusSelfTest.exe ^
  ..\src\*.cs ..\samples\*.cs
if %errorlevel%==0 (
  echo BUILD OK
  ..\bin\ModbusSelfTest.exe %*
) else (echo BUILD FAILED)
pause
