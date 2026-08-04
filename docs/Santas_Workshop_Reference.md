# Santa's Workshop — LP Reference File
> Converted from: Santas_Workshop_Revised.xlsx
> Reference for implementing and verifying every algorithm in the LPR381 project.

---

## SHEET 1: LP — The Problem & Model

### Problem Statement
Santa's Workshop employs elves to manufacture two types of wooden toys: soldiers and trains.
- A soldier sells for R27 and uses R10 worth of raw materials. Each soldier increases variable labour and overhead costs by R14. → Profit per soldier = R27 - R10 - R14 = **R3**
- A train sells for R21 and uses R9 worth of raw materials. Each train increases variable labour and overhead costs by R10. → Profit per train = R21 - R9 - R10 = **R2**
- A soldier requires 2 hours of finishing labour and 1 hour of carpentry labour.
- A train requires 1 hour of finishing labour and 1 hour of carpentry labour.
- Available per week: 100 finishing hours, 80 carpentry hours.
- Demand constraint: at most 40 soldiers per week. Train demand is unlimited.
- **Goal: maximise weekly profit.**

---

### Primary Linear Programming Model

**Decision Variables:**
- x1 = number of soldiers produced per week
- x2 = number of trains produced per week

**Objective Function:**
```
Max z = 3x1 + 2x2
```

**Subject To:**
```
(1) 2x1 +  x2 <= 100   (Finishing hours)
(2)  x1 +  x2 <=  80   (Carpentry hours)
(3)  x1       <=  40   (Demand for soldiers)

x1, x2 >= 0
```

Input file form used in `samples/santas_workshop.txt`:
```
max +3 +2
+2 +1 <=100
+1 +1 <=80
+1 +0 <=40
+ +
```

---

### Dual Linear Programming Model

**Decision Variables:**
- y1 = dual variable for constraint 1 (Finishing)
- y2 = dual variable for constraint 2 (Carpentry)
- y3 = dual variable for constraint 3 (Demand)

**Objective Function:**
```
Min w = 100y1 + 80y2 + 40y3
```

**Subject To:**
```
(1) 2y1 + y2 + y3 >= 3   (Soldiers)
(2)  y1 + y2      >= 2   (Trains)

y1, y2, y3 >= 0
```

---

### Primary Solver Solution (Optimal)
| Variable | Value |
|----------|-------|
| x1 (Soldiers) | 20 |
| x2 (Trains)   | 60 |
| **Max z**     | **180** |

**Constraints at optimum:**
| # | LHS Value | Sign | RHS | Binding? |
|---|-----------|------|-----|----------|
| 1 (Finishing) | 2(20) + 60 = 100 | <= | 100 | Yes |
| 2 (Carpentry) | 20 + 60 = 80     | <= | 80  | Yes |
| 3 (Demand)    | 20               | <= | 40  | No  |

---

### Dual Solver Solution (Optimal)
| Variable | Value |
|----------|-------|
| y1 | 1 |
| y2 | 1 |
| y3 | 0 |
| **Min w** | **180** |

---

### Duality
- Max z (Primary) = **180**
- Min w (Dual)    = **180**
- Duality Gap = **0**
- → **Strong Duality** confirmed.

**Economic Interpretation:**
- Total Resource Cost of Santa's Workshop = Optimal Solution (Dual) = **180**
- Resource Cost (Soldiers): y1·a11 + y2·a21 + y3·a31 = 2(1) + 1(1) + 1(0) = **3** (equals objective coefficient)
- Resource Cost (Trains):   y1·a12 + y2·a22 + y3·a32 = 1(1) + 1(1) + 0(0) = **2** (equals objective coefficient)

---

### Shadow Prices (Primary)
Computed as CbvB⁻¹ from the optimal basis {x2, s3, x1}:

| Constraint | Shadow Price |
|------------|-------------|
| Finishing (b1) | **1** |
| Carpentry (b2) | **1** |
| Demand (b3)    | **0** |

Interpretation: One additional finishing hour increases profit by R1. One additional carpentry hour increases profit by R1. The demand constraint is not binding so its shadow price is 0.

**Shadow Prices (Dual):**
| Variable | Shadow Price |
|----------|-------------|
| Soldiers (y1*) | **20** |
| Trains (y2*)   | **60** |

**Shadow Prices Relationship:**
- Shadow Prices (Primary) = Optimal Solution (Dual): [1, 1, 0]
- Shadow Prices (Dual) = Optimal Solution (Primary): [20, 60]

---

