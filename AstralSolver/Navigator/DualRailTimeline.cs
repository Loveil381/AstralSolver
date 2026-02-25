using System;
using AstralSolver.Core;

namespace AstralSolver.Navigator;

/// <summary>
/// 图标位置信息
/// </summary>
public readonly record struct IconPosition(float X, float Y, float Size, uint ActionId, bool IsHighlighted);

/// <summary>
/// 布局边界框
/// </summary>
public record struct BoundingBox(float X, float Y, float Width, float Height);

/// <summary>
/// 时间轴布局数据
/// </summary>
public sealed record class TimelineLayout
{
    public required IconPosition[] GcdPositions { get; init; }
    public required IconPosition[] OgcdPositions { get; init; }
    public required BoundingBox Bounds { get; init; }
}

/// <summary>
/// 双轨时间轴布局计算器：将 GCD 队列和 oGCD 插入点映射到屏幕坐标
/// </summary>
public class DualRailTimeline
{
    /// <summary>
    /// 根据传入的决策包和起点坐标，计算 GCD 和 oGCD 序列的屏幕布局
    /// </summary>
    public TimelineLayout Calculate(DecisionPacket packet, float startX, float startY, float iconSize)
    {
        if (packet == null || packet.GcdQueue == null || packet.GcdQueue.Length == 0)
        {
            return new TimelineLayout
            {
                GcdPositions = Array.Empty<IconPosition>(),
                OgcdPositions = Array.Empty<IconPosition>(),
                Bounds = new BoundingBox(startX, startY, 0, 0)
            };
        }

        int maxGcd = Math.Min(5, packet.GcdQueue.Length);
        var gcdPositions = new IconPosition[maxGcd];
        
        // 常数
        float spacing = 8f; 
        float railSpacing = 12f; 
        float smallIconSize = 36f; // ICON_SIZE_SMALL 附近
        
        float currentX = startX;
        float gcdY = startY;
        float ogcdY = startY + iconSize + railSpacing; // oGCD 轨迹在 GCD 下方
        
        float maxWidth = 0;

        // 计算 GCD 轨迹
        for (int i = 0; i < maxGcd; i++)
        {
            bool isHighlighted = i == 0; 
            float actualSize = isHighlighted ? iconSize * 1.1f : iconSize;
            
            gcdPositions[i] = new IconPosition(currentX, gcdY, actualSize, packet.GcdQueue[i].ActionId, isHighlighted);
            currentX += actualSize + spacing;
        }
        maxWidth = currentX - startX;

        // 计算 oGCD 轨迹
        int ogcdInsertsCount = packet.OgcdInserts?.Length ?? 0;
        var ogcdPositions = new IconPosition[ogcdInsertsCount];
        int ogcdCount = 0;

        if (packet.OgcdInserts != null)
        {
            foreach (var ogcd in packet.OgcdInserts)
            {
                int index = ogcd.InsertAfterGcdIndex;
                float oX = startX;
                
                if (index < maxGcd && index >= 0)
                {
                     // oGCD坐标定位在对应GCD的末尾到下一个GCD之间
                     var targetGcd = gcdPositions[index];
                     oX = targetGcd.X + targetGcd.Size + (spacing / 2f) - (smallIconSize / 2f);
                }
                else
                {
                     oX = currentX + spacing; // 超出当前显示的GCD列表，放最后面
                }
                
                // 处理同坑位多个oGCD重叠情况
                int countInSameSlot = 0;
                for (int j = 0; j < ogcdCount; j++)
                {
                    if (packet.OgcdInserts[j].InsertAfterGcdIndex == index) countInSameSlot++;
                }
                oX += countInSameSlot * (smallIconSize + 2f);

                ogcdPositions[ogcdCount] = new IconPosition(oX, ogcdY, smallIconSize, ogcd.ActionId, false);
                ogcdCount++;
                
                float reqWidth = (oX + smallIconSize) - startX;
                if (reqWidth > maxWidth) maxWidth = reqWidth;
            }
        }

        return new TimelineLayout
        {
            GcdPositions = gcdPositions,
            OgcdPositions = ogcdPositions,
            Bounds = new BoundingBox(startX, startY, maxWidth, iconSize + railSpacing + smallIconSize)
        };
    }
}
