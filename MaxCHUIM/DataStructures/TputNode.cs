using System.Collections.Generic;

namespace MaxCHUIM.DataStructures;

public class TputNode
{
    public int Item { get; set; }
    public int Nid { get; set; }
    /// <summary>
    /// Each entry: (Tid, ItemUtil, PrefixUtil)
    ///   ItemUtil   = u(this.Item, T_tid)             — individual utility of this item in that transaction
    ///   PrefixUtil = sum of u(item, T_tid) for all items on path root→this node (inclusive)
    /// TIDs are stored in ascending insertion order; since transactions are sorted before insertion the TIDs
    /// are not necessarily monotone, but we will sort Lu by TID after building for two-pointer merges.
    /// </summary>
    public List<(int Tid, long ItemUtil, long PrefixUtil)> Lu { get; set; } = new();
    public long ItemUtilitySum { get; set; }
    public TputNode? ParentLink { get; set; }
    public TputNode? NextLink { get; set; }
    public Dictionary<int, TputNode> Children { get; set; } = new();

    public TputNode()
    {
    }

    public TputNode(int item, int nid, TputNode? parentLink)
    {
        Item = item;
        Nid = nid;
        ParentLink = parentLink;
    }
}
