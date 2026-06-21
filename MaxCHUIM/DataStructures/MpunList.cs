using System;
using System.Collections.Generic;
using MaxCHUIM.Models;

namespace MaxCHUIM.DataStructures;

public class MpunList
{
    public int Item { get; set; }                  // Last item in the itemset
    public List<MpunElement> Elements { get; set; } = new();
    public bool PruningNonMCBr { get; set; }       // LPSNonCHUB flag

    public MpunList(int item)
    {
        Item = item;
    }

    public long Utility()
    {
        long sum = 0;
        int count = Elements.Count;
        for (int i = 0; i < count; i++)
        {
            sum += Elements[i].Nu;
        }
        return sum;
    }

    public long Ru()
    {
        long sum = 0;
        int count = Elements.Count;
        for (int i = 0; i < count; i++)
        {
            sum += Elements[i].Nru;
        }
        return sum;
    }

    public int Support()
    {
        int sum = 0;
        int count = Elements.Count;
        for (int i = 0; i < count; i++)
        {
            sum += Elements[i].Nsup;
        }
        return sum;
    }

    public long Fwub()
    {
        long sum = 0;
        var count = Elements.Count;
        for (var i = 0; i < count; i++)
        {
            var e = Elements[i];
            if (e.Nru > 0)
            {
                sum += e.Nu + e.Nru;
            }
        }
        return sum;
    }

    public long ComputeTwu(Tput tput, Transaction[] txById)
    {
        long sum = 0;
        int count = Elements.Count;
        for (int i = 0; i < count; i++)
        {
            var node = tput.NodesById[Elements[i].Nid];
            var lu = node.Lu;
            int luCount = lu.Count;
            for (int j = 0; j < luCount; j++)
            {
                sum += txById[lu[j].Tid].TU;
            }
        }
        return sum;
    }

    /// <summary>
    /// Builds the 1-itemset MPUN-list for itemJ.
    /// Uses (Tid, ItemUtil, PrefixUtil) stored in each node's Lu.
    ///   Nu  = ∑ Lu[i].ItemUtil
    ///   Nru = ∑ (tx.TU - Lu[i].PrefixUtil)
    ///   Npu = ∑ (Lu[i].PrefixUtil - Lu[i].ItemUtil)  [prefix BEFORE itemJ]
    /// </summary>
    public static MpunList BuildOneItemset(Tput tput, int itemJ, Transaction[] txById)
    {
        var ml = new MpunList(itemJ);
        if (!tput.HeaderTable.TryGetValue(itemJ, out var nodeJ))
        {
            return ml;
        }

        while (nodeJ != null)
        {
            long sumNu = 0;
            long sumNru = 0;
            long sumNpu = 0;

            var lu = nodeJ.Lu;
            var luCount = lu.Count;
            for (var i = 0; i < luCount; i++)
            {
                var (tid, itemUtil, prefixUtil) = lu[i];
                var tx = txById[tid];

                sumNu  += itemUtil;
                sumNru += tx.TU - prefixUtil;      // remaining utility after itemJ
                sumNpu += prefixUtil - itemUtil;   // prefix utility BEFORE itemJ
            }

            ml.Elements.Add(new MpunElement
            {
                Nid   = nodeJ.Nid,
                Nu    = sumNu,
                Nru   = sumNru,
                Npu   = sumNpu,
                Nsup  = luCount
            });

            nodeJ = nodeJ.NextLink;
        }

        ml.Elements.Sort((a, b) => a.Nid.CompareTo(b.Nid));
        return ml;
    }

