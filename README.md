# LPR381 Solver

Menu-driven console app that solves Linear/Integer Programming models and runs
sensitivity analysis on them. Builds to `solve.exe`.

## Build & run

Requires the .NET SDK (9.0+). Works identically in Visual Studio or VS Code
(install the **C# Dev Kit** extension) — same `.sln`/`.csproj` either way.

```
dotnet build
dotnet run --project src/Solve
```

## What's implemented so far

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

Verified against the group's own reference case, `samples/santas_workshop.txt`
(full derivation in `docs/Santas_Workshop_Reference.md`): our Primal Simplex reaches
`x1=20, x2=60, z=180` in the same 3 pivots and the same entering/leaving sequence
(x1↔s3, x2↔s1, s3↔s2) as the reference tableaus. `samples/wyndor.txt` (Hillier &
Lieberman's Wyndor Glass Co., `x1=2, x2=6, z=36`) is kept as a second known-answer
check, plus `samples/unbounded.txt` and `samples/infeasible.txt` for the error paths.

### Known gap for whoever builds Branch & Bound

`PrimalSimplex` is a plain LP solver — it does **not** turn a `bin`/`int` sign
restriction into an upper-bound constraint. Solving `samples/knapsack.txt` through
it directly gives the *unbounded-above* relaxation (variables can exceed 1), not the
`0 <= xi <= 1` fractional-knapsack relaxation a correct Branch & Bound needs. When
building the B&B Simplex / B&B Knapsack root relaxation, add `xi <= 1` bounds for
every `bin` variable (and use `LPModel.WithExtraConstraint(...)` for branching bounds)
before calling `PrimalSimplex.Solve`.

## Reference material

`docs/Santas_Workshop_Reference.md` has the full hand-worked derivation for the LP
above: every Primal Simplex and Revised Primal Simplex tableau, B⁻¹ at each step,
shadow prices, duality (primal/dual, strong duality check), and every sensitivity
range (objective coefficients, RHS values). Useful for checking Revised Simplex
pivot-by-pivot, and for checking every SA operation's expected output before
building the UI around it.

## Still to build (see project plan)

- Revised Primal Simplex — canonical form + Product Form / Price Out iterations.
- Branch & Bound Simplex — LP relaxation, branching, fathoming, backtracking, best
  candidate.
- Branch & Bound Knapsack — profit/weight ordering, fractional-knapsack bound,
  greedy-first traversal, backtracking.
- Cutting Plane (Gomory) — Revised Simplex relaxation, cut generation, re-solve loop,
  iteration cap.
- Sensitivity Analysis — all 12 operations from the brief (NBV/BV ranges and changes,
  RHS ranges and changes, NBV column ranges and changes, add activity, add constraint,
  shadow prices) plus duality (apply, solve dual, verify strong/weak).

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
docs/Santas_Workshop_Reference.md   full worked LP + SA + duality reference
samples/                            sample input files
src/Solve/
├── Program.cs
├── Models/                 LPModel, Tableau, IterationRecord, SimplexResult, BranchNode
├── Parsing/                InputFileParser, InputFormatException
├── Algorithms/             CanonicalFormBuilder, PrimalSimplex  (+ your algorithm files)
├── Output/                 ResultReporter
├── SensitivityAnalysis/    (empty — not yet built)
└── Utils/                  Rounding
```
