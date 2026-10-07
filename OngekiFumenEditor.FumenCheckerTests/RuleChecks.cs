namespace OngekiFumenEditor.FumenCheckerTests;

/// <summary>
/// 用例谱面（内联 ogkr 文本，全部走真实解析器）与断言。
/// golden 基线谱面是一张干净谱面：它必须零检查结果，用来防止任何规则误报。
/// </summary>
internal static class RuleChecks
{
    /// <summary>本次新增的 9 条规则名（8 条「游戏侧会崩溃」+ 1 条「默认变速组最后变速非正速」警告；样例谱面 sweep 用它判定误报）。</summary>
    public static readonly string[] NewRuleNames =
    {
        "BulletPalleteDuplicateId",
        "SoflanPatternMissingForArea",
        "OrphanLaneRecord",
        "ColorfulLaneRecordColumns",
        "BeamRecordColumns",
        "HoldProgressJudgeLoop",
        "BpmOutOfRange",
        "MissingEnemySetWave",
        "DefaultSoflanLastSpeedNonPositive",
    };

    /// <summary>本次新增规则的严重度：除「默认变速组最后变速非正速」是警告档（problem）外，其余都从游戏崩溃路径移植（error）。</summary>
    public static string NewRuleSeverity(string ruleName) => ruleName == "DefaultSoflanLastSpeedNonPositive" ? "Problem" : "Error";

    /// <summary>
    /// 样例谱面里已知的、经人工核对为真实的命中（不是误报），sweep 断言只放行这些组合。
    /// 三张谱面都只写了半个波次序列：20997_03 只有 WAVE1、23807_03 与 24003_04 只有 BOSS，
    /// 按游戏代码会丢弃整组 EnemySet 并改用默认波次布局。
    /// 22863_04 通篇用 0x 停止做节奏 gimmick，默认变速组的最后一条变速（T[65,960]，120 格）
    /// 也是 0x 且之后没有恢复正速的记录（其后仍有音符，最大到 unit 91）→ 命中为真。
    /// </summary>
    public static readonly (string Chart, string RuleName)[] KnownSampleFindings =
    {
        ("20997_03.ogkr", "MissingEnemySetWave"),
        ("23807_03.ogkr", "MissingEnemySetWave"),
        ("24003_04.ogkr", "MissingEnemySetWave"),
        ("22863_04.ogkr", "DefaultSoflanLastSpeedNonPositive"),
    };

    private const string BplA0 = "BPL\tA0\tUPS\t0\tFIX\t1.000000\tN\tCIR\t0";
    private const string ProgJudgeDefault = "PROGJUDGE_BPM\t240.000";
    private const string EstWave1 = "EST\t0\t0\tWAVE1";
    private const string EstBoss = "EST\t8\t0\tBOSS";

    public static IEnumerable<CheckCase> Cases { get; } = BuildCases();

