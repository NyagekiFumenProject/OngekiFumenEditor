#!/usr/bin/env python3
"""Compare BenchmarkDotNet CSV reports between two runtime TFMs (net10 vs net11).

Usage:
    python compare-tfm.py <baseline-dir> <candidate-dir> [--json out.json]

Reads every *-report.csv in both dirs, aligns cases by (class, method, params),
and reports Mean (ns) / Allocated (B) / Gen0 deltas of candidate vs baseline.
"""
import csv
import glob
import json
import os
import re
import sys
from statistics import geometric_mean

CONFIG_COLS = {
    'Method', 'Job', 'AnalyzeLaunchVariance', 'EvaluateOverhead', 'MaxAbsoluteError',
    'MaxRelativeError', 'MinInvokeCount', 'MinIterationTime', 'OutlierMode', 'Affinity',
    'EnvironmentVariables', 'Jit', 'LargeAddressAware', 'Platform', 'PowerPlanMode',
    'Runtime', 'AllowVeryLargeObjects', 'Concurrent', 'CpuGroups', 'Force',
    'HeapAffinitizeMask', 'HeapCount', 'NoAffinitize', 'RetainVm', 'Server', 'Arguments',
    'BuildConfiguration', 'Clock', 'EngineFactory', 'NuGetReferences', 'Toolchain',
    'IsMutator', 'InvocationCount', 'IterationCount', 'IterationTime', 'LaunchCount',
    'MaxIterationCount', 'MaxWarmupIterationCount', 'MemoryRandomization',
    'MinIterationCount', 'MinWarmupIterationCount', 'RunStrategy', 'UnrollFactor',
    'WarmupCount', 'Implementation',
}
RESULT_COLS = {
    'Mean', 'Error', 'StdDev', 'Median', 'Ratio', 'RatioSD',
    'Gen0', 'Gen1', 'Gen2', 'Allocated', 'Alloc Ratio',
}

TIME_UNITS = {
    'ns': 1.0, 'us': 1e3, 'µs': 1e3, 'μs': 1e3, 'ms': 1e6, 's': 1e9,
}
SIZE_UNITS = {
    'B': 1.0, 'KB': 1024.0, 'MB': 1024.0 ** 2, 'GB': 1024.0 ** 3,
}


def parse_value(text, units):
    text = (text or '').strip()
    if not text or text in ('-', 'NA', '?', 'Default', 'ColdStart'):
        return None
    m = re.match(r'^([\d.,]+)\s*([A-Za-zµμ]*)$', text)
    if not m:
        return None
    number = float(m.group(1).replace(',', ''))
    unit = m.group(2)
    if not unit:
        return number
    if unit not in units:
        return None
    return number * units[unit]


def load_dir(path):
    cases = {}
    for csv_path in glob.glob(os.path.join(path, '*-report.csv')):
        class_name = os.path.basename(csv_path).replace('-report.csv', '')
        with open(csv_path, newline='', encoding='utf-8-sig') as fh:
            reader = csv.DictReader(fh)
            for row in reader:
                method = row.get('Method') or ''
                params = tuple(
                    (k, row[k]) for k in row
                    if k not in CONFIG_COLS and k not in RESULT_COLS
                )
                key = (class_name, method, params)
                cases[key] = {
                    'mean_ns': parse_value(row.get('Mean'), TIME_UNITS),
                    'stddev_ns': parse_value(row.get('StdDev'), TIME_UNITS),
                    'alloc_bytes': parse_value(row.get('Allocated'), SIZE_UNITS),
                    'gen0': parse_value(row.get('Gen0'), {'': 1.0}),
                    'runtime': row.get('Runtime', ''),
                }
    return cases


def pct(new, old):
    if new is None or old is None or old == 0:
        return None
    return (new - old) / old * 100.0