## SHEET 2: SR — Excel Sensitivity Report

### Variable Cells
| Variable | Final Value | Reduced Cost | Obj. Coefficient | Allowable Increase | Allowable Decrease |
|----------|-------------|--------------|------------------|--------------------|--------------------|
| x1 (Soldiers) | 20 | 0 | 3 | 1 | 1 |
| x2 (Trains)   | 60 | 0 | 2 | 1 | 0.5 |

### Constraints
| Constraint | Final Value | Shadow Price | RHS | Allowable Increase | Allowable Decrease |
|------------|-------------|--------------|-----|--------------------|--------------------|
| Finishing (b1) | 100 | 1 | 100 | 20 | 20 |
| Carpentry (b2) |  80 | 1 |  80 | 20 | 20 |
| Demand (b3)    |  20 | 0 |  40 | ∞  | 20 |

---

## SHEET 3: Math. Prelims. — Mathematical Preliminaries

### Canonical Form (Initial Tableau T-i)
After adding slack variables s1, s2, s3:

```
(z)  3x1 + 2x2               = 0
     2x1 +  x2 + s1          = 100
      x1 +  x2      + s2     = 80
     2x1             + s3    = 40

x1, x2, s1, s2, s3 >= 0
```

**Initial Tableau (T-i):**
| Row | x1 | x2 | s1 | s2 | s3 | rhs |
|-----|----|----|----|----|-----|-----|
| z   |  3 |  2 |  0 |  0 |  0 |   0 |
| 1   |  2 |  1 |  1 |  0 |  0 | 100 |
| 2   |  1 |  1 |  0 |  1 |  0 |  80 |
| 3   |  1 |  0 |  0 |  0 |  1 |  40 |

---

### Basis at Optimality

**Optimal Basis B** = columns for {x2, s3, x1} (in that order, as basic variables for rows 1, 2, 3)

**Non-Basic Variables (Xnb):** s1, s2
**Basic Variables (Xbv):** x2, s3, x1
- Xbv = {x2, s3, x1} with Cbv = [2, 0, 3]
- Cnbv = [0, 0] for {s1, s2}

**B (basis matrix, columns for x2, s3, x1 from constraint matrix):**
```
B = [ 1  0  2 ]    det(B) = 1
    [ 1  0  1 ]
    [ 0  1  1 ]
```

**B⁻¹ (inverse of basis matrix):**
```
B⁻¹ = [ -1   2   0 ]
      [ -1   1   1 ]
      [  1  -1   0 ]
```

**CbvB⁻¹ (shadow price vector):**
```
Cbv = [2, 0, 3]
CbvB⁻¹ = [2,0,3] × B⁻¹ = [1, 1, 0]
```
→ Shadow prices: y1=1, y2=1, y3=0

**rhs* = B⁻¹ × b:**
```
b = [100, 80, 40]ᵀ

B⁻¹ × b = [ -1(100) + 2(80) + 0(40) ]   = [ 60 ]   → x2 = 60
          [ -1(100) + 1(80) + 1(40) ]   = [ 20 ]   → s3 = 20
          [  1(100) - 1(80) + 0(40) ]   = [ 20 ]   → x1 = 20
```

**Updated Column Calculations (A* = B⁻¹ × A):**

| Column | A (original) | A* = B⁻¹A |
|--------|-------------|-----------|
| A1 (x1) | [2,1,1]ᵀ | [0,0,1]ᵀ |
| A2 (x2) | [1,1,0]ᵀ | [1,0,0]ᵀ |
| A3 (s1) | [1,0,0]ᵀ | [-1,-1,1]ᵀ |
| A4 (s2) | [0,1,0]ᵀ | [2,1,-1]ᵀ |
| A5 (s3) | [0,0,1]ᵀ | [0,1,0]ᵀ |

**Price Out values at optimum (C*j = CbvB⁻¹Aj − Cj):** s1* = 1, s2* = 1 (both ≥ 0 → optimal confirmed).

**Optimal Tableau (T-\*):**
| Row | x1 | x2 | s1 | s2 | s3 | rhs |
|-----|----|----|----|----|-----|-----|
| z   |  0 |  0 |  1 |  1 |  0 | 180 |
| 1   |  0 |  1 | -1 |  2 |  0 |  60 |
| 2   |  0 |  0 | -1 |  1 |  1 |  20 |
| 3   |  1 |  0 |  1 | -1 |  0 |  20 |

---

## SHEET 4: Algo - Primal Simplex

