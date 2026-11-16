# Finance Report Analyser (Final Exam)

**Author:** Asel Nurzhankyzy, group IT-2514 (individual project)

**Course:** Introduction to Functional Programming

**Repository:** https://github.com/Ade1a42/finance-report-analyser-final

**Branch:** main

**Final commit:** 62a3262 


## What the application does
A console personal-finance analyser. You type or import transactions (`type,category,amount`), the program validates each one, keeps them in a budget period (Open or Closed), and produces a summary, two reports, a savings rate (calculated by a C library) and an exported report file. The program runs in a menu loop until you choose 0.

## Setup, build, run, test
- .NET SDK 10.0 (tested with 10.0.115 on Linux and 10.0.401 on macOS)
- C compiler: clang (macOS) or gcc (Linux/MinGW)

```
sh native/build.sh                           # builds the native library into FinanceReportAnalyser/runtime
dotnet build FinanceReportAnalyser
dotnet run --project FinanceReportAnalyser   # interactive menu
dotnet test FinanceReportAnalyser.Tests
```
On Windows run `native\build.cmd` instead of `build.sh`.
Native binaries included in `FinanceReportAnalyser/runtime/`: `libfinance.dylib` (macOS arm64) and `libfinance.so` (Linux x64). The loader picks the file by operating system.

## Menu
| Option | Workflow |
|---|---|
| 1 | Add a transaction: `ParseLine(line).Bind(AddTransaction)` |
| 2 | Summary with a chosen policy (AllMoneyIn or EarnedOnly) |
| 3 | Close the period (a closed period rejects new transactions) |
| 4 | Import a CSV file (bad lines are reported, good lines accepted) |
| 5 | Lookup an expense category (`Option`) |
| 6 | Reports + native savings rate + export (`Build` then `Bind` export) |
| 7 | New empty period |
| 8 | Built-in demo of all failure cases |

## Error codes
`line.empty`, `line.missing`, `line.field_count`, `type.unknown`, `amount.not_a_number`, `amount.not_positive`, `amount.too_many_decimals`, `category.unknown`, `period.closed`, `period.already_closed`, `file.not_found`, `file.malformed`, `file.access_denied`, `file.io_error`, `export.access_denied`, `export.io_error`, `native.invalid_input`, `native.load_failed`, `native.incorrect_input`, `native.entry_missing`, `native.wrong_architecture`.

## Requirement map
| Requirement | Where |
|---|---|
| Immutable domain models, validated factories | `Program.cs`: `IncomeTransaction.Create`, `ExpenseTransaction.Create` (private constructors) |
| Pattern matching | `BudgetPeriodManager`, `SummaryOf`, `Result` switches |
| Pipeline with 3 named stages | `FinancePipeline.SelectTransactions` -> `TotalTransactions` -> `CreateSummary` (`Run`) |
| Higher-order function and two policies | `Run(transactions, incomePolicy)`; `AllMoneyIn` vs `EarnedOnly` |
| Safe operation sequence | `OpenPeriod` / `ClosedPeriod` types; `BudgetPeriodManager` |
| Result, Map, Bind | `Result.cs`; `TransactionParser.ParseLine`, `FileImporter.Import`, `Analysis.Build` |
| Option (missing lookup) | `SummaryLookup.FindExpenseTotal` |
| Boundary exceptions | `FileImporter.ReadFile`, `ReportExporter.Export`, `NativeFinance.SavingsRateWith` |
| Extension methods | `Extensions.cs`: `ToMinorUnits`, `ExpensesAtLeast` |
| LINQ reports | `Reports.cs`: `LargeExpenses`, `SpendingByCategory` |
| Currying and partial application | `Scoring.CreateScore(weight)`: strict (2) and relaxed (1) |
| Lazy `yield return` | `LazyImporter.StreamTransactions` |
| Persistence | `sampleData/*.csv`, `ReportExporter.Export` |
| Native C library (P/Invoke) | `native/finance.c`, `NativeFinance.cs` |
| Tests | `FinanceReportAnalyser.Tests/AssignmentTests.cs` |

## Native library
`int32_t savings_rate(int64_t income_minor, int64_t savings_minor)` returns savings as a whole percent (rounded down), or -1 for invalid input. Amounts are in tiyn (tenge x 100, via `ToMinorUnits`). The adapter validates input, checks the file exists, calls the function and converts `-1`, `DllNotFoundException`, `EntryPointNotFoundException` and `BadImageFormatException` into `Result` failures, so the program never crashes at this boundary.

## Resource ownership
`LazyImporter.StreamTransactions` owns its `TextReader` (`using var`, released on completion, failure or early stop). `ReportExporter.Export` owns its `StreamWriter` (`using var`). `File.ReadAllLines` opens and closes the file itself. The native call passes plain integers, so no native memory needs releasing. Reports return `ToList()` snapshots, so no lazy query keeps source data alive.

## Tests
45 tests (xunit): validation, parsing, Map/Bind, period transitions, file failures, reports (empty, no match, ties), extension methods, scorers, lazy processing and disposal, native success and failures, three full workflows. Tests that need the real native binary return early when no binary exists for the current OS.

## Limitations
- Native binaries are included for macOS arm64 and Linux x64 only; Windows needs `native\build.cmd`. An x64 .NET runtime cannot load an arm64 library.
- `LazyImporter.StreamTransactions` silently skips invalid lines; strict validation with a full error list is `FileImporter.Import`.
- The menu keeps the period only in memory; it is not saved between runs (reports can be exported).
- `Result<T>.Ok` and `Error` now have internal constructors, so null payloads can only be created through `Success`/`Failure`.
- Not implemented (future ideas): JSON storage, multiple periods, currency conversion.

