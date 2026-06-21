---
name: implement-bitmax
description: Helps with implementing the Bitmax algorithm in .NET. 
---
# BM-MaxHUI: Bitmax Integration Plan for the .NET MaxC-HUIM Implementation

> Based on: *Section 3 — "BM-MaxHUI Method Based on Bitmax Representation"*
> Extends: `MaxC-HUIM_DotNet_Plan.md` (Duong et al., 2022 base algorithm)

This document describes how to **integrate the Bitmax representation** into the existing .NET MaxC-HUIM solution to produce **BM-MaxHUI** — a version that replaces sequential CHUI/MaxHUI containment scans with **64-bit word bitwise operations**.

> ⚠️ **Important invariant**: BM-MaxHUI preserves the search space, pruning conditions, and result sets of MaxC-HUIM. Only the *representation, checking, and updating* of result sets change. No correctness loss.

---

## Table of Contents

1. [Motivation & Design Goals](#-1-motivation--design-goals)
2. [New Data Structures](#-2-new-data-structures)
3. [Update Mechanisms](#-3-update-mechanisms)
4. [Algorithm Integration](#-4-algorithm-integration)
5. [.NET-Specific Implementation Notes](#-5-net-specific-implementation-notes)
6. [Validation & Testing](#-6-validation--testing)
7. [Complexity Tracking & Benchmarks](#-7-complexity-tracking--benchmarks)
8. [Milestones](#-8-suggested-milestones)
9. [Risks & Mitigations](#-9-risks--mitigations)

---

## 📋 1. Motivation & Design Goals

### 1.1 Bottleneck in MaxC-HUIM
- Every candidate `A` must check **inclusion** against the discovered `CHUI` and `MaxHUI` sets.
- Sequential scan cost: `O(k · (|X| + len_avg))` per candidate.
- As `k` grows (low μ, dense datasets), this dominates runtime.

### 1.2 BM-MaxHUI Goals
- Replace inclusion checks with **bitwise AND over 64-bit words**: `O(|X| · ⌈k/64⌉)`.
- Avoid rebuilding bit vectors on every MaxHUI invalidation → use a **Validity Mask**.
- Skip large empty regions with a **block-index layer** (H-MaxBitmax).
- Preserve the existing `Find-MaxCHUI` traversal and pruning logic.

### 1.3 What Changes vs. Base Plan
| Component | MaxC-HUIM (base) | BM-MaxHUI (new) |
|---|---|---|
| MaxHUI containment | Linear scan + subset test | `BM_MaxHUI(x)` AND with Validity Mask |
| MaxHUI invalidation | Remove from list | Flip bits in `V` (DiffSub) |
| CHUI 1-backward filter | TWU hash → subset test | `BM_CHUI(x)` AND → TWU hash → final verify |
| Search-space pruning | unchanged | **unchanged** |
| Result sets returned | unchanged | **unchanged** |

---

## 📋 2. New Data Structures

### 2.1 `BitVector64` — 64-bit Word Backed Bit Vector
Foundation for all Bitmax structures.

```csharp
public sealed class BitVector64 {
    private ulong[] _words;     // packed 64-bit storage
    private int _bitLength;     // logical length k

    public int BitLength => _bitLength;
    public int WordCount => _words.Length;
    public ReadOnlySpan<ulong> Words => _words;

    public bool Get(int i);
    public void Set(int i, bool value);
    public void AppendBit(bool value);              // grows storage
    public bool IsZero();                           // all-zero check (SIMD friendly)
    public int PopCount();                          // BitOperations.PopCount per word

    // In-place / out-of-place bitwise ops
    public void AndInPlace(BitVector64 other);
    public void OrInPlace(BitVector64 other);
    public void NotInPlace();
    public static BitVector64 And(BitVector64 a, BitVector64 b);
    public static BitVector64 Or (BitVector64 a, BitVector64 b);
    public static BitVector64 Not(BitVector64 a);
    public static bool AndIsNonZero(BitVector64 a, BitVector64 b); // short-circuit
}
```

**Implementation hints**:
- Use `ulong` (64-bit). Grow by doubling, like `List<T>`.
- Hot loops should iterate over `Span<ulong>` — JIT will auto-vectorize where possible.
- Add `System.Numerics.BitOperations.PopCount(ulong)` for density checks.

### 2.2 `MaxBitmax` — Item-wise MaxHUI Encoding (Definition 2)
```csharp
public sealed class MaxBitmax {
    private readonly Dictionary<int, BitVector64> _bm;  // item → BM(item)
    private int _maxHuiCount;                            // k

    public int MaxHuiCount => _maxHuiCount;
    public BitVector64 GetOrCreate(int item);

    /// Bit column for a newly-recorded MaxHUI M.
    /// For every item x ∈ M  → AppendBit(true)
    /// For every item x ∉ M  → AppendBit(false)
    public int AppendMaxHui(IReadOnlyList<int> M, IEnumerable<int> universe);

    /// P := AND over BM(x), x ∈ X.  Theorem 1.
    public BitVector64 Intersect(IReadOnlyList<int> X);
}
```

### 2.3 `HMaxBitmax` — Two-layer Block Index (Definition 3)
```csharp
public sealed class HMaxBitmax {
    public BitVector64 BM2(int item);   // detailed layer (= MaxBitmax)
    public BitVector64 BM1(int item);   // one bit per 64-bit block of BM2
    // BM1 bit j = OR of BM2 bits in block B_j

    /// Returns the set of BM2 blocks that still need detailed checking.
    public IEnumerable<int> CandidateBlocks(IReadOnlyList<int> X);
}
```
**Rule**: only build BM1 when `k > THRESHOLD_K` (e.g. > 4096) — otherwise pure `MaxBitmax` is enough.

### 2.4 `ValidityMask` (Definition 4)
```csharp
public sealed class ValidityMask {
    private BitVector64 _v;
    public BitVector64 Vector => _v;
    public void AppendValid();                 // V.append(1)
    public void InvalidateMany(BitVector64 maskSubset);  // V := V ∧ ¬maskSubset
    public double Density();                   // popcount / length
}
```

### 2.5 `BmChui` — CHUI Encoding for 1-backward Filtering (§3.2.2)
Same shape as `MaxBitmax` but no Validity Mask (CHUIs are never invalidated — they are append-only).

```csharp
public sealed class BmChui {
    public int Append(IReadOnlyList<int> C);    // returns the bit index
    public BitVector64 Intersect(IReadOnlyList<int> X);
}
```

### 2.6 Universe of Items (`I`)
DiffSub needs `I_out = I \ M`. Cache the sorted item universe (post-RD filtering) as `int[] _itemUniverse` to enumerate complement cheaply.

---

## 📋 3. Update Mechanisms

### 3.1 DiffSub — Subset Detection (Theorem 4)
Given a new MaxHUI `M`:
1. `I_out = I \ M`
2. `Mask_out = OR over BM_MaxHUI(y), y ∈ I_out`
3. `Mask_subset = (¬Mask_out) ∧ V`
4. Every bit set in `Mask_subset` corresponds to an old valid MaxHUI ⊆ M.

```csharp
public BitVector64 ComputeSubsetMask(IReadOnlyList<int> M) {
    var iOut = _itemUniverse.Where(x => !M.Contains(x));
    var maskOut = new BitVector64(_validity.Vector.BitLength);
    foreach (var y in iOut) maskOut.OrInPlace(_maxBitmax.GetOrCreate(y));
    maskOut.NotInPlace();
    maskOut.AndInPlace(_validity.Vector);
    return maskOut; // == Mask_subset
}
```

### 3.2 Invalidation
`V := V ∧ (¬Mask_subset)` — single bitwise pass, **no rebuild of `BM_MaxHUI`**.

### 3.3 Append New MaxHUI
- For every `x ∈ I`: append bit `1` if `x ∈ M`, else `0`.
- `V.AppendValid()`.

### 3.4 Periodic Compression
Track `density(V)`. When `density(V) < τ` (e.g. `τ = 0.5`):
1. Enumerate valid columns (bits where `V[i] = 1`).
2. Rebuild `BM_MaxHUI` keeping only those columns.
3. Reset `V` to all-ones over `k_valid`.
- Cost: `O(|I| · ⌈k_valid / 64⌉)` — amortized over many updates.

```csharp
public void CompressIfNeeded(double tau = 0.5) {
    if (_validity.Density() >= tau) return;
    var validIdx = EnumerateValidColumnIndices(_validity.Vector);
    foreach (var item in _itemUniverse)
        _maxBitmax.RebuildFromValidColumns(item, validIdx);
    _validity.ResetAllOnes(validIdx.Count);
}
```

### 3.5 BM_CHUI Closedness Pipeline (Theorem 5 + Lemma 1)
Three layers, **in this order**:
1. **Bitmax layer**: `P = AND BM_CHUI(x) for x ∈ X`. If `P = 0` → no 1-backward, **fast skip**.
2. **TWU-hash layer**: keep only `C_i` with `TWU(C_i) = TWU(X)` and `P[bitIndex(C_i)] = 1`.
3. **Final verify**: assert `X ⊂ C_i` and `supp(X) = supp(C_i)`.

This replaces the `CheckBackward` method from the base plan.

---

## 📋 4. Algorithm Integration

### 4.1 New / Updated Solution Components
```
MaxCHUIM.Core/
├── DataStructures/
│   ├── BitVector64.cs           ★ NEW
│   ├── MaxBitmax.cs             ★ NEW
│   ├── HMaxBitmax.cs            ★ NEW
│   ├── ValidityMask.cs          ★ NEW
│   ├── BmChui.cs                ★ NEW
│   └── (existing TPUT, MPUN-list)
├── Algorithms/
│   ├── BmMaxHuiAlgorithm.cs     ★ NEW (BM-MaxHUI-Main)
│   ├── FindBmMaxCHUI.cs         ★ NEW (replaces Find-MaxCHUI)
│   ├── UpdateBmMaxCHUI.cs       ★ NEW (replaces UpdateMaxCHUI)
│   └── (existing MaxC-HUIM kept for benchmark comparison)
└── Pruning/
    └── PsNonCHUB_Bitmax.cs      ★ NEW (Bitmax-based)
```

### 4.2 `BM-MaxHUI-Main` (Figure 1)
```text
1. Build integrated D' and TWU(a) for each item
2. Build RD and TPUT
3. CHUI = ∅; MaxHUI = ∅
4. BM_CHUI = ∅; BM_MaxHUI = ∅
5. V = ∅
6. For each item a in HeaderTable (ascending TWU):
   7.  if fwub(a) < μ → continue                          // SPWUB
   8.  newms = current support threshold
   9.  if supp(a) < newms → continue
   10. Build MLs_a (2-itemset MPUN-lists)
   11. Determine 1-forward status of {a}
   12. backward = PSNonCHUB_Bitmax({a})                   // §3.2.2
   13. isClosed = !hasForward && !backward
   14. UpdateBmMaxCHUI({a}, isClosed)
   15. if MLs_a ≠ ∅ → FindBmMaxCHUI(MLs_a, {a}, ...)
17. return CHUI, MaxHUI
```

### 4.3 `Find-BM-MaxCHUI` (Figure 2)
For each `ML_j` in `MLs`:
1. `A = prefix ∪ {ML_j.item}`
2. SPWUB / newms / LPSNonCHUB checks (unchanged from MaxC-HUIM)
3. Build extensions `exMLs`; track `hasForward` and `pruningNonMCBr` flags on siblings
4. `noBackward = !PSNonCHUB_Bitmax(A)`
5. `isClosed = noBackward && !hasForward`
6. `UpdateBmMaxCHUI(A, isClosed)`
7. **Important**: continue to recurse on `exMLs` even if `!isClosed` — deeper extensions can still produce CHUIs/MaxHUIs

### 4.4 `PSNonCHUB_Bitmax(A)` (Figure 3)
```csharp
public bool PsNonCHUB_Bitmax(IReadOnlyList<int> A,
                             int suppA, long twuA) {
    // Layer 1 — Bitmax
    var P = _bmChui.Intersect(A);
    if (P.IsZero()) return false;

    // Layer 2 — TWU hash
    if (!_chuiByTwu.TryGetValue(twuA, out var bucket)) return false;

    // Layer 3 — direct verification on candidates whose bit-id is set in P
    foreach (var (C, suppC, bitId) in bucket) {
        if (!P.Get(bitId)) continue;
        if (C.Count <= A.Count) continue;
        if (suppC != suppA) continue;
        if (IsProperSuperset(C, A)) return true;
    }
    return false;
}
```

### 4.5 `UpdateBmMaxCHUI` (Figure 4)
```csharp
public void UpdateBmMaxCHUI(IReadOnlyList<int> A, bool isClosed) {
    long utilA = ...;             // already known from MPUN aggregation
    if (utilA < mu) return;

    // CHUI side
    if (isClosed) {
        int bitId = _bmChui.Append(A);
        _chui.Add(A);
        _chuiByTwu.Add(TWU(A), (A, suppA, bitId));
    }

    // MaxHUI side — every HUI is a MaxHUI candidate
    var Pvalid = _maxBitmax.Intersect(A);
    Pvalid.AndInPlace(_validity.Vector);

    if (Pvalid.IsZero()) {
        // No valid MaxHUI contains A. A is a new MaxHUI.
        // 1) DiffSub: invalidate any valid MaxHUI that is a subset of A
        var maskSubset = ComputeSubsetMask(A);
        _validity.InvalidateMany(maskSubset);

        // 2) Append A as a new column
        _maxBitmax.AppendMaxHui(A, _itemUniverse);
        _validity.AppendValid();
        _maxHui.Add(A);

        // 3) Periodic compression
        if (_validity.Density() < TAU) CompressMaxBitmap();
    }
}
```

### 4.6 Containment-only vs. Pruning
> The Bitmax-based containment check **only decides whether `A` is added** to the result sets.
> It must **NOT** prune the search branch — extensions of `A` may still produce MaxHUIs.
> This is exactly the rule stated under Theorem 1 in §3.1.1.

---

## 📋 5. .NET-Specific Implementation Notes

### 5.1 Bitwise Performance
- Use `ulong` arrays + `Span<ulong>` for AND/OR/NOT loops.
- Call `System.Numerics.BitOperations.PopCount(ulong)` for density.
- Consider `System.Runtime.Intrinsics` (`Vector256<ulong>`) for AVX2 acceleration on hot AND loops; gate behind `Avx2.IsSupported`.

```csharp
public static bool AndIsNonZero(ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b) {
    int len = Math.Min(a.Length, b.Length);
    int i = 0;
    if (Avx2.IsSupported && len >= 4) {
        for (; i + 4 <= len; i += 4) {
            var va = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(a[i..]));
            var vb = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(b[i..]));
            if (!Vector256.EqualsAll(Vector256.BitwiseAnd(va, vb), Vector256<ulong>.Zero))
                return true;
        }
    }
    for (; i < len; i++) if ((a[i] & b[i]) != 0) return true;
    return false;
}
```

### 5.2 Memory Layout
- Per-item `BitVector64` may grow many times. Use exponential growth + `ArrayPool<ulong>` for transient ANDs to avoid GC churn.
- Keep `BM_CHUI` and `BM_MaxHUI` as `Dictionary<int, BitVector64>` keyed by item id (sparse over `I`).

### 5.3 Compression Threshold τ
- Default `τ = 0.5` (paper-typical). Make it configurable.
- Use a small heuristic: if a single update invalidates ≥ `Δ` bits, also re-evaluate compression.

### 5.4 Block Indexing (H-MaxBitmax)
- Activate `BM1` layer only once `k_M > 4096` (i.e. more than one BM1 word).
- Rebuild `BM1(x)` lazily on each AppendMaxHui by setting the current block bit.

### 5.5 Thread Safety
- Single-threaded by default. If parallelizing the outermost item loop:
  - Per-thread local `BM_CHUI`/`BM_MaxHUI` not feasible (shared correctness).
  - Use a coarse lock around `UpdateBmMaxCHUI`, or batch updates and merge at sync points.

---

## 📋 6. Validation & Testing

### 6.1 Reproduce Paper's Running Example (Tables 1–2)
- Database `D` (6 transactions, items a–e), profits `{3,2,1,2,4}`, **μ = 12**.
- Expected output:
  - `CHUI(D,12) = { {a,c}, {b,c}, {c,d}, {a,c,d}, {a,c,d,e} }`
  - `MaxHUI(D,12) = { {b,c}, {a,c,d,e} }`

### 6.2 Step-by-step Bit-State Assertions (§3.3.2)
| Step | Action | Assertion |
|---|---|---|
| 1 | Add `M₁ = {b,c}` | `V = 1`, `BM(b)=1`, `BM(c)=1` |
| 2 | Add `M₂ = {e,d}` then `M₃ = {e,d,a}` | `Mask_subset = 01` → `V = 101` |
| 3 | Add `M₄ = {e,d,a,c}` | `Mask_subset = 001` → `V = 1001` |
| 4 | Query `X = {a,c}` | `P_valid = 0001 ≠ 0` → **not** added |

Write each as an xUnit `[Fact]` so any regression in DiffSub/Validity is caught immediately.

### 6.3 Bit-mechanic Unit Tests
- `BitVector64`: get/set, append, grow, AND/OR/NOT, popcount, AndIsNonZero.
- `MaxBitmax.AppendMaxHui` — bit columns line up across items.
- `HMaxBitmax`: block-skip example from Table 4 (e.g. `X = {a,d}` skips `B₁`; `X' = {b,d}` skips both blocks).
- `BmChui` pipeline (Example 5 / Table 5): for `X = {a,c}`, the AND eliminates `C₂` and `C₄` at the Bitmax layer.

### 6.4 Equivalence Tests vs. Base MaxC-HUIM
- For each benchmark dataset (Chess, Mushroom, Retail, Pumsb at several μ values), run both `MaxCHUIM` and `BmMaxHUI`.
- Assert **set equality** of CHUI and MaxHUI outputs.
- Use this as the primary correctness oracle.

### 6.5 Stress Tests for Compression
- Build a synthetic stream where 90 % of MaxHUIs are invalidated.
- Assert: result correctness preserved, peak memory bounded by `O(|I| · k_valid / 64)`.

---

## 📋 7. Complexity Tracking & Benchmarks

### 7.1 Counters to Expose
Add instrumentation (behind `--profile` flag):
- `maxhui_containment_checks`
- `bitmax_and_ops`
- `diffsub_invocations` + total `|I_out|`
- `compression_events` and `k_valid` per event
- `chui_bitmax_filter_hits` / `chui_bitmax_filter_misses`

### 7.2 Reference Costs (Table 6, paper)
| Operation | BM-MaxHUI |
|---|---|
| MaxHUI containment + V | `O(|X| · ⌈k_M/64⌉)` |
| H-MaxBitmax general | `O(|X| · ⌈k_M/4096⌉ + |X|·q_X)` |
| H-MaxBitmax worst case | `O(|X| · ⌈k_M/64⌉)` |
| DiffSub + mask | `O(|I_out| · ⌈k_M/64⌉ + ⌈k_M/64⌉)` |
| Append new MaxHUI | `O(|M|)` |
| Periodic compression | `O(|I| · ⌈k_valid/64⌉)` |
| BM_CHUI layer-1 filter | `O(|X| · ⌈k_C/64⌉)` |

### 7.3 Space (Table 7)
- Total auxiliary: `O(|I|·⌈k_M/64⌉ + |I|·⌈k_C/64⌉ + |I|·⌈k_M/4096⌉ + ⌈k_M/64⌉)`

### 7.4 BenchmarkDotNet Targets
- `MaxCHUIM` vs `BmMaxHUI` on Chess @ μ ∈ {19%, 16%, 14%, 13%}.
- `BmMaxHUI` with/without H-MaxBitmax on Connect/Pumsb at low μ.
- Report: runtime, peak working set, allocations, GC counts.

---

## 📅 8. Suggested Milestones

| # | Deliverable | Est. effort |
|---|---|---|
| B1 | `BitVector64` + unit tests (AND/OR/NOT, popcount, grow) | 0.5 day |
| B2 | `MaxBitmax`, `BmChui`, `ValidityMask` + their unit tests | 1 day |
| B3 | DiffSub + compression (with §3.3.2 step-by-step asserts) | 1 day |
| B4 | New `UpdateBmMaxCHUI` + integration into existing `Find-MaxCHUI` | 1 day |
| B5 | `PSNonCHUB_Bitmax` (3-layer pipeline) + Example 5 test | 0.5 day |
| B6 | `HMaxBitmax` block-index layer (gated by k_M threshold) | 1 day |
| B7 | Equivalence tests vs. base MaxC-HUIM (Chess/Mushroom/Retail/Pumsb) | 1 day |
| B8 | AVX2 / `Vector256` acceleration on hot AND loops | 0.5–1 day |
| B9 | BenchmarkDotNet suite + report | 1 day |

**Total: ~7–8 days** on top of the existing MaxC-HUIM implementation.

---

## ⚠️ 9. Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Bit-index drift between `BM_MaxHUI` and `V` after compression | Centralize all column appends/removals through `MaxBitmaxStore` so they always update in lock-step. Add an invariant check: `BM(x).BitLength == V.BitLength` for every item. |
| `BM_CHUI` grows unbounded (never invalidated) | This is by design — CHUIs are monotone. Use sparse-by-item dictionary; do **not** add a Validity Mask here. |
| Inclusion test cost dominates again at low μ | Combine Bitmax (Theorem 5) with the existing TWU-hash (Lemma 1) — both gates, in this order. |
| Off-by-one when computing `I_out` for DiffSub | Use the post-RD universe (after items with `TWU < μ` are dropped). Snapshot it once and reuse. |
| Block-index false positives in H-MaxBitmax | Always finish with detailed `BM2` check (Theorem 2 losslessness). |
| Misuse of `isClosed` to prune branches | Document at the call site: `isClosed` controls **insertion**, never **traversal** (§3.3.1). |

---

## 📚 Key References (this paper)

- **Definition 2** — `MaxBitmax`: per-item bit vector over discovered MaxHUIs.
- **Definition 3** — `H-MaxBitmax`: two-layer block index over `MaxBitmax`.
- **Definition 4** — Validity Mask `V`.
- **Theorem 1** — Containment via AND of `BM(x_i)`.
- **Theorem 2** — Losslessness of the block index.
- **Theorem 3** — Validity-Mask correctness.
- **Theorem 4** — DiffSub correctness.
- **Theorem 5** — Losslessness of `BM_CHUI` filter for closedness.
- **Lemma 1** — TWU equality under same-support superset.
- **Theorems 6 & 7** — Soundness and Completeness of BM-MaxHUI.
- **Figures 1–4** — Pseudocode of `BM-MaxHUI-Main`, `Find-BM-MaxCHUI`, `PSNonCHUB`, `UpdateBMMaxCHUI`.
- **Figure 5 / §3.3.2** — Worked example on database `D`, μ = 12.
- **Tables 6 & 7** — Time and space complexity summaries.
