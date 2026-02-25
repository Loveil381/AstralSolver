using System;

namespace AstralSolver.Core;

/// <summary>
/// 泛型无锁环形缓冲区，专为主线程单写设计。
/// 满时自动覆盖最旧数据，不扩容，不产生额外堆分配。
/// </summary>
/// <typeparam name="T">存储的元素类型</typeparam>
public sealed class SnapshotRing<T>
{
    private readonly T[] _buffer;
    /// <summary>下一个写入位置的索引（头指针）</summary>
    private int _head;
    /// <summary>当前已存储的元素数量</summary>
    private int _count;

    /// <summary>
    /// 创建指定容量的环形缓冲区
    /// </summary>
    /// <param name="capacity">最大容量（帧数），默认 18000 ≈ 5 分钟 @ 60fps</param>
    /// <exception cref="ArgumentOutOfRangeException">容量必须大于 0</exception>
    public SnapshotRing(int capacity = 18000)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "容量必须大于 0");
        _buffer = new T[capacity];
        _head = 0;
        _count = 0;
    }

    /// <summary>缓冲区的最大容量</summary>
    public int Capacity => _buffer.Length;

    /// <summary>当前已存储的元素数量</summary>
    public int Count => _count;

    /// <summary>缓冲区是否为空</summary>
    public bool IsEmpty => _count == 0;

    /// <summary>缓冲区是否已满（下一次 Push 会覆盖最旧元素）</summary>
    public bool IsFull => _count == _buffer.Length;

    /// <summary>
    /// 写入一个新元素。若缓冲区已满，将覆盖最旧的元素。
    /// </summary>
    /// <param name="item">要写入的元素</param>
    public void Push(T item)
    {
        // ⚡ 性能关键：热路径，无分配，无 LINQ
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length)
            _count++;
    }

    /// <summary>
    /// 返回最新写入的元素。
    /// </summary>
    /// <returns>最新元素</returns>
    /// <exception cref="InvalidOperationException">缓冲区为空时抛出</exception>
    public T GetLatest()
    {
        if (_count == 0)
            throw new InvalidOperationException("环形缓冲区为空，无法获取最新元素");
        // _head 指向下一个写入位置，所以最新的元素在 _head - 1
        int latestIndex = (_head - 1 + _buffer.Length) % _buffer.Length;
        return _buffer[latestIndex];
    }

    /// <summary>
    /// 返回最近 N 个元素的数组（从旧到新顺序排列）。
    /// 若 n 大于当前 Count，则返回所有已有元素。
    /// </summary>
    /// <param name="n">要获取的元素数量</param>
    /// <returns>从旧到新排列的元素数组</returns>
    public T[] GetLastN(int n)
    {
        // ⚡ 性能关键：无 LINQ，预分配精确大小
        if (n <= 0) return Array.Empty<T>();

        int actualCount = n > _count ? _count : n;
        T[] result = new T[actualCount];

        // 计算起始读取位置（从旧到新，起点是最旧元素的索引）
        // 最新元素索引 = (_head - 1 + Length) % Length
        // 最旧的 n 个元素起点 = (latestIndex - actualCount + 1 + Length) % Length
        int latestIndex = (_head - 1 + _buffer.Length) % _buffer.Length;
        int startIndex = (latestIndex - actualCount + 1 + _buffer.Length) % _buffer.Length;

        for (int i = 0; i < actualCount; i++)
        {
            result[i] = _buffer[(startIndex + i) % _buffer.Length];
        }

        return result;
    }

    /// <summary>
    /// 清空缓冲区，重置所有指针和计数器。
    /// </summary>
    public void Clear()
    {
        // 清除所有引用，避免 GC 长期持有旧对象引用
        Array.Clear(_buffer, 0, _buffer.Length);
        _head = 0;
        _count = 0;
    }
}
