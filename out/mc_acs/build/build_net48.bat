@echo off
rem ===== Windows 自带 .NET Framework 编译（零安装，验证代码可编译） =====
rem [ACS] 本套件含 ACS 适配器，编译需官方 ACS.SPiiPlusNET.dll：
rem       安装 ACS 软件后，在下行追加   /r:ACS.SPiiPlusNET.dll   （并确保 DLL 在同目录或 GAC）
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
cd /d %~dp0
if not exist ..\bin mkdir ..\bin
"%CSC%" /nologo /target:exe /out:..\bin\GlueDemo.exe ^
  ..\src\Core\*.cs ..\src\Virtual\*.cs ..\src\Adapters\*.cs ..\samples\*.cs
if %errorlevel%==0 (
  echo BUILD OK
  ..\bin\GlueDemo.exe
) else (
  echo BUILD FAILED
)
pause
