using MaxCHUIM.Models;
using MaxCHUIM.DataStructures;
using MaxCHUIM.Algorithms;

namespace MaxCHUIM.Pruning;

public static class PruningStrategies
{
    /// <summary>
    /// SPWUB Pruning (Theorem 1): Prune a branch if the forward weak utility bound is less than mu.
    /// </summary>
    public static bool SpwubPrune(MpunList ml, long mu)
    {
        return ml.Fwub() < mu;
    }
}
