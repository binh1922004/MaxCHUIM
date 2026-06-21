using System;
using System.Collections.Generic;

namespace MaxCHUIM.Algorithms;

public class AlgorithmResult
{
    public List<ChuiEntry> CHUIs { get; set; } = new();
    public List<MaxHuiEntry> MaxHUIs { get; set; } = new();
    public TimeSpan Runtime { get; set; }
    public long CandidatesCount { get; set; }
}