def short_class(name):
    return name.split('.')[-1]


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    base_dir, cand_dir = sys.argv[1], sys.argv[2]
    json_out = None
    if '--json' in sys.argv:
        json_out = sys.argv[sys.argv.index('--json') + 1]

    base = load_dir(base_dir)
    cand = load_dir(cand_dir)

    only_base = sorted(set(base) - set(cand))
    only_cand = sorted(set(cand) - set(base))
    common = sorted(set(base) & set(cand))

    rows = []
    for key in common:
        class_name, method, params = key
        b, c = base[key], cand[key]
        cv10 = (b['stddev_ns'] / b['mean_ns']) if b['mean_ns'] and b['stddev_ns'] is not None else None
        cv11 = (c['stddev_ns'] / c['mean_ns']) if c['mean_ns'] and c['stddev_ns'] is not None else None
        rows.append({
            'class': short_class(class_name),
            'method': method,
            'params': dict(params),
            'mean10': b['mean_ns'], 'mean11': c['mean_ns'],
            'mean_delta_pct': pct(c['mean_ns'], b['mean_ns']),
            'alloc10': b['alloc_bytes'], 'alloc11': c['alloc_bytes'],
            'alloc_delta_pct': pct(c['alloc_bytes'], b['alloc_bytes']),
            'gen0_10': b['gen0'], 'gen0_11': c['gen0'],
            'cv10': cv10, 'cv11': cv11,
        })

    timed = [r for r in rows if r['mean_delta_pct'] is not None]
    faster = [r for r in timed if r['mean_delta_pct'] < -2]
    slower = [r for r in timed if r['mean_delta_pct'] > 2]
    same = [r for r in timed if -2 <= r['mean_delta_pct'] <= 2]

    def confident(r, limit=0.15):
        return (r['cv10'] is not None and r['cv11'] is not None
                and r['cv10'] <= limit and r['cv11'] <= limit)

    hi = [r for r in timed if confident(r)]

    print(f'# .NET TFM 对比: {base_dir} -> {cand_dir}')
    print()
    print(f'- 对齐用例: {len(common)} (仅基线 {len(only_base)}, 仅候选 {len(only_cand)})')
    print(f'- 变快(>2%): {len(faster)} / 变慢(>2%): {len(slower)} / 持平(±2%): {len(same)}')
    if timed:
        ratios = [r['mean11'] / r['mean10'] for r in timed if r['mean10'] and r['mean11']]
        if ratios:
            print(f'- 几何平均 Mean 比 (net11/net10), 全部: {geometric_mean(ratios):.4f}')
    if hi:
        ratios = [r['mean11'] / r['mean10'] for r in hi if r['mean10'] and r['mean11']]
        if ratios:
            hi_faster = sum(1 for r in hi if r['mean_delta_pct'] < -2)
            hi_slower = sum(1 for r in hi if r['mean_delta_pct'] > 2)
            print(f'- 高置信子集 (两轮 CV<=15%): {len(hi)} 用例, '
                  f'几何平均 {geometric_mean(ratios):.4f}, 变快 {hi_faster} / 变慢 {hi_slower}')
    alloc_rows = [r for r in rows if r['alloc_delta_pct'] is not None]
    if alloc_rows:
        zeroed = [r for r in alloc_rows if r['alloc10'] and r['alloc10'] > 0 and r['alloc11'] == 0]
        ratios = [r['alloc11'] / r['alloc10'] for r in alloc_rows
                  if r['alloc10'] and r['alloc10'] > 0 and r['alloc11'] and r['alloc11'] > 0]
        if ratios:
            print(f'- 几何平均 Alloc 比 (net11/net10), 正样本: {geometric_mean(ratios):.4f}')
        if zeroed:
            print(f'- 分配归零用例 (net11 降到 0B, net10 有分配): {len(zeroed)}')
        if not ratios and not zeroed:
            print('- 几何平均 Alloc 比: (无可比样本)')
    print()

    print('## 按类聚合（Mean 几何平均比）')
    print()
    print('| Benchmark 类 | 用例数 | 几何平均 Mean 比 | 高置信子集比 | 最快提升 | 最大退步 |')
    print('| --- | ---: | ---: | ---: | ---: | ---: |')
    by_class = {}
    for r in timed:
        by_class.setdefault(r['class'], []).append(r)
    for cls in sorted(by_class):
        items = by_class[cls]
        ratios = [r['mean11'] / r['mean10'] for r in items if r['mean10'] and r['mean11']]
        if not ratios:
            continue
        gm = geometric_mean(ratios)
        hi_items = [r for r in items if confident(r)]
        hi_ratios = [r['mean11'] / r['mean10'] for r in hi_items if r['mean10'] and r['mean11']]
        hi_text = f'{geometric_mean(hi_ratios):.4f} (n={len(hi_items)})' if hi_ratios else '-'
        best = min(items, key=lambda r: r['mean_delta_pct'])
        worst = max(items, key=lambda r: r['mean_delta_pct'])
        print(f"| {cls} | {len(items)} | {gm:.4f} | {hi_text} | {best['mean_delta_pct']:+.1f}% "
              f"({best['method']}) | {worst['mean_delta_pct']:+.1f}% ({worst['method']}) |")
    print()

    print('## 明细（按 Mean 变化排序，前 30 快 / 前 15 慢）')
    print()
    print('| 类 | Method | 参数 | Mean(net10) | Mean(net11) | Δ% | Alloc10 | Alloc11 | Δ% | Gen0 10→11 |')
    print('| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |')

    def fmt_time(v):
        if v is None:
            return '-'
        if v < 1e3:
            return f'{v:.2f} ns'
        if v < 1e6:
            return f'{v / 1e3:.2f} us'
        return f'{v / 1e6:.2f} ms'

    def fmt_size(v):
        if v is None:
            return '-'
        if v == 0:
            return '0 B'
        if v < 1024:
            return f'{v:.0f} B'
        return f'{v / 1024:.1f} KB'

    def emit(r):
        p = ' '.join(f'{k}={v}' for k, v in r['params'].items())
        d = r['mean_delta_pct']
        da = r['alloc_delta_pct']
        g0 = (f"{r['gen0_10']}→{r['gen0_11']}"
              if r['gen0_10'] is not None and r['gen0_11'] is not None else '-')
        print(f"| {r['class']} | {r['method']} | {p} | {fmt_time(r['mean10'])} | "
              f"{fmt_time(r['mean11'])} | {d:+.1f}% | {fmt_size(r['alloc10'])} | "
              f"{fmt_size(r['alloc11'])} | {da:+.1f}% | {g0} |" if da is not None else
              f"| {r['class']} | {r['method']} | {p} | {fmt_time(r['mean10'])} | "
              f"{fmt_time(r['mean11'])} | {d:+.1f}% | {fmt_size(r['alloc10'])} | "
              f"{fmt_size(r['alloc11'])} | - | {g0} |")

    for r in sorted(timed, key=lambda x: x['mean_delta_pct'])[:30]:
        emit(r)
    print()
    for r in sorted(timed, key=lambda x: -x['mean_delta_pct'])[:15]:
        emit(r)

    if json_out:
        with open(json_out, 'w', encoding='utf-8') as fh:
            json.dump(rows, fh, ensure_ascii=False, indent=2)
        print(f'\nJSON -> {json_out}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
