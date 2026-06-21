using MaxCHUIM.Algorithms;
using MaxCHUIM.Models;

namespace MaxCHUIM.Interface;

public interface ChuiStore
{
    void Add(Itemset itemset, int support, long utility, long twu);
    bool CheckBackward(Itemset B, int suppB, long twuB);
    public List<ChuiEntry> GetAllEntries();
}

public interface MaxHuiStore
{
    void UpdateMHUI(Itemset A, long utility);
    public List<MaxHuiEntry> GetAllEntries();
}