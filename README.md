# LPR381 Solver

Menu-driven console app that solves Linear/Integer Programming models and (once
Member 4's part lands) runs sensitivity analysis on them. Builds to `solve.exe`.

## Build & run

Requires the .NET SDK (9.0+). Works identically in Visual Studio or VS Code
(install the **C# Dev Kit** extension) — same `.sln`/`.csproj` either way.

```
dotnet build
dotnet run --project src/Solve
```

## Status: Member 1's scope (done)

- `Models/` — `LPModel`, `Tableau`, `IterationRecord`, `SimplexResult`, `BranchNode`
  (shared contract — code against these, don't change shapes without telling the group).
- `Parsing/InputFileParser.cs` — reads the input file format below. Any number of
  variables/constraints.
- `Algorithms/CanonicalFormBuilder.cs` — parsed model -> initial simplex tableau
  (slack/surplus/artificial columns, Big-M setup, `urs` variable splitting).
- `Algorithms/PrimalSimplex.cs` — Big-M Primal Simplex. Handles `<=`, `>=`, `=`.
  Detects infeasible (artificial stuck in basis) and unbounded (no positive pivot
  entry) automatically.
- `Output/ResultReporter.cs` — one formatter, called for both console and file output,
  so the two always match. All values rounded to 3 decimals.
- `Program.cs` — main menu, algorithm submenu, sensitivity submenu (stubbed — see below).

Verified against two hand-checkable LPs (not in the brief, just sanity checks):
`samples/wyndor.txt` -> `x1=2, x2=6, z=36` (Hillier & Lieberman's Wyndor Glass Co.),
plus `samples/unbounded.txt` and `samples/infeasible.txt` for the error-handling paths.

### Known gap Member 2/3 need to handle

`PrimalSimplex` is a plain LP solver — it does **not** turn a `bin`/`int` sign
restriction into an upper-bound constraint. Solving `samples/knapsack.txt` through
it directly gives the *unbounded-above* relaxation (variables can exceed 1), not the
`0 <= xi <= 1` fractional-knapsack relaxation a correct Branch & Bound needs. When
building the B&B Simplex / B&B Knapsack root relaxation, add `xi <= 1` bounds for
every `bin` variable (and use `LPModel.WithExtraConstraint(...)` for branching bounds)
before calling `PrimalSimplex.Solve`.

## Still to build (see project plan)

| Owner | Piece |
|---|---|
| Member 2 | Revised Primal Simplex, Branch & Bound Simplex |
| Member 3 | Branch & Bound Knapsack, Cutting Plane |
| Member 4 | All Sensitivity Analysis operations, Duality |

The algorithm submenu and SA submenu in `Program.cs` already have numbered stubs
for every one of these — wire your implementation in where it says "not yet
implemented".

## Input file format

```
max +2 +3 +3 +5 +2 +4
+11 +8 +6 +14 +10 +10 <=40
bin bin bin bin bin bin
```

- Line 1: `max`/`min`, then one signed coefficient per decision variable.
- One line per constraint: one signed coefficient per variable, then a relation
  (`<=`, `>=`, `=`) and RHS — fused (`<=40`) or spaced (`<= 40`), both parse.
- Last line: one sign restriction per variable — `+`, `-`, `urs`, `int`, or `bin`.

Sample files are in `samples/`.

## Folder structure

```
src/Solve/
├── Program.cs
├── Models/                 LPModel, Tableau, IterationRecord, SimplexResult, BranchNode
├── Parsing/                InputFileParser, InputFormatException
├── Algorithms/             CanonicalFormBuilder, PrimalSimplex  (+ your algorithm files)
├── Output/                 ResultReporter
├── SensitivityAnalysis/    (empty — Member 4)
└── Utils/                  Rounding
```
