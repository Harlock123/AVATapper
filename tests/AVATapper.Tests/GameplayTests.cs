using AVATapper.Engine;
using Engine = AVATapper.Engine;

namespace AVATapper.Tests;

public class GameplayTests
{
    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "avatapper-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>A game under way, the venue's name skipped, and no customers unless a test puts them there.</summary>
    private static GameEngine Game(int seed = 7)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(TempFolder())) { Silent = true, Calm = true };
        g.StartGame();
        g.Go();
        g.Idle(1);
        return g;
    }

    private static Engine.Controls Press(int dx = 0, int dy = 0, bool pour = false) => new(dx, dy, pour);

    /// <summary>Holds the tap until the mug is full and lets go: a mug down the bar.</summary>
    private static void PourOne(GameEngine g)
    {
        g.Run((int)(GameEngine.PourSeconds * 60) + 3, Press(pour: true));
        g.Run(1, default);
    }

    // ───────────────────────────── the bartender ─────────────────────────────

    [Fact]
    public void Up_and_down_run_between_the_four_taps_and_round()
    {
        var g = Game();
        Assert.Equal(0, g.Bartender.Bar);
        g.Run(1, Press(dy: 1));
        g.Run(1, default);
        Assert.Equal(1, g.Bartender.Bar);
        g.Run(10, Press(dy: 1));
        Assert.Equal(2, g.Bartender.Bar);
        g.Run(1, default);
        g.Run(1, Press(dy: -1));
        g.Run(1, default);
        g.Run(1, Press(dy: -1));
        g.Run(1, default);
        g.Run(1, Press(dy: -1));
        Assert.Equal(3, g.Bartender.Bar);
        Assert.True(g.Bartender.AtTap);
    }

    [Fact]
    public void Left_and_right_walk_the_bar_but_not_past_its_ends()
    {
        var g = Game();
        g.Run(30, Press(dx: -1));
        Assert.True(g.Bartender.X < Field.PourX - 100);
        g.Run(300, Press(dx: -1));
        Assert.Equal(Field.Left(0) + 30, g.Bartender.X);
        g.Run(300, Press(dx: 1));
        Assert.True(g.Bartender.AtTap);
    }

    [Fact]
    public void A_full_mug_slides_off_down_the_bar_only_when_it_was_filled()
    {
        var g = Game();
        // Let go too soon and nothing goes.
        g.Run(10, Press(pour: true));
        Assert.InRange(g.Bartender.Fill, 0.2, 0.9);
        g.Run(1, default);
        Assert.Empty(g.Mugs);
        Assert.Equal(0, g.Bartender.Fill);
        PourOne(g);
        var m = Assert.Single(g.Mugs);
        Assert.True(m.Full);
        double x = m.X;
        g.Idle(10);
        Assert.True(m.X < x - 30);
    }

    [Fact]
    public void Away_from_the_tap_there_is_no_pouring()
    {
        var g = Game();
        g.Run(20, Press(dx: -1));
        g.Run(40, Press(pour: true));
        Assert.Equal(0, g.Bartender.Fill);
        g.Run(1, default);
        Assert.Empty(g.Mugs);
    }

    // ───────────────────────────── customers ─────────────────────────────

    [Fact]
    public void A_customer_catches_the_mug_slides_back_drinks_and_returns_the_empty()
    {
        var g = Game();
        var c = g.PutCustomer(0, 500);
        PourOne(g);
        g.Idle(90);
        Assert.Equal(CustomerState.Drinking, c.State);
        Assert.True(c.X < 500 - GameEngine.PushBack + 20);
        g.Idle(90);
        Assert.Equal(CustomerState.Approaching, c.State);
        var empty = Assert.Single(g.Mugs);
        Assert.False(empty.Full);
        // The bartender at the tap catches it.
        g.Idle(200);
        Assert.Empty(g.Mugs);
        Assert.Equal(100, g.Score);
    }

    [Fact]
    public void A_customer_near_the_door_is_pushed_out_and_scores_50()
    {
        var g = Game();
        g.SetToCome(0);
        var c = g.PutCustomer(0, Field.Left(0) + 40);
        PourOne(g);
        g.Idle(150);
        Assert.Equal(CustomerState.Leaving, c.State);
        Assert.Equal(50, g.Score);
        Assert.Equal(1, g.Served);
    }

    [Fact]
    public void The_first_customer_down_the_bar_takes_the_mug()
    {
        var g = Game();
        var near = g.PutCustomer(0, 600);
        var far = g.PutCustomer(0, 400);
        PourOne(g);
        g.Idle(56);
        Assert.Equal(CustomerState.Pushed, near.State);
        Assert.Equal(CustomerState.Approaching, far.State);
    }

    [Fact]
    public void Customers_walk_up_the_bar_and_queue_rather_than_pass()
    {
        var g = Game();
        var a = g.PutCustomer(1, 400);
        var b = g.PutCustomer(1, 380);
        a.StepTime = b.StepTime = 0;
        g.Idle(60 * 5);
        Assert.True(a.X > 420);
        Assert.True(a.X - b.X >= 55);
    }

    // ───────────────────────────── mistakes ─────────────────────────────

    [Fact]
    public void A_mug_with_nobody_to_catch_it_falls_off_the_end()
    {
        var g = Game();
        PourOne(g);
        g.Idle(60 * 4);
        Assert.Equal(Phase.Oops, g.Phase);
        Assert.Contains("FELL OFF", g.Mistake);
        Assert.Equal(GameEngine.StartingLives - 1, g.Lives);
    }

    [Fact]
    public void An_empty_with_nobody_at_its_tap_smashes()
    {
        var g = Game();
        g.PutMug(2, 700, full: false);
        g.Idle(60 * 3);
        Assert.Equal(Phase.Oops, g.Phase);
        Assert.Contains("SMASHED", g.Mistake);
    }

    [Fact]
    public void An_empty_is_caught_anywhere_along_the_bar()
    {
        var g = Game();
        g.Run(40, Press(dx: -1));
        g.PutMug(0, g.Bartender.X - 200, full: false);
        g.Idle(60 * 2);
        Assert.Equal(Phase.Serving, g.Phase);
        Assert.Equal(100, g.Score);
    }

    [Fact]
    public void A_customer_who_reaches_the_tap_costs_a_life()
    {
        var g = Game();
        var c = g.PutCustomer(3, Field.PourX - 80);
        c.StepTime = 0;
        for (int i = 0; i < 60 * 3 && g.Phase != Phase.Oops; i++) g.Idle(1);
        Assert.Equal(Phase.Oops, g.Phase);
        Assert.Equal(CustomerState.Grabbing, c.State);
        Assert.Contains("CUSTOMER", g.Mistake);
    }

    [Fact]
    public void After_a_mistake_the_bar_is_cleared_and_customers_go_back_to_the_doors()
    {
        var g = Game();
        var c = g.PutCustomer(1, 700);
        g.PutMug(1, 750, full: false);
        g.PutMug(2, 600, full: true);
        g.Idle(60);
        Assert.Equal(Phase.Oops, g.Phase);
        g.Idle(60 * 3);
        Assert.Equal(Phase.Serving, g.Phase);
        Assert.Empty(g.Mugs);
        Assert.True(c.X < Field.Left(1) + 80);
    }

    [Fact]
    public void Out_of_bartenders_the_game_is_over()
    {
        var g = Game();
        g.Lives = 1;
        g.PutMug(0, 800, full: false);
        g.Run(1, Press(dy: 1));
        g.Idle(60 * 5);
        Assert.Equal(GameState.GameOver, g.State);
    }

    // ───────────────────────────── tips and the level ─────────────────────────────

    [Fact]
    public void A_tip_scores_1500_and_starts_the_jukebox_which_stops_everyone()
    {
        var g = Game();
        var t = g.PutTip(0, 600);
        var c = g.PutCustomer(1, 300);
        c.StepTime = 0;
        g.Run(120, Press(dx: -1));
        Assert.DoesNotContain(t, g.Tips);
        Assert.Equal(1500, g.Score);
        Assert.True(g.Jukebox > 0);
        double x = c.X;
        g.Idle(60);
        Assert.Equal(x, c.X);
    }

    [Fact]
    public void Serving_everyone_clears_the_level_and_the_bonus_round_follows()
    {
        var g = Game();
        g.SetToCome(0);
        g.Idle(2);
        Assert.Equal(Phase.LevelClear, g.Phase);
        g.Idle(60 * 3);
        Assert.Equal(Phase.Bonus, g.Phase);
    }

    [Fact]
    public void The_crowd_gets_bigger_and_faster()
    {
        var g = Game();
        int crowd = g.CrowdSize;
        double walk = g.WalkSpeed, empty = g.EmptySpeed;
        g.StartAt(5);
        Assert.True(g.CrowdSize > crowd);
        Assert.True(g.WalkSpeed > walk);
        Assert.True(g.EmptySpeed > empty);
        Assert.NotEqual(Venues.Of(1), Venues.Of(2));
        Assert.Equal(Venues.Of(1), Venues.Of(5));
    }

    // ───────────────────────────── the bonus round ─────────────────────────────

    /// <summary>Plays the bonus round through to the choosing.</summary>
    private static void ToChoice(GameEngine g)
    {
        g.BonusNow();
        for (int i = 0; i < 60 * 30 && g.Stage != BonusStage.Choose; i++) g.Tick(default);
        Assert.Equal(BonusStage.Choose, g.Stage);
    }

    private static void PickSlot(GameEngine g, int slot)
    {
        while (g.Cursor != slot)
        {
            g.Tick(Press(dx: Math.Sign(slot - g.Cursor)));
            g.Tick(default);
        }
        g.Tick(Press(pour: true));
    }

    [Fact]
    public void Picking_the_can_he_didnt_shake_scores_3000_and_moves_on()
    {
        var g = Game();
        int level = g.Level;
        ToChoice(g);
        int score = g.Score;
        PickSlot(g, Array.IndexOf(g.CanAt, g.Good));
        Assert.Equal(BonusStage.Reveal, g.Stage);
        Assert.True(g.PickedRight);
        Assert.Equal(score + GameEngine.BonusPoints, g.Score);
        g.Idle(60 * 3);
        Assert.Equal(level + 1, g.Level);
        Assert.Equal(Phase.LevelStart, g.Phase);
    }

    [Fact]
    public void Picking_a_shaken_can_sprays_and_scores_nothing()
    {
        var g = Game();
        ToChoice(g);
        int score = g.Score;
        PickSlot(g, (Array.IndexOf(g.CanAt, g.Good) + 1) % GameEngine.Cans);
        Assert.False(g.PickedRight);
        Assert.Equal(score, g.Score);
    }

    [Fact]
    public void The_shuffle_keeps_every_can_and_the_bandit_shakes_all_but_one()
    {
        var g = Game();
        g.BonusNow();
        var shaken = new HashSet<int>();
        for (int i = 0; i < 60 * 30 && g.Stage != BonusStage.Choose; i++)
        {
            g.Tick(default);
            if (g.Shaking >= 0) shaken.Add(g.Shaking);
        }
        Assert.Equal(GameEngine.Cans - 1, shaken.Count);
        Assert.DoesNotContain(g.Good, shaken);
        Assert.Equal(Enumerable.Range(0, GameEngine.Cans), g.CanAt.Order());
    }

    // ───────────────────────────── the machine ─────────────────────────────

    [Fact]
    public void The_computer_bartender_clears_the_first_levels()
    {
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var g = new GameEngine(seed, HighScoreTable.Load(TempFolder())) { Silent = true };
            g.StartGame();
            for (int t = 0; t < 60 * 240 && g.Level < 3 && g.State == GameState.Playing; t++) g.Tick(g.Brain.Decide(g));
            Assert.True(g.Level >= 3, $"seed {seed} stopped at level {g.Level}");
        }
    }

    [Fact]
    public void Extra_bartenders_come_with_the_score()
    {
        var g = Game();
        g.AddScore(GameEngine.FirstExtraLife, 0, 0);
        Assert.Equal(GameEngine.StartingLives + 1, g.Lives);
        g.AddScore(GameEngine.ExtraLifeEvery - 1, 0, 0);
        Assert.Equal(GameEngine.StartingLives + 1, g.Lives);
        g.AddScore(1, 0, 0);
        Assert.Equal(GameEngine.StartingLives + 2, g.Lives);
    }

    [Fact]
    public void The_attract_mode_plays_itself_then_shows_the_high_scores()
    {
        var g = new GameEngine(5, HighScoreTable.Load(TempFolder())) { Silent = true };
        g.Idle((int)(60 * GameEngine.TitleIdleSeconds) + 2);
        Assert.Equal(GameState.Attract, g.State);
        g.Idle(60 * 30);
        Assert.True(g.Score > 0);
        g.Idle((int)(60 * (GameEngine.AttractSeconds - 30)) + 2);
        Assert.Equal(GameState.HighScores, g.State);
    }

    [Fact]
    public void A_good_score_asks_for_initials()
    {
        var g = Game();
        g.Score = 99999;
        g.Lives = 1;
        g.PutMug(0, 800, full: false);
        g.Run(1, Press(dy: 1));
        g.Idle(60 * 8);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Type('T');
        g.Initials.Type('A');
        g.Initials.Type('P');
        g.Initials.Finish();
        g.Idle(1);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(new HighScoreEntry("TAP", 99999, 1), g.HighScores.Entries[0]);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Play()
        {
            var g = new GameEngine(9, HighScoreTable.Load(TempFolder())) { Silent = true };
            g.StartGame();
            for (int t = 0; t < 60 * 90; t++) g.Tick(g.Brain.Decide(g));
            return $"{g.Score}:{g.Level}:{g.Lives}:{g.Served}";
        }
        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void High_scores_persist_in_their_folder()
    {
        string folder = TempFolder();
        var t = HighScoreTable.Load(folder);
        Assert.Equal(HighScoreTable.Size, t.Entries.Count);
        t.Insert("BIG", 123456, 9);
        t.Save();
        Assert.Equal(new HighScoreEntry("BIG", 123456, 9), HighScoreTable.Load(folder).Entries[0]);
    }
}