    private static IEnumerable<CheckCase> BuildCases()
    {
        // ---------------- golden 基线 ----------------
        yield return new CheckCase
        {
            Name = "clean-baseline",
            Chart = new ChartBuilder().Build(),
            ExpectNoFindings = true,
        };

        // ---------------- 1. BPL 弹种 ID 重复 ----------------
        yield return new CheckCase
        {
            Name = "bpl-duplicate-id",
            Chart = new ChartBuilder()
                .Add("BPL\tA0\tCEN\t0\tFIX\t1.000000\tN\tCIR\t0")
                .Build(),
            Expected = new[] { new Expectation("BulletPalleteDuplicateId", 1) },
        };
        yield return new CheckCase
        {
            Name = "bpl-distinct-ids",
            Chart = new ChartBuilder()
                .Add("BPL\tB0\tCEN\t0\tFIX\t1.000000\tN\tCIR\t0")
                .Build(),
            Forbidden = new[] { "BulletPalleteDuplicateId" },
        };

        // ---------------- 2. ISF 引用了不存在的 soflan 组且有长条在区域内 ----------------
        yield return new CheckCase
        {
            Name = "soflan-pattern-missing-for-area",
            Chart = new ChartBuilder()
                .Add("ISF\t1\t960\t-12\t480\t24\t7")
                .Build(),
            Expected = new[] { new Expectation("SoflanPatternMissingForArea", 1) },
        };
        yield return new CheckCase
        {
            Name = "soflan-pattern-present-for-area",
            Chart = new ChartBuilder()
                .Add("ISF\t1\t960\t-12\t480\t24\t7", "SFL\t1\t960\t480\t0.700000\t7")
                .Build(),
            Forbidden = new[] { "SoflanPatternMissingForArea" },
        };

        // ---------------- 3. 轨道延伸段找不到起点 ----------------
        yield return new CheckCase
        {
            Name = "orphan-lane-segments",
            Chart = new ChartBuilder()
                .Add("LLN\t9\t1\t0\t-24", "WLN\t8\t1\t0\t-24")
                .Build(),
            Expected = new[] { new Expectation("OrphanLaneRecord", 2) },
        };

        // ---------------- 4. 长条判定点步长归零（PROGJUDGE_BPM ↔ BPM 比值边界） ----------------
        yield return new CheckCase
        {
            Name = "progjudge-safe-boundary",
            Chart = new ChartBuilder()
                .Replace(ProgJudgeDefault, "PROGJUDGE_BPM\t30000.000")
                .Build(),
            Forbidden = new[] { "HoldProgressJudgeLoop" },
        };
        yield return new CheckCase
        {
            Name = "progjudge-freeze",
            Chart = new ChartBuilder()
                .Replace(ProgJudgeDefault, "PROGJUDGE_BPM\t40000.000")
                .Build(),
            Expected = new[] { new Expectation("HoldProgressJudgeLoop", 1) },
        };

        // ---------------- 5. BPM 取值越界 ----------------
        yield return new CheckCase
        {
            Name = "bpm-out-of-range",
            Chart = new ChartBuilder()
                .Add("BPM\t1\t0\t0.000")
                .Build(),
            Expected = new[] { new Expectation("BpmOutOfRange", 1) },
        };
        yield return new CheckCase
        {
            Name = "bpm-normal",
            Chart = new ChartBuilder()
                .Add("BPM\t1\t0\t200.000")
                .Build(),
            Forbidden = new[] { "BpmOutOfRange" },
        };

        // ---------------- 6. 色带轨道记录列数 ----------------
        yield return new CheckCase
        {
            Name = "colorful-lane-short-record",
            Chart = new ChartBuilder()
                .Add("CLS\t2\t0\t0\t-12")
                .Build(),
            Expected = new[] { new Expectation("ColorfulLaneRecordColumns", 1) },
        };
        yield return new CheckCase
        {
            Name = "colorful-lane-full-record",
            Chart = new ChartBuilder()
                .Add("CLS\t2\t0\t0\t-12\t0\t3\t0")
                .Build(),
            Forbidden = new[] { "ColorfulLaneRecordColumns" },
        };

        // ---------------- 7. 光束记录列数（普通 / 斜向） ----------------
        yield return new CheckCase
        {
            Name = "oblique-beam-short-record",
            Chart = new ChartBuilder()
                .Add("OBS\t3\t0\t0\t-12\t1")
                .Build(),
            Expected = new[] { new Expectation("BeamRecordColumns", 1) },
        };
        yield return new CheckCase
        {
            Name = "beam-short-record",
            Chart = new ChartBuilder()
                .Add("BMS\t4\t0\t0\t-12")
                .Build(),
            Expected = new[] { new Expectation("BeamRecordColumns", 1) },
        };
        yield return new CheckCase
        {
            Name = "beam-full-records",
            Chart = new ChartBuilder()
                .Add("OBS\t3\t0\t0\t-12\t1\t2", "BMS\t4\t0\t0\t-12\t1")
                .Build(),
            Forbidden = new[] { "BeamRecordColumns" },
        };

        // ---------------- 8. EnemySet 缺 WAVE1 / BOSS ----------------
        yield return new CheckCase
        {
            Name = "enemyset-missing-wave1",
            Chart = new ChartBuilder()
                .Replace(EstWave1, "EST\t0\t0\tWAVE2")
                .Build(),
            Expected = new[] { new Expectation("MissingEnemySetWave", 1) },
        };
        yield return new CheckCase
        {
            Name = "enemyset-missing-boss",
            Chart = new ChartBuilder()
                .Remove(EstBoss)
                .Build(),
            Expected = new[] { new Expectation("MissingEnemySetWave", 1) },
        };
        yield return new CheckCase
        {
            Name = "enemyset-complete",
            Chart = new ChartBuilder().Build(),
            Forbidden = new[] { "MissingEnemySetWave" },
        };

        // ---------------- 默认变速组最后一条变速非正速（警告档） ----------------
        yield return new CheckCase
        {
            Name = "default-soflan-last-speed-non-positive",
            Chart = new ChartBuilder()
                .Add("SFL\t1\t960\t240\t0.000000\t0")
                .Build(),
            Expected = new[] { new Expectation("DefaultSoflanLastSpeedNonPositive", 1, "Problem") },
        };
        yield return new CheckCase
        {
            Name = "default-soflan-last-speed-positive",
            Chart = new ChartBuilder()
                .Add("SFL\t1\t960\t240\t0.500000\t0")
                .Build(),
            Forbidden = new[] { "DefaultSoflanLastSpeedNonPositive" },
        };
        yield return new CheckCase
        {
            Name = "default-soflan-last-speed-non-positive-other-group",
            Chart = new ChartBuilder()
                .Add("SFL\t1\t960\t240\t0.000000\t5")
                .Build(),
            Forbidden = new[] { "DefaultSoflanLastSpeedNonPositive" },
        };

        // ---------------- 全部缺陷同时出现：9 条规则各命中一次 ----------------
        yield return new CheckCase
        {
            Name = "kitchen-sink",
            Chart = new ChartBuilder()
                .Replace(ProgJudgeDefault, "PROGJUDGE_BPM\t40000.000")
                .Replace(EstWave1, "EST\t0\t0\tWAVE2")
                .Add(
                    "BPL\tA0\tCEN\t0\tFIX\t1.000000\tN\tCIR\t0",
                    "ISF\t1\t960\t-12\t480\t24\t7",
                    "LLN\t9\t1\t0\t-24",
                    "BPM\t1\t0\t0.000",
                    "CLS\t2\t0\t0\t-12",
                    "OBS\t3\t0\t0\t-12\t1",
                    "SFL\t1\t960\t240\t0.000000\t0")
                .Build(),
            Expected = NewRuleNames.Select(x => new Expectation(x, 1, NewRuleSeverity(x))).ToArray(),
        };
    }

