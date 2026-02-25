using System;
using Xunit;
using AstralSolver.Core;
using AstralSolver.Utils;

namespace AstralSolver.Tests.Core;

/// <summary>BattleState 数据结构单元测试</summary>
public sealed class BattleStateTests
{
    // ── 辅助：构建测试用 PartyMemberState ─────────────────────────────────────
    private static PartyMemberState MakeMember(uint hp, uint maxHp, string name = "TestMember") =>
        new()
        {
            ObjectId           = 1u,
            Name               = name,
            JobId              = 22,
            HP                 = hp,
            MaxHP              = maxHp,
            PosX               = 0f, PosY = 0f, PosZ = 0f,
            DistanceFromPlayer = 0f,
            Buffs              = [],
        };

    // ── 辅助：构建带 N 个成员的 BattleSnapshot ────────────────────────────────
    private static BattleSnapshot MakeSnapshot(params PartyMemberState[] members)
    {
        var partySlots = new PartyMemberState?[8];
        for (int i = 0; i < members.Length && i < 8; i++)
            partySlots[i] = members[i];
        return new BattleSnapshot
        {
            FrameNumber      = 1,
            Timestamp        = DateTime.UtcNow,
            PartyMembers     = partySlots,
            PartyMemberCount = members.Length,
            Player           = new PlayerState { JobId = 22, HP = 10000, MaxHP = 10000, Buffs = [] },
        };
    }

    // ── 测试1：BattleSnapshot.Empty 的关键字段为默认值 ───────────────────────
    [Fact]
    public void Empty_HasDefaultValues()
    {
        var empty = BattleSnapshot.Empty;

        Assert.Equal(-1, empty.FrameNumber);
        Assert.Equal(DateTime.MinValue, empty.Timestamp);
        Assert.False(empty.IsInCombat);
        Assert.Equal(0, empty.PartyMemberCount);
        Assert.NotNull(empty.PartyMembers);
        Assert.Equal(8, empty.PartyMembers.Length);
        Assert.Null(empty.CurrentTarget);
        Assert.Null(empty.Astrologian);
    }

    // ── 测试2：GetLowestHpPartyMember 在空队伍时返回 null ────────────────────
    [Fact]
    public void GetLowestHpPartyMember_EmptyParty_ReturnsNull()
    {
        var snapshot = MakeSnapshot();
        Assert.Null(snapshot.GetLowestHpPartyMember());
    }

    // ── 测试3：GetLowestHpPartyMember 返回 HP% 最低的成员 ────────────────────
    [Fact]
    public void GetLowestHpPartyMember_ReturnsLowestHpPercent()
    {
        var low  = MakeMember(1000, 10000, "Low");   // 10%
        var high = MakeMember(9000, 10000, "High");  // 90%
        var mid  = MakeMember(5000, 10000, "Mid");   // 50%

        var snapshot = MakeSnapshot(high, mid, low);
        var result = snapshot.GetLowestHpPartyMember();

        Assert.NotNull(result);
        Assert.Equal("Low", result!.Value.Name);
    }

    // ── 测试4：GetPartyMembersSortedByHpPercent 正确由低到高排序 ─────────────
    [Fact]
    public void GetPartyMembersSortedByHpPercent_SortsAscending()
    {
        var a = MakeMember(9000, 10000, "A"); // 90%
        var b = MakeMember(2000, 10000, "B"); // 20%
        var c = MakeMember(5000, 10000, "C"); // 50%

        var snapshot = MakeSnapshot(a, b, c);
        var sorted = snapshot.GetPartyMembersSortedByHpPercent();

        Assert.Equal(3, sorted.Length);
        Assert.Equal("B", sorted[0].Name); // 20%
        Assert.Equal("C", sorted[1].Name); // 50%
        Assert.Equal("A", sorted[2].Name); // 90%
    }

    // ── 测试5：GetPartyMembersBelow(0.5f) 正确计数 ───────────────────────────
    [Fact]
    public void GetPartyMembersBelow_CountsMembersUnderThreshold()
    {
        var a = MakeMember(3000, 10000, "A"); // 30% — 低于 50%
        var b = MakeMember(6000, 10000, "B"); // 60%
        var c = MakeMember(1000, 10000, "C"); // 10% — 低于 50%

        var snapshot = MakeSnapshot(a, b, c);
        int count = snapshot.GetPartyMembersBelow(0.5f);

        Assert.Equal(2, count);
    }

    // ── 测试6：IsAstrologian 在 JobId == 33 时为 true ───────────────────────
    [Fact]
    public void IsAstrologian_WhenJobId33_ReturnsTrue()
    {
        var partySlots = new PartyMemberState?[8];
        var snapshot = new BattleSnapshot
        {
            FrameNumber  = 1,
            Timestamp    = DateTime.UtcNow,
            PartyMembers = partySlots,
            Player       = new PlayerState { JobId = Constants.JobIds.Astrologian, Buffs = [] },
        };

        Assert.True(snapshot.IsAstrologian);
    }
}