### Canonical Form for Primal Simplex
```
(z) - 3x1 - 2x2 = 0
     2x1 +  x2 + s1       = 100
      x1 +  x2      + s2  = 80
     2x1             + s3 = 40
```
Note: objective row coefficients are negated (cost row form for maximisation).

### Primal Simplex Algorithm — All Iterations

**T-1 (Initial):**
| t-i | x1 | x2 | s1 | s2 | s3 | rhs |  θ  |
|-----|----|----|----|----|-----|-----|-----|
| Z   | -3 | -2 |  0 |  0 |  0 |   0 |  -  |
| 1   |  2 |  1 |  1 |  0 |  0 | 100 |  50 |
| 2   |  1 |  1 |  0 |  1 |  0 |  80 |  80 |
| 3   |  1 |  0 |  0 |  0 |  1 |  40 |  40 |

- Most negative in Z row: x1 (coefficient = -3) → **Entering variable: x1**
- θ ratios: 100/2=50, 80/1=80, 40/1=40 → Minimum θ = 40 (row 3) → **Leaving variable: s3**
- **Pivot element: row 3, column x1 = 1**

---

**T-2 (after x1 enters, s3 leaves):**
| t-2 | x1 | x2 | s1 | s2 | s3 | rhs |  θ  |
|-----|----|----|----|----|-----|-----|-----|
| Z   |  0 | -2 |  0 |  0 |  3 | 120 |  -  |
| 1   |  0 |  1 |  1 |  0 | -2 |  20 |  20 |
| 2   |  0 |  1 |  0 |  1 | -1 |  40 |  40 |
| 3   |  1 |  0 |  0 |  0 |  1 |  40 |  -  |

- Most negative in Z row: x2 (coefficient = -2) → **Entering variable: x2**
- θ ratios: 20/1=20, 40/1=40, row 3 ignored (coefficient 0) → Minimum θ = 20 (row 1) → **Leaving variable: s1**
- **Pivot element: row 1, column x2 = 1**

---

**T-3 (after x2 enters, s1 leaves):**
| t-3 | x1 | x2 | s1 | s2 | s3 | rhs |  θ  |
|-----|----|----|----|----|-----|-----|-----|
| Z   |  0 |  0 |  2 |  0 | -1 | 160 |  -  |
| 1   |  0 |  1 |  1 |  0 | -2 |  20 | -10 |
| 2   |  0 |  0 | -1 |  1 |  1 |  20 |  20 |
| 3   |  1 |  0 |  0 |  0 |  1 |  40 |  40 |

- Most negative in Z row: s3 (coefficient = -1) → **Entering variable: s3**
- θ ratios: 20/(-2) = -10 (negative, skip), 20/1=20, 40/1=40 → Minimum θ = 20 (row 2) → **Leaving variable: s2**
- **Pivot element: row 2, column s3 = 1**

---

**T-4\* (OPTIMAL — after s3 enters, s2 leaves):**
| t-4* | x1 | x2 | s1 | s2 | s3 | rhs |
|------|----|----|----|----|-----|-----|
| Z    |  0 |  0 |  1 |  1 |  0 | **180** |
| 1    |  0 |  1 | -1 |  2 |  0 |  60 |
| 2    |  0 |  0 | -1 |  1 |  1 |  20 |
| 3    |  1 |  0 |  1 | -1 |  0 |  20 |

- All Z row coefficients ≥ 0 → **OPTIMAL**
- **Solution: x1 = 20, x2 = 60, z = 180**
- Basis at T-4*: {x2 (row1), s3 (row2), x1 (row3)}
- Non-basic: {s1=0, s2=0}

> **Verified against `src/Solve/Algorithms/PrimalSimplex.cs`** — our Big-M implementation reaches the
> same optimum (x1=20, x2=60, z=180) in 3 pivots, with the identical entering/leaving sequence
> (x1↔s3, x2↔s1, s3↔s2) and the same final z-row [0, 0, 1, 1, 0 | 180]. Our slack numbering differs
> (s1/s2/s3 assigned in constraint order, same as here) but the tableau values match exactly.

---

## SHEET 5: Algo - Revised Primal Simplex

### Canonical Form for Revised Primal Simplex
```
(z)  3x1 + 2x2               = 0    (positive coefficients, not negated)
     2x1 +  x2 + s1          = 100
      x1 +  x2      + s2     = 80
     2x1             + s3    = 40
```

