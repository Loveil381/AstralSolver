using System.Collections.Generic;

namespace AstralSolver.Utils;

/// <summary>
/// 存放全局共享常量，避免使用魔法数字
/// </summary>
public static class Constants
{
    // === 性能要求常量 ===
    public const float MaxDecisionTimeMs = 5.0f; // 核心引擎单帧计算限制不超过 5ms

    // === UI 常量 ===
    public const uint CardPanelWidth = 300;
    public const uint TimelineHeight = 120;
    public const string MainWindowId = "AstralSolver_MainWindow";

    // === 职业 ID 集合定义 ===
    /// <summary>职业 ID 常量</summary>
    public static class JobIds
    {
        /// <summary>占星术士职业 ID</summary>
        public const byte Astrologian = 33;
    }

    /// <summary>技能 ID 常量（用于冷却/GCD 速度查询）</summary>
    public static class ActionIds
    {
        /// <summary>代理 GCD 测速技能 ID（凶星 I），用于读取当前 GCD 总时间</summary>
        public const uint CommonGcd = 3596;
    }

    public const uint JobAstrologian = 33; // 保留向后兼容
    
    // 近战职业IDs (Play I 最佳目标)
    public static readonly IReadOnlySet<uint> MeleeJobs = new HashSet<uint>
    {
        1,  // Gladiator
        2,  // Pugilist
        3,  // Marauder
        4,  // Lancer
        19, // Paladin
        20, // Monk
        21, // Warrior
        22, // Dragoon
        30, // Ninja
        32, // Dark Knight
        34, // Samurai
        37, // Gunbreaker
        39, // Reaper
        41  // Viper
    };

    // 远程/法系职业IDs (Play II 最佳目标)
    public static readonly IReadOnlySet<uint> RangedJobs = new HashSet<uint>
    {
        5,  // Archer
        23, // Bard
        31, // Machinist
        38, // Dancer
        26, // Arcanist
        27, // Summoner
        28, // Scholar
        25, // Black Mage
        35, // Red Mage
        42  // Pictomancer
    };

    // === 占星术士技能与状态 ID（7.x Dawntrail 验证）===
    /// <summary>占星术士技能 ID</summary>
    public static class AstActionIds
    {
        // ―― 输出技能 ――
        /// <summary>凶星 IV（Malefic IV）— 当前GCD主输出，同时用于GCD测速</summary>
        public const uint Malefic = 3596;
        /// <summary>燃烬 III（Combust III）— DoT</summary>
        public const uint Combust = 3599;
        /// <summary>占卜（Divination）— 团辅大技能，CD=120s，持续20s</summary>
        public const uint Divination = 16552;
        /// <summary>对岁（Oracle）— 占卜后续 oGCD，600P AoE</summary>
        public const uint Oracle = 37029;

        // ―― 牌系统（7.x Dawntrail 重做，印记/Astrodyne 已删除）――
        /// <summary>星极吸引（Astral Draw）— 抽 4 张星极属性牌，CD=55s</summary>
        public const uint AstralDraw = 37017;
        /// <summary>灵极吸引（Umbral Draw）— 抽 4 张灵极属性牌，与 AstralDraw 共享 CD</summary>
        public const uint UmbralDraw = 37018;
        /// <summary>Play I — 出攻击牌（Balance/Spear）</summary>
        public const uint PlayI = 37019;
        /// <summary>Play II — 出防御牌（Arrow/Bole）</summary>
        public const uint PlayII = 37020;
        /// <summary>Play III — 出回复牌（Ewer/Spire）</summary>
        public const uint PlayIII = 37021;
        /// <summary>Minor Arcana — 出小奥秘卡（Lord/Lady of Crowns），CD≈1s</summary>
        public const uint MinorArcana = 7443;

        // ―― 单体治疗技能 ――
        /// <summary>有益（Benefic）</summary>
        public const uint Benefic = 3594;
        /// <summary>优质有益（Benefic II）</summary>
        public const uint BeneficII = 3610;
        /// <summary>星辉有益（Aspected Benefic）— 附加 HoT</summary>
        public const uint AspectedBenefic = 3611;
        /// <summary>加护尹州（Essential Dignity）— 3 充能，血量越低治疗量越高</summary>
        public const uint EssentialDignity = 3614;
        /// <summary>天宫交嵌（Celestial Intersection）— 单目标盾 + HoT</summary>
        public const uint CelestialIntersection = 16556;
        /// <summary>崇高（Exaltation）— 单目标 10% 盾 + 后续治疗</summary>
        public const uint Exaltation = 25873;

        // ―― 全团治疗技能 ――
        /// <summary>天地神明（Collective Unconscious）— 全团盾 + HoT</summary>
        public const uint CollectiveUnconscious = 3613;
        /// <summary>天星地星（Earthly Star）— 放置式治疗/伤害</summary>
        public const uint EarthlyStar = 7439;
        /// <summary>天宫对岁（Celestial Opposition）— 全团治疗 + HoT</summary>
        public const uint CelestialOpposition = 16553;
        /// <summary>天宫星象（Horoscope）— 延迟触发全团治疗</summary>
        public const uint Horoscope = 16557;
        /// <summary>小宇宙（Microcosmos）— 全团盾和治疗</summary>
        public const uint Microcosmos = 25874;
        /// <summary>天巴星象（Macrocosmos）— 将伤害转换为治疗储存</summary>
        public const uint Macrocosmos = 25872;
        /// <summary>天地合相（Helios）</summary>
        public const uint Helios = 3600;
        /// <summary>星辉天地合相（Aspected Helios / Helios Conjunction）</summary>
        public const uint AspectedHelios = 3601;
    }

    /// <summary>占星术士 Buff/Debuff 状态 ID</summary>
    public static class AstStatusIds
    {
        // ―― Debuff ――
        /// <summary>燃烬 III DoT 状态 ID</summary>
        public const uint Combust = 838;

        // ―― 团辅 Buff ――
        /// <summary>占卜（Divination）Buff 状态 ID</summary>
        public const uint Divination = 1878;
        /// <summary>对岁（Oracle）就绪状态 ID</summary>
        public const uint OracleReady = 3840;

        // ―― 卡牌 Buff（7.x 新卡系）――
        /// <summary>天良（The Balance）Buff ID — 近战/坦 +6% 伤害</summary>
        public const uint Balance = 3887;
        /// <summary>钿木（The Bole）Buff ID — 减伤 10%</summary>
        public const uint Bole = 3890;
        /// <summary>射手（The Arrow）Buff ID — 回复量 +10%</summary>
        public const uint Arrow = 3889;
        /// <summary>镇魂（The Spear）Buff ID — 远战/奥运 +6% 伤害</summary>
        public const uint Spear = 3888;
        /// <summary>山泽（The Ewer）HoT Buff ID</summary>
        public const uint Ewer = 3891;
        /// <summary>高塔（The Spire）盾 Buff ID</summary>
        public const uint Spire = 3892;
    }
}
