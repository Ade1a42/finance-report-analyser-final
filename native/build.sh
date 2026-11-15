#!/bin/sh
# Builds the native library for the current OS into FinanceReportAnalyser/runtime
cd "$(dirname "$0")"
mkdir -p ../FinanceReportAnalyser/runtime
case "$(uname -s)" in
  Darwin) clang -dynamiclib -o ../FinanceReportAnalyser/runtime/libfinance.dylib finance.c ;;
  Linux)  gcc -shared -fPIC -o ../FinanceReportAnalyser/runtime/libfinance.so finance.c ;;
  *)      echo "On Windows run native/build.cmd" ; exit 1 ;;
esac