**Initial Full Tableau (for reference):**
| Row | x1 | x2 | s1 | s2 | s3 | rhs |
|-----|----|----|----|----|-----|-----|
| z   |  3 |  2 |  0 |  0 |  0 |   0 |
| 1   |  2 |  1 |  1 |  0 |  0 | 100 |
| 2   |  1 |  1 |  0 |  1 |  0 |  80 |
| 3   |  1 |  0 |  0 |  0 |  1 |  40 |

---

### T-1 (Initial Iteration)

**Basic Variables (Xbv):** s1, s2, s3
**Non-Basic Variables (Xnbv):** x1, x2
**Basis B = I (identity, since initial basis is all slacks), B⁻¹ = I**
**CbvB⁻¹ = [0, 0, 0]**

**Price Out (Cj* = CbvB⁻¹Aj − Cj for each non-basic):**
| Variable | Aj (column) | CbvB⁻¹Aj | Cj | Cj* |
|----------|------------|-----------|-----|------|
| x1 | [2,1,1]ᵀ | 0 | 3 | **-3** — most negative, enters |
| x2 | [1,1,0]ᵀ | 0 | 2 | **-2** |

**Entering variable: x1**
**A1* = B⁻¹ × A1 = [2, 1, 1]ᵀ**

**Ratio test (θ = b/A1*):**
| Row | b   | A1* | θ    |
|-----|-----|-----|------|
| 1   | 100 |  2  |  50  |
| 2   |  80 |  1  |  80  |
| 3   |  40 |  1  | **40** — minimum |

**Leaving variable: s3 (row 3)** → x1 enters, s3 leaves

---

### T-2

**Xbv = {s1, s2, x1}, Xnbv = {x2, s3}, Cbv = [0, 0, 3], Cnbv = [2, 0]**

**Product Form (Eta matrix E from pivot on row 3, A1* = [2,1,1]ᵀ, pivot element = 1):**
```
E = [ 1   0  -2 ]
    [ 0   1  -1 ]
    [ 0   0   1 ]
```

**New B⁻¹ = E × old B⁻¹:**
```
B⁻¹ = [ 1  0  -2 ]
      [ 0  1  -1 ]
      [ 0  0   1 ]
```

**CbvB⁻¹ = [0,0,3] × B⁻¹ = [0, 0, 3]**

**Price Out for x2** (A2 = [1,1,0]ᵀ): CbvB⁻¹·A2 = 0(1)+0(1)+3(0) = 0 → C2* = 0 − 2 = **-2** (enters)
**Price Out for s3** (A5 = [0,0,1]ᵀ): CbvB⁻¹·A5 = 0(0)+0(0)+3(1) = 3 → C5* = 3 − 0 = +3 (stays out)

**A2* = B⁻¹ × A2 = [1, 1, 0]ᵀ**

**Ratio test for x2:**
| Row | b*  | A2* | θ   |
|-----|-----|-----|-----|
| s1  |  20 |  1  | **20** — minimum |
| s2  |  40 |  1  |  40 |
| x1  |  40 |  0  |  -  |

**Entering: x2, Leaving: s1**

---

### T-3 (first pivot: x2 enters, s1 leaves)

**Xbv = {x2, s2, x1}, Xnbv = {s1, s3}, Cbv = [2, 0, 3], Cnbv = [0, 0]**

**Product Form E (pivot on row 1, A2* = [1,1,0]ᵀ, pivot = 1):**
```
E = [ 1   0   0 ]
    [-1   1   0 ]
    [ 0   0   1 ]
```

**New B⁻¹ = E × old B⁻¹:**
```
B⁻¹ = [ 1   0  -2 ]
      [-1   1   1 ]
      [ 0   0   1 ]
```

**CbvB⁻¹ = [2,0,3] × B⁻¹ = [2, 0, -1]**

**Price Out for s1** (A3 = [1,0,0]ᵀ): 2(1)+0(-1)+(-1)(0) = 2 → C3* = +2 (stays out)
**Price Out for s3** (A5 = [0,0,1]ᵀ): 2(0)+0(-1)+(-1)(1) = -1 → C5* = -1 (enters)

**A5* = B⁻¹ × A5 = [-2, 1, 1]ᵀ**

**Ratio test for s3:**
| Row | b*  | A5* |  θ  |
|-----|-----|-----|-----|
| x2  |  20 | -2  | -10 (negative, skip) |
| s2  |  20 |  1  | **20** — minimum |
| x1  |  40 |  1  |  40 |

**Entering: s3, Leaving: s2**

---

### T-3 (second pivot: s3 enters, s2 leaves — optimal)