    private sealed class ChartBuilder
    {
        private readonly List<string> lines = new()
        {
            "[HEADER]",
            "VERSION\t1\t8\t0",
            "CREATOR\tchecker-tests",
            "BPM_DEF\t134.000\t134.000\t134.000\t134.000",
            "MET_DEF\t4\t4",
            "TRESOLUTION\t1920",
            "XRESOLUTION\t4096",
            "CLK_DEF\t1920",
            ProgJudgeDefault,
            "TUTORIAL\t0",
            "BULLET_DAMAGE\t1.000",
            "HARDBULLET_DAMAGE\t2.000",
            "DANGERBULLET_DAMAGE\t3.000",
            "BEAM_DAMAGE\t2.000",
            "T_TOTAL\t2",
            "[B_PALETTE]",
            BplA0,
            "[COMPOSITION]",
            // 注意：不要在这里写 BPM 0 0 —— 编辑器会按 BPM_DEF 生成 T[0,0] 的首个 BPM 记录，
            // 再写一条同位置的 BPM 会被 ObjectOverlap 判为重复（真实谱面同样不在 T0 写 BPM）。
            EstWave1,
            EstBoss,
            "[LANE]",
            "LLS\t1\t0\t0\t-24",
            "LLN\t1\t1\t0\t-24",
            "LLE\t1\t2\t0\t-24",
            "[NOTES]",
            "TAP\t1\t1\t0\t-24\t0",
            "HLD\t1\t1\t960\t-24\t0\t2\t0\t-24\t0",
        };

        public ChartBuilder Add(params string[] newLines)
        {
            lines.AddRange(newLines);
            return this;
        }

        public ChartBuilder Replace(string from, string to)
        {
            var index = lines.IndexOf(from);
            if (index < 0)
                throw new InvalidOperationException($"fixture line not found: {from}");

            lines[index] = to;
            return this;
        }

        public ChartBuilder Remove(string line)
        {
            if (!lines.Remove(line))
                throw new InvalidOperationException($"fixture line not found: {line}");

            return this;
        }

        public string Build() => string.Join("\n", lines) + "\n";
    }
}
