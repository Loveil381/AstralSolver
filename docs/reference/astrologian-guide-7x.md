# 占星术士 Dawntrail 7.x 完整攻略
> 整理日期: 2026-02-23
> 用途: AstrologianModule 决策引擎开发参考

## 一、技能分类

### 1.1 GCD 输出技能
| 技能名 | 技能ID | 类型 | 威力 | 施法时间 | 备注 |
|---|---|---|---|---|---|
| Fall Malefic / 坠星 | 37014 | 单体 | 270 | 1.5s | 92级上位，主要填充技 |
| Combust III / 烧灼III | 3599 | DoT | 55 | 瞬发 | 30s持续 |
| Gravity II / 重力II | 25875 | AOE | 130 | 1.5s | 82级上位，3+目标使用 |

### 1.2 GCD 治疗技能
| 技能名 | 技能ID | 类型 | 威力 | 备注 |
|---|---|---|---|---|
| Benefic / 吉星 | 3594 | 单体 | 500 | 几乎不用 |
| Benefic II / 吉星II | 3610 | 单体 | 800 | 常用GCD单体治疗 |
| Aspected Benefic / 星辉有益 | 3611 | 单体HoT | 250 + 250*5 | 移动补位常用 |
| Helios Conjunction / 星辉天地合相 | 3601 | 团体HoT | 250 + 150*5 | 7.0 阳星相位更名，核心团愈 |
| Helios / 天地合相 | 3600 | 团体 | 400 | 无HoT即时团愈 |
| Macrocosmos / 天巴星象 | 25872 | 攻击+治疗 | 250 | 结算受到伤害的50%作为治疗 |

### 1.3 oGCD 输出技能
| 技能名 | 技能ID | 威力 | 类型 | 备注 |
|---|---|---|---|---|
| Lord of Crowns / 领主之冠 | 7444 | 400 | AoE | 抽到日属性组获得 |
| Earthly Star / 天地地星 | 7439 | 310 | AoE | 巨星爆发威力 |
| Oracle / 对岁 | 37029 | 860 | AoE | 占卜期间触发的高端输出 |

### 1.4 oGCD 治疗技能
| 技能名 | 优先级 | 类型 | 效果 | 备注 |
|---|---|---|---|---|
| Essential Dignity / 加护尹州 | 1 | 单体回复 | 400-900 | 7.1改为3充能，低血量奇效 |
| Celestial Intersection / 天宫交嵌 | 2 | 单体盾+奶 | 200奶+400盾 | 2充能，卡CD给坦 |
| Exaltation / 崇高 | 3 | 单体减伤 | 10%减+500回 | 预判大伤害 |
| Celestial Opposition / 天宫对岁 | 4 | 团体HoT | 200奶+200HoT | 常用oGCD团愈 |
| Horoscope / 天宫星象 | 5 | 团体延迟 | 200/400 | 配合GCD团愈增强 |
| Collective Unconscious / 天地神明 | 6 | 团减/HoT | 10%减/100HoT | 120s CD |
| Lady of Crowns / 贵妇之冠 | - | 团体回复 | 400 | 抽到月属性组获得 |
| Sun Sign / 太阳签名 | - | 团减 | 10% | 中和派生技能 |

### 1.5 卡牌系统 (Card System 7.x)
Dawntrail 7.0 废除了随机抽牌和印记系统，改为轮流获得固定卡组。

- **Astral Draw (星极抽卡)**:
  - **Play I**: The Balance (天良) - 近战/坦克 +6% 伤害。
  - **Play II**: The Bole (钿木) - 单体 10% 减伤。
  - **Play III**: The Arrow (射手) - 单体治疗受量提升 10%。
  - **Minor Arcana**: Lord of Crowns (领主) - 400威力AoE攻击。

- **Umbral Draw (灵极抽卡)**:
  - **Play I**: The Spear (镇魂) - 远程/咏唱 +6% 伤害。
  - **Play II**: The Ewer (山泽) - 单体 HP 持续回复 (HoT)。
  - **Play III**: The Spire (高塔) - 单体护盾。
  - **Minor Arcana**: Lady of Crowns (贵妇) - 400威力AoE治疗。

### 1.6 团辅技能
- **Divination (占卜)**: 120s CD，全队+6%伤害，持续20秒。
- **Lightspeed (光速)**: 120s CD，两层充能。爆发期双插卡牌的核心。

## 二、开幕循环 (Opener)

### 2.1 7.0 标准开幕
- `-4.0s`: 放置 Earthly Star (地星)
- `-1.5s`: 预读 Fall Malefic (坠星)
- `0.0s`: Combust III (烧灼) + 爆发药
- `+1 GCD`: Fall Malefic + Lightspeed (光速)
- `+2 GCD`: Fall Malefic + **Divination (占卜)** + **Play I (Balance, 近战DPS)**
- `+3 GCD`: Fall Malefic + **Minor Arcana (Lord)** + **Oracle (对岁)**
- `+4 GCD`: Fall Malefic + **Umbral Draw (切月组)** + **Play I (Spear, 远程DPS)**
- `+5 GCD`: Fall Malefic (正常填充)

## 三、循环优先级（战斗中稳态）

### 3.1 GCD 优先级
1. **DoT 维持**: 刷新 Combust III (剩余 < 3s)。
2. **爆发**: 占卜期内打出 Oracle。
3. **填充**: Fall Malefic。
4. **多目标**: 3+目标使用 Gravity II。

### 3.2 oGCD 穿插优先级
1. **Draw**: 冷却好了立即执行，确保卡牌不浪费。
2. **Play I (攻击卡)**: 尽可能在占卜/大爆发期给到对应DPS。
3. **Play II/III (功能卡)**: 按需给坦克或危险队友，不要空转太久。
4. **Oracle / Minor Arcana**: 输出向 oGCD 严格对齐 120s。

### 3.3 治疗决策树
1. **oGCD 优先**: 尽量不丢 GCD 治疗。
   - `Essential Dignity` 应对单体掉血。
   - `Celestial Opposition` 应对中量全团掉血。
   - `Earthly Star` 预设应对巨额掉血。
2. **GCD 补位**:
   - `Helios Conjunction` 用于持续 AOE 压力。
   - `Macrocosmos` 用于大穿透或生命值强制降低机制。

### 3.4 发牌目标选择算法
- **近战卡 (Balance)**: 蝰蛇 > 龙骑 > 武士 > 武僧 > 忍者 > 坦克。
- **远程卡 (Spear)**: 黑魔 > 召唤 > 赤魔 > 机工 > 舞者 > 诗人 > 治疗。
- *如果目标已有增益，则顺延至下一位。*

## 四、特殊场景处理
- **移动**: 占星拥有两层 Lightspeed 和大把瞬发卡牌，移动能力极强。
- **高压**: Bole 和 Spire 的精准投送是 7.0 占星的新亮点。

## 五、关键数值
- **GCD 基础**: 2.50s。
- **Draw CD**: 55s（注：7.0 改为每 55s 获得一整套 4 张卡）。
- **Divination CD**: 120s。
- **Oracle 威力**: 860。
