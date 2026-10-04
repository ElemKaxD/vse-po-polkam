using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AllOnShelves.Core;

// Генератор уровней «Всё по полкам!» вне Unity (те же исходники ядра).
// dotnet run -c Release -- [from] [to] [attempts] [mode]
// mode: 0 — Лёгкий (levels.json), 1 — Средний (levels_medium.json), 2 — Сложный (levels_hard.json)
// Пишет Assets/AllOnShelves/Resources/Levels/<файл>.json и Tools/LevelTool/report[_medium|_hard].csv

static class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        int from = args.Length > 0 ? int.Parse(args[0]) : 1;
        int to = args.Length > 1 ? int.Parse(args[1]) : LevelPlanner.LevelCount;
        int attempts = args.Length > 2 ? int.Parse(args[2]) : 30;
        int mode = args.Length > 3 ? int.Parse(args[3]) : 0;
        string file = LevelPlanner.ModeFiles[mode];

        string root = FindProjectRoot();
        string outDir = Path.Combine(root, "Assets", "AllOnShelves", "Resources", "Levels");
        Directory.CreateDirectory(outDir);
        string outFile = Path.Combine(outDir, file + ".json");

        var opts = new JsonSerializerOptions { IncludeFields = true, WriteIndented = false };

        // существующие уровни (перегенерируем только диапазон)
        var existing = new LevelData[LevelPlanner.LevelCount + 1];
        if (File.Exists(outFile))
        {
            var set = JsonSerializer.Deserialize<LevelSet>(File.ReadAllText(outFile), opts);
            if (set?.levels != null) foreach (var l in set.levels) if (l.id <= LevelPlanner.LevelCount) existing[l.id] = l;
        }

        var sw = Stopwatch.StartNew();
        var results = new ConcurrentDictionary<int, LevelData>();
        Parallel.For(from, to + 1, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, id =>
        {
            var t = Stopwatch.StartNew();
            var lvl = LevelPlanner.Build(id, attempts, 250000, mode);
            results[id] = lvl;
            string cls = lvl == null ? "FAILED" : LevelEvaluator.Classify(lvl);
            Console.WriteLine($"L{id,3} d{LevelPlanner.DistrictOf(id),2} target={LevelPlanner.DifficultyOf(id, mode),-9} got={cls,-10} " +
                              (lvl == null ? "" : $"peak={lvl.metrics.minPeakCart} greedy={lvl.metrics.greedyWin} rnd={lvl.metrics.randomWinRate:0.00} moves={lvl.metrics.moves}") +
                              $" {t.ElapsedMilliseconds}ms");
        });

        foreach (var kv in results) existing[kv.Key] = kv.Value;
        var all = existing.Where(l => l != null).OrderBy(l => l.id).ToArray();
        File.WriteAllText(outFile, JsonSerializer.Serialize(new LevelSet { version = 1, levels = all }, opts), new UTF8Encoding(false));

        var csv = new StringBuilder("id;district;target;class;goals_sets;types;sections;window;visible;cart;tokens;minPeak;greedy;random;moves;mechanic;features\n");
        foreach (var l in all)
        {
            int sets = l.goals.Sum(g => g.sets);
            int tokens = l.belts.Sum(b => b.tokens.Length);
            var feats = string.Join(",", new[]
            {
                l.sections.Any(s => s.lockSets > 0) ? "lock" : null,
                l.belts.Any(b => b.tokens.Any(x => x.StartsWith("box:"))) ? "box" : null,
                l.customers.Length > 0 ? "customer" : null,
                l.perishTimer > 0 ? "perish" : null,
                l.sections.Any(s => s.kind == "freezer") ? "freezer" : null,
                l.goals.Any(g => ItemCatalog.IsBig(ItemCatalog.IndexOf(g.type))) ? "big" : null,
                l.belts.Any(b => b.tokens.Any(x => x.StartsWith("pallet:"))) ? "pallet" : null,
                l.sections.Any(s => s.kind == "sale") ? "sale" : null,
                l.belts.Any(b => b.tokens.Any(x => x.StartsWith("bundle:"))) ? "bundle" : null,
                l.belts.Length > 1 ? "belt2" : null,
                l.doorOpen > 0 ? "door" : null,
                l.night ? "night" : null,
                l.revision ? "revision" : null,
            }.Where(x => x != null));
            csv.Append($"{l.id};{l.district};{LevelPlanner.DifficultyOf(l.id, mode)};{LevelEvaluator.Classify(l)};{sets};{l.goals.Length};{l.sections.Length};" +
                       $"{l.window};{l.visible};{l.cart};{tokens};{l.metrics.minPeakCart};{l.metrics.greedyWin};{l.metrics.randomWinRate:0.00};{l.metrics.moves};{l.mechanic};{feats}\n");
        }
        File.WriteAllText(Path.Combine(root, "Tools", "LevelTool", file.Replace("levels", "report") + ".csv"), csv.ToString(), new UTF8Encoding(true));

        int failed = Enumerable.Range(from, to - from + 1).Count(i => results.TryGetValue(i, out var l) && l == null);
        int match = all.Count(l => l.id >= from && l.id <= to && LevelEvaluator.Distance(LevelEvaluator.Classify(l), LevelPlanner.DifficultyOf(l.id, mode)) == 0);
        Console.WriteLine($"done in {sw.Elapsed.TotalSeconds:0}s; levels={all.Length}; failed={failed}; class match {match}/{to - from + 1 - failed}");
        return failed > 0 ? 1 : 0;
    }

    static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Assets"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new Exception("Unity project root not found");
    }
}
