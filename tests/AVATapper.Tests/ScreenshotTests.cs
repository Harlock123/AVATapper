using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVATapper.Engine;
using AVATapper.Views;

namespace AVATapper.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set TAPPER_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "avatapper-ui-" + Guid.NewGuid().ToString("N")))) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("TAPPER_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVATapper-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>Lets the computer bartender serve until something worth a picture is on screen.</summary>
    private static void ServeUntil(GameEngine g, GameCanvas c, Func<GameEngine, bool> done, int maxSeconds = 600)
    {
        for (int t = 0; t < 60 * maxSeconds && !done(g); t++)
        {
            g.Tick(g.Brain.Decide(g));
            if (t % 3 == 0) c.Advance(1 / 20.0);
            if (g.State != GameState.Playing) g.StartGame();
        }
        c.Advance(0.016);
    }

    private static bool Busy(GameEngine g) =>
        g.Phase == Phase.Serving && g.PhaseTime > 6 && g.Customers.Count(x => x.State != CustomerState.Leaving) >= 3 && g.Mugs.Count >= 2;

    [AvaloniaFact]
    public void The_title_and_the_high_scores()
    {
        var (w, c, g) = Open();
        g.Idle(30);
        c.Advance(0.6);
        Shot(w, c, "title");
        g.HighScores.Insert("FIZ", 61250, 9);
        g.HighScores.Insert("AVA", 45000, 6);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + GameEngine.AttractSeconds)) + 10);
        Shot(w, c, "high-scores");
    }

    [AvaloniaFact]
    public void Serving_in_all_four_venues()
    {
        var (w, c, g) = Open(6);
        g.StartGame();
        g.Idle(60);
        c.Advance(0.5);
        Shot(w, c, "level-start");
        ServeUntil(g, c, g => g.Level == 1 && Busy(g) && g.Bartender.Fill > 0.4);
        Shot(w, c, "saloon");
        ServeUntil(g, c, g => g.Level == 1 && g.Phase == Phase.Serving && g.Tips.Count > 0 && g.Customers.Any(x => x.State == CustomerState.Drinking));
        Shot(w, c, "tip");
        ServeUntil(g, c, g => g.Phase == Phase.Bonus && g.Stage == BonusStage.Shake);
        Shot(w, c, "bandit");
        ServeUntil(g, c, g => g.Phase == Phase.Bonus && g.Stage == BonusStage.Shuffle && g.SwapProgress is > 0.4 and < 0.6);
        Shot(w, c, "shuffle");
        ServeUntil(g, c, g => g.Level == 2 && Busy(g));
        Shot(w, c, "sports");
        ServeUntil(g, c, g => g.Level == 2 && g.Jukebox > 2);
        Shot(w, c, "jukebox");
        ServeUntil(g, c, g => g.Level == 3 && Busy(g));
        Shot(w, c, "punk");
        ServeUntil(g, c, g => g.Level == 4 && Busy(g));
        Shot(w, c, "space");
        ServeUntil(g, c, g => g.Phase == Phase.Oops && g.PhaseTime > 0.3);
        Shot(w, c, "oops");
    }
}
