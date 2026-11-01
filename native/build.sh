#!/bin/sh
cd "$(dirname "$0")"
mkdir -p ../FinanceReportAnalyser/runtime
clang -dynamiclib -arch arm64 -o ../FinanceReportAnalyser/runtime/libfinance.dylib finance.c