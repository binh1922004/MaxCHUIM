---
name: implement-maxc-hui
description: Helps with implementing the MaxC-HUIM algorithm in .NET. 
---

# MaxC-HUIM Implementation Plan in .NET

> Based on: *Duong, H., Hoang, T., Tran, T., Truong, T., Le, B., Fournier-Viger, P. (2022). "Efficient algorithms for mining closed and maximal high utility itemsets." Knowledge-Based Systems 257, 109921.*

This document is a step-by-step plan to implement the **MaxC-HUIM algorithm** (which simultaneously mines Closed High Utility Itemsets — CHUIs — and Maximal High Utility Itemsets — MaxHUIs) in C# / .NET. The companion algorithm **C-HUIM** (mining only CHUIs) reuses the same code path.

---

## Table of Contents

1. [Project Setup & Domain Models](#-phase-1-project-setup--domain-models)
2. [Dataset Loading & Preprocessing](#-phase-2-dataset-loading--preprocessing)
3. [Core Data Structures](#-phase-3-core-data-structures)
4. [Pruning Strategies](#-phase-4-pruning-strategies)
5. [Result Set Management](#-phase-5-result-set-management)
6. [Main Algorithm — MaxC-HUIM](#-phase-6-main-algorithm--maxc-huim)
7. [Optimization Notes for .NET](#-phase-7-optimization-notes-for-net)
8. [Validation & Testing](#-phase-8-validation--testing)
9. [CLI](#-phase-9-cli)
10. [Milestones](#-suggested-milestones)

---

## 📋 Phase 1: Project Setup & Domain Models

### 1.1 Solution Structure

```
MaxCHUIM.sln
├── MaxCHUIM.Core/              # Core algorithm library
│   ├── Models/                 # Domain entities
│   ├── DataStructures/         # TPUT, MPUN-List, Hash tables
│   ├── Algorithms/             # MaxC-HUIM, C-HUIM
│   ├── Utilities/              # Helpers (IO, sorting)
│   └── Pruning/                # Pruning strategies
├── MaxCHUIM.IO/                # Dataset readers/writers
├── MaxCHUIM.CLI/               # Console runner
├── MaxCHUIM.Tests/             # xUnit tests
└── MaxCHUIM.Benchmarks/        # BenchmarkDotNet perf tests
```

Target framework: **.NET 8** (LTS). Use C# 12.

### 1.2 Core Domain Models

- `QItem(int Item, int Quantity)` — record struct
- `Transaction { int Tid; List<QItem> QItems; long TU; }`
- `QuantitativeDatabase` — list of transactions + profit dictionary
- `Itemset` — sorted `int[]` with cached hash code
- `ProfitVector : Dictionary<int, int>`

---

## 📋 Phase 2: Dataset Loading & Preprocessing

### 2.1 Input Reader

- Parse SPMF-format files: `item:tu:item util item util ...` (compatible with the paper's benchmarks: Chess, Connect, Pumsb, Retail, etc.)
- Build the **integrated QDB D'** (Definition 2): pre-compute `q' = q × p(a)` once

### 2.2 Build Reduced Dataset (RD)

Per Section 4.3.1:

1. First scan: compute `TWU(aj)` for every item.
2. Remove items where `TWU(aj) < mu`.
3. Remove empty transactions.
4. Sort items inside each transaction by **ascending TWU** (`≺twu`).
5. Sort transactions by descending TWU order of items.
6. Compute `maxTWU` and `newms = max(1, ⌈mu / maxTWU⌉)` (Remark 1.a).

---

## 📋 Phase 3: Core Data Structures

### 3.1 TPUT (Transaction Prefix Utility Tree) — Definition 14

```csharp
public class TputNode {
    public int Item;
    public int Nid;                              // node identifier
    public List<(int Tid, int Util)> Lu;         // tid + utility list
    public TputNode ParentLink;
    public TputNode NextLink;                    // same-item chain
    public Dictionary<int, TputNode> Children;
}

public class Tput {
    public TputNode Root;
    public Dictionary<int, TputNode> HeaderTable; // item → first node
    public int NextNid;
}
```

**Construction**: single pass over RD, inserting each transaction along the prefix tree.

### 3.2 MPUN-List — Definitions 15–17

```csharp
public struct MpunElement {
    public int Nid;     // node id in TPUT
    public long Nu;     // utility
    public long Nru;    // remaining utility
    public long Npu;    // prefix utility
    public int Nsup;    // support
}

public class MpunList {
    public int Item;                  // last item
    public List<MpunElement> Elements;
    public bool PruningNonMCBr;       // LPSNonCHUB flag
}
```

**Two construction paths:**

- **2-itemsets**: walk TPUT header chains, find ancestor relationships (Definition 16).
- **k-itemsets (k > 2)**: join two (k-1)-MPUN-lists by matching `nid` (Definition 17).

### 3.3 Aggregation Helpers (Proposition 4)

```csharp
long Utility() => Elements.Sum(e => e.Nu);
long Ru()      => Elements.Sum(e => e.Nru);
int  Support() => Elements.Sum(e => e.Nsup);
long Fwub()    => Elements.Where(e => e.Nru > 0).Sum(e => e.Nu + e.Nru);
```

---

## 📋 Phase 4: Pruning Strategies

### 4.1 SPWUB (Theorem 1) — Weak Upper Bound

Prune `fbranch(A)` when `fwub(A) < mu`. Strictly tighter than `feub` and `TWU`.

### 4.2 PSNonCHUB (Theorem 3) — Closure Check via Hash

Store CHUI set in a **TWU-keyed hash table** (Remark 1.b):

```csharp
Dictionary<long, List<Itemset>> chuiByTwu;

bool CheckBackward(Itemset B, int suppB, long twuB) {
    if (!chuiByTwu.TryGetValue(twuB, out var bucket)) return false;
    return bucket.Any(C => C.Count > B.Count
                        && SuppOf(C) == suppB
                        && C.IsSupersetOf(B));
}
```

### 4.3 LPSNonCHUB (Theorem 4) — Local Pruning at Two Levels

While extending sibling `C = P⊕x` with item `y` (next sibling `B = P⊕y`):

- If a forward extension `S = C⊕y` has `supp(S) == supp(B)`, **mark** `B.PruningNonMCBr = true`.
- Skip B's whole branch — no inclusion checks needed.

---

## 📋 Phase 5: Result Set Management

### 5.1 CHUI Store

- Hash table keyed by TWU (Remark 1.b).
- Each entry: itemset + support + utility.

### 5.2 MaxHUI Store (`UpdateMHUI` Procedure)

When adding a new HUI `A`:

1. If any existing maximal `M` exists with `M ⊃ A` → do **not** add.
2. Otherwise add `A`, and **remove** any existing `M' ⊂ A`.
3. Use a TWU-bucketed structure to limit comparisons.

---

## 📋 Phase 6: Main Algorithm — MaxC-HUIM

### 6.1 `MaxCHUIM(D, mu)` — Algorithm 1

```text
1. Build D' and TWU per item
2. Build RD (Phase 2.2)
3. Build TPUT (Phase 3.1)
4. CHUI = ∅; MaxHUI = ∅; newms = max(1, ⌈mu / maxTWU⌉)
5. For each aj in HeaderTable (ordered by ≺twu):
     if fwub({aj}) < mu  → Update + skip                    // SPWUB
     if supp(aj) < newms → skip                             // newms opt
     Build all 2-itemset MPUN-lists {aj⊕ak | k ≻twu j}
     UpdateMaxCHUI({aj}, mls, ...)
     if any extension survives → Find-MaxCHUI(mls, {aj}, ...)
```

### 6.2 `Find-MaxCHUI` — Algorithm 2 (Recursive)

For each `MLj` in `MLs`:

1. `A = prefix ⊕ MLj.item`
2. If `fwub(A) < mu` → `Update(A)` then **return** (SPWUB)
3. If `supp(A) < newms` → skip
4. If `MLj.PruningNonMCBr == true` → skip (LPSNonCHUB)
5. If `CheckBackward(A) == true` → skip (PSNonCHUB)
6. Build extension MPUN-lists `MLjk` for each later `MLk`; apply LPSNonCHUB mark
7. `UpdateMaxCHUI`
8. Recurse into surviving extensions

### 6.3 `Update` — Algorithm 4

If `u(A) ≥ mu`: add A to CHUI; call `UpdateMHUI(A)`.

### 6.4 C-HUIM Variant

Same code path, but skip the `UpdateMHUI` call.

---

## 📋 Phase 7: Optimization Notes for .NET

| Concern       | Approach                                                                                   |
|---------------|--------------------------------------------------------------------------------------------|
| Allocations   | Use `struct` for MPUN elements; pool `List<T>` instances                                   |
| Memory        | `Span<T>` / `ArrayPool<MpunElement>` for hot loops                                         |
| Hashing       | Precompute `Itemset.GetHashCode()` from sorted ints                                        |
| Big QDBs      | Use `int` TIDs; `long` for utilities (to avoid overflow on Chainstore)                     |
| Determinism   | Stable sort by TWU then by item id for tie-break                                           |
| Parallelism   | Optional: parallelize the outermost loop over header items (independent branches; CHUI/MaxHUI stores need locks or per-thread buffers merged at the end) |

---

## 📋 Phase 8: Validation & Testing

### 8.1 Unit Tests (xUnit)

- TWU / feub / **fwub** calculations on the paper's running example (Tables 1–2)
- TPUT construction from RD (compare to Fig. 2)
- MPUN-list joins (Definition 17 example: `acd`)
- Result for `mu = 370` must equal:
  - `CHUI = {cde, de, f}`
  - `MaxHUI = {cde, f}` (from §4.3.2 walkthrough)

### 8.2 Integration Tests

- Compare against SPMF Java reference (CHUI-Miner, EFIM-Closed) outputs on Chess, Mushroom, Retail at multiple `mu` thresholds.
- Sets must match exactly.

### 8.3 Benchmarks (BenchmarkDotNet)

- Datasets: Chess, Connect, Mushroom, Pumsb, Retail, Chainstore.
- Report runtime + peak working set; compare with paper's Tables 5–6.

---

## 📋 Phase 9: CLI

```text
MaxCHUIM.CLI --input chess.txt --mu 0.13 --mode maxc --output result.txt
  --mode   = c | maxc
  --format = spmf | csv
```

---

## 📅 Suggested Milestones

| #   | Deliverable                                       | Est. effort |
|-----|---------------------------------------------------|-------------|
| M1  | Project skeleton + models + SPMF reader           | 0.5 day     |
| M2  | TWU computation + RD + TPUT                       | 1 day       |
| M3  | MPUN-list (2-itemset + k-itemset join)            | 1.5 days    |
| M4  | Update + UpdateMHUI + result stores               | 1 day       |
| M5  | MaxC-HUIM main loop + SPWUB                       | 1 day       |
| M6  | PSNonCHUB + LPSNonCHUB pruning                    | 1 day       |
| M7  | Tests against running example (`mu = 370`)        | 0.5 day     |
| M8  | C-HUIM variant + CLI                              | 0.5 day     |
| M9  | Benchmarks + tuning                               | 1–2 days    |

**Total: ~8–10 days** for a working, validated implementation.

---

## 📚 Key References

- **Theorem 1** — fwub is tighter than feub and TWU; basis for SPWUB.
- **Theorem 3** — PSNonCHUB: prune non-closed HUI branches via CHUI lookup.
- **Theorem 4** — LPSNonCHUB: local pruning between two successive prefix-tree levels without inclusion checks.
- **Remark 1.a** — `newms` optimization based on `mu` and `maxTWU`.
- **Remark 1.b** — TWU-keyed hashing for CHUI lookup.
- **Definitions 14–17** — TPUT and MPUN-list construction.
- **Algorithms 1–4** — MaxC-HUIM, Find-MaxCHUI, UpdateMaxCHUI, Update.
