using AVATapper.Engine;

namespace AVATapper.Tests;

/// <summary>How the computer bartender gets on (set TAPPER_PROBE to run it, for tuning).</summary>
public sealed class TapperProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_serves()
    {
        if (Environment.GetEnvironmentVariable("TAPPER_PROBE") is null) return;
        foreach (int seed in new[] { 1, 2, 3, 4, 5, 6 })
        {
            var g = new GameEngine(seed, HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "tapper-probe-" + Guid.NewGuid().ToString("N")))) { Silent = true };
            g.StartGame();
            var why = new Dictionary<string, int>();
            var last = g.Phase;
            int bonuses = 0, right = 0, t = 0;
            for (; t < 60 * 900 && g.State == GameState.Playing; t++)
            {
                g.Tick(g.Brain.Decide(g));
                if (g.Phase == Phase.Oops && last != Phase.Oops) why[g.Mistake ?? "?"] = why.GetValueOrDefault(g.Mistake ?? "?") + 1;
                if (g.Phase == Phase.Bonus && g.Stage == BonusStage.Reveal && g.StageTime < 0.01) { bonuses++; if (g.PickedRight) right++; }
                last = g.Phase;
            }
            output.WriteLine($"seed {seed}: {t / 60}s level {g.Level} score {g.Score} lives {g.Lives} {g.State} bonus {right}/{bonuses} mistakes: {string.Join(", ", why.Select(kv => kv.Key + " " + kv.Value))}");
        }
    }
}
