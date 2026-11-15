@echo off
rem Windows (MinGW gcc or clang): builds finance.dll into FinanceReportAnalyser\runtime
cd /d "%~dp0"
if not exist ..\FinanceReportAnalyser\runtime mkdir ..\FinanceReportAnalyser\runtime
gcc -shared -o ..\FinanceReportAnalyser\runtime\finance.dll finance.c
