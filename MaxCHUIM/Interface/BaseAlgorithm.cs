using MaxCHUIM.Algorithms;
using MaxCHUIM.Models;

namespace MaxCHUIM.Interface;

public interface BaseAlgorithm
{
    AlgorithmResult Run(QuantitativeDatabase db, long mu, AlgorithmMode mode);
}