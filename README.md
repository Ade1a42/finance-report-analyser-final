---

# Assignment 8: Extension Methods, LINQ, Currying, Lazy Processing, Native C

**Author:** Asel Nurzhankyzy, group IT-2514 (individual project)
**Project:** Finance Report Analyser (the same project as Assignments 5, 6, 7)
**Repository:** https://github.com/Ade1a42/finance-report-analyser-final (tag `assignment-8`)

## 1. What is new in this assignment
I added six things on top of my A7 code (pure pipeline, `Result`, `Map`/`Bind`):
1. Two custom extension methods.
2. Two LINQ reports.
3. A curried scoring function with two partially applied versions.
4. A lazy `yield return` reader with safe resource disposal.
5. A small C library called from C# (P/Invoke), used in the savings rate.
6. Report export to a file.

## 2. How to build, run and test
- .NET SDK: 10.0.401
- OS: ProductName: macOS, ProductVersion: 26.6, BuildVersion: 25G72, Apple Silicon (arm64)
- C compiler: Apple clang version 21.0.0 (clang-2100.3.34.2)
- Working directory: the `assignment8` folder (it contains `FinanceReportAnalyser`, `FinanceReportAnalyser.Tests` and `native`).

```
sh native/build.sh
dotnet run --project FinanceReportAnalyser
dotnet test FinanceReportAnalyser.Tests
```

**Normal demo:** run the app. Section 6 at the bottom prints the lazy preview, both reports for the strict and relaxed scorer, and `Savings rate: 16%`. It also writes `report_strict.txt` and `report_relaxed.txt` next to the built app.
**Failure demos:**
- Missing file and malformed file: section 5 of the app output (`file.not_found`, `file.malformed`).
- Native library missing: run `dotnet build FinanceReportAnalyser`, then temporarily rename `FinanceReportAnalyser/bin/Debug/net10.0/runtime`, then run `dotnet run --project FinanceReportAnalyser --no-build`. Section 6 prints `Failed: [native.load_failed]` and the program does not crash. Rename the folder back afterwards.

**Latest test result (1 Nov 2026):** 43 tests, 43 passed, 0 failed.

## 3. Input and domain rules
- Input file: `sampleData/transactions.csv`, header `type,category,amount`.
- An amount must be greater than 0 and have at most 2 decimals. Categories must exist.
- Bad lines are not thrown as exceptions, they become explicit `Result` failures (from A7).
- File problems: missing file gives `file.not_found`; wrong header or wrong number of fields gives `file.malformed`.

## 4. Requirement map
| Requirement | File and method |
|---|---|
| Extension 1: `decimal.ToMinorUnits()` | `Extensions.cs`, used in `NativeFinance.SavingsRateWith` |
| Extension 2: `IEnumerable<Transaction>.ExpensesAtLeast(limit)` | `Extensions.cs`, used in `Reports.LargeExpenses` |
| Report 1: large expenses | `Reports.cs`, `LargeExpenses` |
| Report 2: spending by category | `Reports.cs`, `SpendingByCategory` |
| Currying and partial application | `Scoring.cs`, `CreateScore` |
| Lazy `yield return` | `LazyImporter.cs`, `StreamTransactions` |
| Resource disposal | `LazyImporter.cs` and `Analysis.cs` (`ReportExporter.Export`) |
| Native C function | `native/finance.c` |
| Binding and adapter | `NativeFinance.cs` |
| Integration with `Map` and `Bind` | `Analysis.cs`, `Build` and `RunFromFile` |
| Export | `Analysis.cs`, `ReportExporter.Export` |
| Tests | `FinanceReportAnalyser.Tests/AssignmentTests.cs` |

## 5. Extension methods (and why they are on these types)
- `ToMinorUnits(this decimal amount)` returns `long`. It converts tenge to tiyn (x100). It is on `decimal` because any money amount needs this conversion before it goes to the native library.
- `ExpensesAtLeast(this IEnumerable<Transaction> transactions, decimal limit)` returns `IEnumerable<ExpenseTransaction>`. It is a reusable query step for any sequence of transactions. It builds a new query and does not change the original list.

## 6. LINQ reports
**Report 1: Large expenses.** For a user who wants to see their biggest spending.
- Input: transactions, a limit, and a scorer function.
- Stages: `ExpensesAtLeast(limit)` (filter) -> `Select` (line with score) -> `OrderByDescending` score -> `ThenBy` category name (tiebreaker) -> `ToList`.
- Output fields: category, amount, score.

**Report 2: Spending by category.** For a user who wants totals per category.
- Input: transactions.
- Stages: `OfType<ExpenseTransaction>` -> `GroupBy` category -> `Select` with `Sum` and `Count` -> `OrderByDescending` total -> `ThenBy` category name.
- Output fields: category, total, count.

Both reports are pure: no Console and no file access inside `Reports.cs`. Order is deterministic because ties are broken by category name.

## 7. Configured functions (currying)
`CreateScore : decimal weight -> (ExpenseTransaction -> decimal)`.
Urgent categories (Medicine, UnexpectedExpenses) count `weight` times, other categories count once.
- `Scoring.CreateScore(2m)` is the strict scorer.
- `Scoring.CreateScore(1m)` is the relaxed scorer.

Example on a Medicine expense of 100: strict gives 200, relaxed gives 100. A Groceries expense of 100 gives 100 in both. The scorer is passed into the report as a parameter, so I do not change any global setting.