**Xbv = {x2, s3, x1}, Xnbv = {s1, s2}, Cbv = [2, 0, 3], Cnbv = [0, 0]**

**Product Form E (pivot on row 2, A5* = [-2,1,1]ᵀ, pivot = 1):**
```
E = [ 1   2   0 ]
    [ 0   1   0 ]
    [ 0  -1   1 ]
```

**New B⁻¹ = E × old B⁻¹:**
```
B⁻¹ = [ -1   2   0 ]
      [ -1   1   1 ]
      [  1  -1   0 ]
```

**CbvB⁻¹ = [2,0,3] × B⁻¹ = [1, 1, 0]** → Shadow prices confirmed: y1=1, y2=1, y3=0

**Price Out for s1** (A3 = [1,0,0]ᵀ): 1(1)+1(0)+0(0) = **1 ≥ 0**
**Price Out for s2** (A4 = [0,1,0]ᵀ): 1(0)+1(1)+0(0) = **1 ≥ 0**

**All Price Out values ≥ 0 → OPTIMAL**

**Final B⁻¹:**
```
B⁻¹ = [ -1   2   0 ]
      [ -1   1   1 ]
      [  1  -1   0 ]
```

**rhs\* = B⁻¹ × b = [60, 20, 20]ᵀ → x2=60, s3=20, x1=20**

### Optimal Tableau (T-\*)
| Row | x1 | x2 | s1 | s2 | s3 | rhs |
|-----|----|----|----|----|-----|-----|
| z   |  0 |  0 |  1 |  1 |  0 | **180** |
| 1   |  0 |  1 | -1 |  2 |  0 |  60 |
| 2   |  0 |  0 | -1 |  1 |  1 |  20 |
| 3   |  1 |  0 |  1 | -1 |  0 |  20 |

**Solution: x1 = 20, x2 = 60, z = 180**

---

## SENSITIVITY ANALYSIS REFERENCE

### Graphical Sensitivity Ranges (from Sheet 1: LP)

#### Range for c1 (Soldiers objective coefficient, currently = 3)
- Binding constraints at optimum: b1 (Finishing), b2 (Carpentry)
- Maximum decrease ratio: c1:2 = b1 ratio → c1=2
- Maximum increase ratio: c1:2 = b2 ratio → c1=4
- **Range: 2 <= c1 <= 4**  (allowable change: -1 <= Δ <= +1)

#### Range for c2 (Trains objective coefficient, currently = 2)
- Maximum decrease ratio: 3:c2 = 2:1 → c2 = 1.5
- Maximum increase ratio: 3:c2 = 1:1 → c2 = 3
- **Range: 1.5 <= c2 <= 3**  (allowable change: -0.5 <= Δ <= +1)

#### Range for b1 (Finishing RHS, currently = 100)
- Maximum increase: +20 → b1 <= 120
- Maximum decrease: -20 → b1 >= 80
- **Range: 80 <= b1 <= 120**  (Δ: -20 <= Δ <= +20)

#### Range for b2 (Carpentry RHS, currently = 80)
- Maximum increase: +20 → b2 <= 100
- Maximum decrease: -20 → b2 >= 60
- **Range: 60 <= b2 <= 100**  (Δ: -20 <= Δ <= +20)

#### Range for b3 (Demand RHS, currently = 40)
- Maximum increase: unbounded
- Maximum decrease: -20 → b3 >= 20
- **Range: 20 <= b3 <= infinity**  (Δ: -20 <= Δ <= infinity)

---

## QUICK REFERENCE — EXPECTED VALUES

Use these to verify your implementation:

| Check | Expected Value |
|-------|---------------|
| Optimal x1 | 20 |
| Optimal x2 | 60 |
| Optimal z  | 180 |
| Shadow price (Finishing, y1) | 1 |
| Shadow price (Carpentry, y2) | 1 |
| Shadow price (Demand, y3)    | 0 |
| Dual optimal (Min w) | 180 |
| Duality gap | 0 (Strong Duality) |
| c1 range | [2, 4] |
| c2 range | [1.5, 3] |
| b1 range | [80, 120] |
| b2 range | [60, 100] |
| b3 range | [20, infinity] |
| Primal Simplex iterations | 4 (T-1 to T-4*) |
| Revised Simplex iterations | 3 (T-1 to T-3, then optimal) |
| B⁻¹ at optimum | [[-1,2,0],[-1,1,1],[1,-1,0]] |
| CbvB⁻¹ at optimum | [1, 1, 0] |
