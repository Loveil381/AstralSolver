using System;
using Xunit;
using AstralSolver.Core;

namespace AstralSolver.Tests.Core;

/// <summary>SnapshotRing 环形缓冲区单元测试</summary>
public sealed class SnapshotRingTests
{
    // ── 测试1：Push 单个元素后 GetLatest 返回该元素 ──────────────────────────
    [Fact]
    public void Push_SingleItem_GetLatestReturnsIt()
    {
        var ring = new SnapshotRing<int>(5);
        ring.Push(42);
        Assert.Equal(42, ring.GetLatest());
    }

    // ── 测试2：Push 多个元素后 GetLatest 返回最后一个 ─────────────────────────
    [Fact]
    public void Push_MultipleItems_GetLatestReturnsLast()
    {
        var ring = new SnapshotRing<int>(5);
        ring.Push(1);
        ring.Push(2);
        ring.Push(3);
        Assert.Equal(3, ring.GetLatest());
    }

    // ── 测试3：GetLastN(3) 在有 5 个元素时返回最后 3 个（旧→新顺序）────────────
    [Fact]
    public void GetLastN_WithMoreItems_ReturnsLastN_OldToNew()
    {
        var ring = new SnapshotRing<int>(10);
        for (int i = 1; i <= 5; i++) ring.Push(i);

        int[] result = ring.GetLastN(3);

        Assert.Equal(3, result.Length);
        Assert.Equal(3, result[0]); // 旧
        Assert.Equal(4, result[1]);
        Assert.Equal(5, result[2]); // 新
    }

    // ── 测试4：GetLastN(10) 在只有 3 个元素时返回全部 3 个 ────────────────────
    [Fact]
    public void GetLastN_MoreThanCount_ReturnsAll()
    {
        var ring = new SnapshotRing<int>(10);
        ring.Push(10);
        ring.Push(20);
        ring.Push(30);

        int[] result = ring.GetLastN(10);

        Assert.Equal(3, result.Length);
        Assert.Equal(10, result[0]);
        Assert.Equal(30, result[2]);
    }

    // ── 测试5：容量为 3，Push 4 个后 Count 仍为 3，且最旧被覆盖 ───────────────
    [Fact]
    public void Push_OverCapacity_WrapsAndOldestOverwritten()
    {
        var ring = new SnapshotRing<int>(3);
        ring.Push(1);
        ring.Push(2);
        ring.Push(3);
        ring.Push(4); // 覆盖 1

        Assert.Equal(3, ring.Count);
        Assert.True(ring.IsFull);

        int[] result = ring.GetLastN(3);
        // 预期 [2, 3, 4]（从旧到新）
        Assert.Equal(2, result[0]);
        Assert.Equal(3, result[1]);
        Assert.Equal(4, result[2]);
    }

    // ── 测试6：Clear 后 IsEmpty 为 true，Count 为 0 ──────────────────────────
    [Fact]
    public void Clear_ResetsToEmpty()
    {
        var ring = new SnapshotRing<int>(5);
        ring.Push(1);
        ring.Push(2);
        ring.Clear();

        Assert.True(ring.IsEmpty);
        Assert.Equal(0, ring.Count);
    }

    // ── 测试7：空缓冲区 GetLatest 抛出 InvalidOperationException ─────────────
    [Fact]
    public void GetLatest_EmptyRing_ThrowsInvalidOperationException()
    {
        var ring = new SnapshotRing<int>(5);
        Assert.Throws<InvalidOperationException>(() => ring.GetLatest());
    }

    // ── 测试8：GetLastN(0) 返回空数组 ────────────────────────────────────────
    [Fact]
    public void GetLastN_Zero_ReturnsEmptyArray()
    {
        var ring = new SnapshotRing<int>(5);
        ring.Push(1);
        ring.Push(2);

        int[] result = ring.GetLastN(0);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
