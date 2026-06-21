using System;
using System.Collections.Generic;
using Xunit;
using MaxCHUIM.DataStructures;

namespace MaxCHUIM.Tests;

public class BitmaxStructuresTests
{
    [Fact]
    public void ValidityMask_DensityAndInvalidate_Work()
    {
        var vm = new ValidityMask();
        vm.AppendValid();
        vm.AppendValid();
        vm.AppendValid();

        Assert.Equal(1.0, vm.Density());

        // Create a mask with 2nd bit set
        var mask = new BitVector64();
        mask.AppendBit(false);
        mask.AppendBit(true);
        mask.AppendBit(false);

        vm.InvalidateMany(mask);

        // V should be 1 0 1
        Assert.True(vm.Vector.Get(0));
        Assert.False(vm.Vector.Get(1));
        Assert.True(vm.Vector.Get(2));
        Assert.Equal(2.0 / 3.0, vm.Density(), 5);
    }

    [Fact]
    public void MaxBitmax_AppendAndIntersect_Works()
    {
        var maxBm = new MaxBitmax();
        var universe = new List<int> { 1, 2, 3, 4, 5 };

        maxBm.AppendMaxHui(new List<int> { 1, 2 }, universe);
        maxBm.AppendMaxHui(new List<int> { 2, 3, 4 }, universe);
        maxBm.AppendMaxHui(new List<int> { 1, 2, 5 }, universe);

        Assert.Equal(3, maxBm.MaxHuiCount);

        // Intersect {1, 2} -> should be in M0 and M2
        var p = maxBm.Intersect(new List<int> { 1, 2 });
        Assert.True(p.Get(0));
        Assert.False(p.Get(1));
        Assert.True(p.Get(2));
        
        // Intersect {2, 4} -> should be in M1
        var p2 = maxBm.Intersect(new List<int> { 2, 4 });
        Assert.False(p2.Get(0));
        Assert.True(p2.Get(1));
        Assert.False(p2.Get(2));
    }

    [Fact]
    public void BmChui_AppendAndIntersect_Works()
    {
        var bmChui = new BmChui();

        // 0: {1, 2}
        bmChui.Append(new List<int> { 1, 2 });
        // 1: {2, 3, 4}
        bmChui.Append(new List<int> { 2, 3, 4 });
        // 2: {1, 2, 5}
        bmChui.Append(new List<int> { 1, 2, 5 });

        Assert.Equal(3, bmChui.ChuiCount);

        var p = bmChui.Intersect(new List<int> { 1, 2 });
        Assert.True(p.Get(0));
        Assert.False(p.Get(1));
        Assert.True(p.Get(2));

        var p2 = bmChui.Intersect(new List<int> { 3 });
        Assert.False(p2.Get(0));
        Assert.True(p2.Get(1));
        Assert.False(p2.Get(2));
        
        // Ensure padding works when new items are added
        var p3 = bmChui.Intersect(new List<int> { 5 });
        Assert.False(p3.Get(0));
        Assert.False(p3.Get(1));
        Assert.True(p3.Get(2));
    }
}
