namespace MaxCHUIM.DataStructures;

public struct MpunElement
{
    public int Nid;     // node id in TPUT
    public long Nu;     // utility of the itemset in the transactions of the node
    public long Nru;    // remaining utility of the itemset in the transactions of the node
    public long Npu;    // prefix utility of the itemset in the transactions of the node
    public int Nsup;    // support (number of transactions of the node containing the itemset)
}