## 8. Deferred execution and materialisation
`StreamTransactions` is a `yield return` method. Calling it runs nothing. The file is opened and read only while someone loops over the result.
- If I enumerate the lazy sequence twice, the file is opened and read twice.
- `ToList()` is my deliberate materialisation point: the snapshot can be reused as many times as I want without reading the file again.
- Tests prove this with an "opened" counter: 0 before enumeration, 1 after `ToList()`, still 1 when reusing the list, and 2 when enumerating the lazy sequence again.

Note: `StreamTransactions` skips invalid lines. The strict validation with a full error list is the A7 `FileImporter`, which the report workflow uses.

## 9. Resource ownership
- `LazyImporter.StreamTransactions` owns its `TextReader`. It is released by `using var` when iteration finishes, fails, or stops early (for example with `Take`).
- `ReportExporter.Export` owns its `StreamWriter`. It is released at the end of its `using`, also when an exception happens. The exception is caught at this output boundary and returned as `export.io_error` or `export.access_denied`.
- `FileImporter.ReadFile` uses `File.ReadAllLines`. This is a convenience API that opens and closes the file itself before it returns.
- The native call only passes simple integers, so no native memory is allocated and nothing needs to be released.

## 10. Resource note (retention and allocation)
- **Where datasets are materialised:** `File.ReadAllLines` (a `string[]`), `ImportResult.ValidTransactions`, and the report lists (`ToList()` snapshots).
- **Deferred queries and closures:** the report methods return materialised lists, so no lazy query stays alive holding the source list. A scorer made by `CreateScore` captures only one `decimal` weight, not any data.
- **Caches, long-lived collections, event subscriptions:** not used.
- **Design decision:** reports return snapshots instead of lazy queries, so nothing keeps the source collection alive after the report is built.
- **Trade-off:** streaming uses little memory and can start early, but it can be read only once and keeps the file open. A snapshot uses more memory but is safe to reuse many times.

## 11. Native function contract
- File: `native/finance.c`
- Function: `int32_t savings_rate(int64_t income_minor, int64_t savings_minor)`
- Units: tiyn (tenge x 100) as whole numbers.
- Output: savings as a whole percent, rounded down (12.5% becomes 12).
- Valid range: income from 1 to 9,000,000,000,000,000; savings from 0 up to income. The upper limit stops `savings * 100` from overflowing.
- Invalid input: returns `-1`.
- Manual example: income 50000 tg and savings 10000 tg are 5000000 and 1000000 tiyn, so the result is 20.

**C# side:**
- Binding: `Native.savings_rate`, declared with `DllImport("finance", CallingConvention = CallingConvention.Cdecl)`.
- Adapter: `NativeFinance.SavingsRate`. It validates the domain input first, checks that the library file exists, calls the native function, and uses a `switch` expression to map `-1` to `Result.Failure` and any other number to `Result.Success`.
- Failure cases are plain `Result` failures: `native.invalid_input`, `native.load_failed`, `native.incorrect_input`.
- The adapter takes the native function as a parameter (`SavingsRateWith`), so tests can pass a fake one for the failure paths.

**Used in the workflow:** `Analysis.Build` calls the adapter and uses `Map` to build the reports only if the native call succeeded. If it fails, the report stages are skipped.

## 12. Native build and loading
- Platform: macOS, Apple Silicon (arm64).
- Compiler and version: see section 2.
- Build command (in `native/build.sh`): `clang -dynamiclib -arch arm64 -o ../FinanceReportAnalyser/runtime/libfinance.dylib finance.c`
- Binary location: `FinanceReportAnalyser/runtime/libfinance.dylib`. The `.csproj` copies it to `bin/.../runtime/` on every build.
- Loading: `NativeLibrary.SetDllImportResolver` loads the library from `AppContext.BaseDirectory/runtime`. If the file is missing, the adapter returns `native.load_failed` and the program keeps running.
- **Difficulty and how I solved it:** a plain `DllImport("finance")` only searches next to the executable, not in the `runtime` subfolder, so the library would not be found. I solved it with a custom resolver that loads from the explicit `runtime` path, plus a `File.Exists` check before calling.
- Limitation: an x64 .NET runtime cannot load an arm64 library. For another platform the library must be rebuilt.

## 13. Tests (43 total, all in `AssignmentTests.cs`)
- **Assignment 7 tests (19):** `Result`, `Map` and `Bind`, parsing, budget period, file failures. They still pass.
- **Assignment 8 tests (24):**
  - Reports: normal result, empty input, no matches, tied scores, grouping and totals.
  - Extensions: `ToMinorUnits` values, `ExpensesAtLeast` filter and source list unchanged.
  - Configured functions: strict and relaxed scorer on the same input give different results for Medicine.
  - Deferred execution: nothing runs before enumeration, `ToList()` reuse does not reopen the file.
  - Resource lifetime: reader disposed after full enumeration and after early stop (`Take(1)`).
  - Native: real call returns 20 (manual value), library missing becomes `native.load_failed`, `-1` becomes `native.incorrect_input`, invalid domain input never calls native.
  - Workflows: normal path with real native call, native failure skips the report stages, full file to export path.
  - Files: missing file, malformed file (nothing is exported), unwritable export path. Temporary folders are created per test and deleted afterwards.

## 14. Member contribution
This is an individual project. I wrote all the code, tests and documentation above.