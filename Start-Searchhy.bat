@echo off
title Starting Searchhy...
cd /d "%~dp0"
powershell -NoProfile -Command "Get-ChildItem -Path . | Unblock-File" >nul 2>&1
start "" "%~dp0Searchhy.exe"
exit