    /// <summary>
    /// Builds the 2-itemset MPUN-list for {itemJ, itemK} where itemJ ≺twu itemK.
    /// Per Definition 16: for each nD ∈ L_j1 (itemJ nodes, descendant / lower TWU),
    /// if an ancestor nA ∈ L_j2 (itemK nodes, higher TWU) exists on the path,
    /// a new element Ele is created:
    ///   Ele.nid   = nD.Nid
    ///   Ele.nu    = ∑ (tD.ItemUtil + tA.ItemUtil) for matched TIDs (two-pointer, O(N+M))
    ///   Ele.nru   = ∑ (tx.TU - tD.PrefixUtil)     for matched TIDs
    ///   Ele.npu   = ∑ tD.PrefixUtil - tD.ItemUtil  over ALL nD.Lu entries
    ///   Ele.nsup  = number of matched TIDs
    /// </summary>
    public static MpunList BuildTwoItemset(Tput tput, int itemJ, int itemK, Transaction[] txById)
    {
        var ml = new MpunList(itemK);

        // Traverse the header table chain of itemJ (descendant, lower TWU)
        if (!tput.HeaderTable.TryGetValue(itemJ, out var nodeJ))
        {
            return ml;
        }

        while (nodeJ != null)
        {
            // Walk up parent links to find the ancestor node of itemK (higher TWU → closer to root)
            TputNode? ancestorK = null;
            var p = nodeJ.ParentLink;
            while (p != null && p.Item != -1)
            {
                if (p.Item == itemK)
                {
                    ancestorK = p;
                    break;
                }
                p = p.ParentLink;
            }

            if (ancestorK != null)
            {
                // nD = nodeJ (descendant), nA = ancestorK (ancestor)
                // Both Lu lists are sorted by Tid ascending — use two-pointer merge: O(|nD.Lu| + |nA.Lu|)
                var luD = nodeJ.Lu;
                var luA = ancestorK.Lu;
                var cntD = luD.Count;
                var cntA = luA.Count;

                long sumNu   = 0;
                long sumNru  = 0;
                var nsup = luD.Count;

                // npu = ∑ (PrefixUtil - ItemUtil) over ALL nD.Lu entries (prefix BEFORE nD per Def.16)
                long sumNpu = 0;
                for (var i = 0; i < cntD; i++)
                {
                    sumNpu += luD[i].ItemUtil;
                }

                // Two-pointer: match TIDs between nD.Lu and nA.Lu
                int iD = 0, iA = 0;
                while (iD < cntD && iA < cntA)
                {
                    var tidD = luD[iD].Tid;
                    var tidA = luA[iA].Tid;

                    if (tidD == tidA)
                    {
                        var (tid, itemUtilD, _) = luD[iD];
                        var (_, itemUtilA, prefixUtilA)             = luA[iA];

                        sumNu  += itemUtilD + itemUtilA;          // u(itemJ, T) + u(itemK, T)
                        sumNru += prefixUtilA;            // remaining after nD
                        iD++;
                        iA++;
                    }
                    else if (tidD < tidA)
                    {
                        iD++;
                    }
                    else
                    {
                        iA++;
                    }
                }

                if (nsup > 0)
                {
                    ml.Elements.Add(new MpunElement
                    {
                        Nid  = nodeJ.Nid,
                        Nu   = sumNu,
                        Nru  = sumNru,
                        Npu  = sumNpu,
                        Nsup = nsup
                    });
                }
            }

            nodeJ = nodeJ.NextLink;
        }

        // Sort elements by Nid ascending to support binary search during JoinLists
        ml.Elements.Sort((a, b) => a.Nid.CompareTo(b.Nid));
        return ml;
    }

    /// <summary>
    /// Joins two (k-1)-MPUN-lists px (ML1 for P ∪ {x}) and py (ML2 for P ∪ {y}) where x ≺twu y to form the k-MPUN-list for P ∪ {x, y}.
    /// Per Definition 17:
    ///   e.nid  = e1.nid
    ///   e.nu   = e1.nu + e2.nu - e1.npu
    ///   e.nru  = e2.nru
    ///   e.npu  = e1.nu
    ///   e.nsup = e1.nsup
    /// </summary>
    public static MpunList JoinLists(MpunList px, MpunList py)
    {
        var pxy = new MpunList(py.Item);

        int iX = 0;
        int iY = 0;
        int countX = px.Elements.Count;
        int countY = py.Elements.Count;

        // Both lists are sorted by Nid ascending. Use two-pointer merge.
        while (iX < countX && iY < countY)
        {
            var e1 = px.Elements[iX];
            var e2 = py.Elements[iY];

            if (e1.Nid == e2.Nid)
            {
                pxy.Elements.Add(new MpunElement
                {
                    Nid  = e1.Nid,
                    Nu   = e1.Nu + e2.Nu - e1.Npu,
                    Nru  = e2.Nru,
                    Npu  = e1.Nu,
                    Nsup = e1.Nsup
                });
                iX++;
                iY++;
            }
            else if (e1.Nid < e2.Nid)
            {
                iX++;
            }
            else
            {
                iY++;
            }
        }

        return pxy;
    }

}
