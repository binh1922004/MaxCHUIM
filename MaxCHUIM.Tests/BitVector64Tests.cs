using System;
using Xunit;
using MaxCHUIM.DataStructures;

namespace MaxCHUIM.Tests;

public class BitVector64Tests
{
    [Fact]
    public void AppendBit_AndGet_WorksCorrectly()
    {
        var bv = new BitVector64();
        bv.AppendBit(true);
        bv.AppendBit(false);
        bv.AppendBit(true);

        Assert.Equal(3, bv.BitLength);
        Assert.True(bv.Get(0));
        Assert.False(bv.Get(1));
        Assert.True(bv.Get(2));
        Assert.False(bv.Get(3)); // Out of bounds should be false
    }

    [Fact]
    public void Set_UpdatesBitCorrectly()
    {
        var bv = new BitVector64();
        bv.AppendBit(false);
        bv.AppendBit(false);

        Assert.False(bv.Get(1));
        bv.Set(1, true);
        Assert.True(bv.Get(1));
        bv.Set(1, false);
        Assert.False(bv.Get(1));
    }

    [Fact]
    public void IsZero_ReturnsTrueWhenAllZeros()
    {
        var bv = new BitVector64();
        bv.AppendBit(false);
        bv.AppendBit(false);
        Assert.True(bv.IsZero());

        bv.AppendBit(true);
        Assert.False(bv.IsZero());
    }

    [Fact]
    public void PopCount_ReturnsCorrectNumberOfSetBits()
    {
        var bv = new BitVector64();
        for(int i=0; i<100; i++)
        {
            bv.AppendBit(i % 3 == 0); // set every 3rd bit
        }
        
        // i=0, 3, 6, 9, ..., 99 -> 34 bits set
        Assert.Equal(34, bv.PopCount());
    }

    [Fact]
    public void AndInPlace_ComputesCorrectIntersection()
    {
        var bv1 = new BitVector64();
        var bv2 = new BitVector64();

        // bv1: 1 0 1 1
        bv1.AppendBit(true); bv1.AppendBit(false); bv1.AppendBit(true); bv1.AppendBit(true);
        
        // bv2: 1 1 0 1
        bv2.AppendBit(true); bv2.AppendBit(true); bv2.AppendBit(false); bv2.AppendBit(true);

        bv1.AndInPlace(bv2);

        // Expected: 1 0 0 1
        Assert.True(bv1.Get(0));
        Assert.False(bv1.Get(1));
        Assert.False(bv1.Get(2));
        Assert.True(bv1.Get(3));
        Assert.Equal(2, bv1.PopCount());
    }

    [Fact]
    public void OrInPlace_ComputesCorrectUnion()
    {
        var bv1 = new BitVector64();
        var bv2 = new BitVector64();

        // bv1: 1 0 0
        bv1.AppendBit(true); bv1.AppendBit(false); bv1.AppendBit(false);
        
        // bv2: 0 1 0 1
        bv2.AppendBit(false); bv2.AppendBit(true); bv2.AppendBit(false); bv2.AppendBit(true);

        bv1.OrInPlace(bv2);

        // Expected: 1 1 0 1
        Assert.True(bv1.Get(0));
        Assert.True(bv1.Get(1));
        Assert.False(bv1.Get(2));
        Assert.True(bv1.Get(3));
        Assert.Equal(4, bv1.BitLength);
        Assert.Equal(3, bv1.PopCount());
    }

    [Fact]
    public void NotInPlace_InvertsBitsCorrectly()
    {
        var bv = new BitVector64();
        bv.AppendBit(true);
        bv.AppendBit(false);
        bv.AppendBit(false);

        bv.NotInPlace();

        Assert.False(bv.Get(0));
        Assert.True(bv.Get(1));
        Assert.True(bv.Get(2));
        // Ensure out of bounds are not erroneously set
        Assert.False(bv.Get(3));
    }

    [Fact]
    public void AndIsNonZero_ShortCircuitsCorrectly()
    {
        var bv1 = new BitVector64();
        var bv2 = new BitVector64();

        // bv1: 0 0 1
        bv1.AppendBit(false); bv1.AppendBit(false); bv1.AppendBit(true);
        // bv2: 0 1 0
        bv2.AppendBit(false); bv2.AppendBit(true); bv2.AppendBit(false);

        Assert.False(BitVector64.AndIsNonZero(bv1, bv2));

        bv1.Set(1, true); // bv1: 0 1 1
        Assert.True(BitVector64.AndIsNonZero(bv1, bv2));
    }
}
