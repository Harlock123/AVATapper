namespace AVATapper.Engine;

public sealed partial class GameEngine
{
    public const double PourSeconds = 0.42;
    public const double PushBack = 150;
    public const int FirstExtraLife = 20000, ExtraLifeEvery = 40000;

    public Bartender Bartender { get; private set; } = new();
    public List<Customer> Customers { get; } = [];
    public List<Mug> Mugs { get; } = [];
    public List<Tip> Tips { get; } = [];
    public int Level { get; private set; } = 1;
    public Venue Venue => Venues.Of(Level);
    public int Score { get; internal set; }
    public int Lives { get; internal set; }
    /// <summary>Customers still to come in this level, and how many have been served out of the door.</summary>
    public int ToCome { get; private set; }
    public int Served { get; private set; }
    /// <summary>A tip picked up: the jukebox plays and everyone stops to dance.</summary>
    public double Jukebox { get; private set; }
    /// <summary>What went wrong, and where, for the oops.</summary>
    public string? Mistake { get; private set; }
    public (int Bar, double X) MistakeAt { get; private set; }
    /// <summary>The computer bartender, for the demonstration (and the tests).</summary>
    public BarkeepBrain Brain { get; private set; } = new(1);

    private double _spawnTimer;
    private int _dyPrev;
    private int _nextExtra;
    private int _burst;

    /// <summary>Test hook: no customers come in unless a test puts them there.</summary>
    internal bool Calm { get; set; }

    private double Rand(double lo, double hi) => lo + _rng.NextDouble() * (hi - lo);

    // How the level's crowd behaves.
    public double WalkSpeed => Math.Min(120, 46 + Level * 6);
    public double PauseMean => Math.Max(0.35, 1.05 - Level * 0.07);
    public double FullSpeed => 290 + Level * 10;
    public double EmptySpeed => Math.Min(330, 170 + Level * 12);
    public double SpawnEvery => Math.Max(0.9, 3.0 - Level * 0.22);
    public int CrowdSize => 6 + Level * 3;

    private void NewGame()
    {
        _rng = new Random(_seed + (int)(StateTime * 997));
        Score = 0;
        Lives = StartingLives;
        _nextExtra = FirstExtraLife;
        Brain = new BarkeepBrain(_rng.Next());
        Effects.Clear();
        StartLevel(1);
    }

    private void StartLevel(int level)
    {
        Level = level;
        Customers.Clear();
        Mugs.Clear();
        Tips.Clear();
        Bartender = new Bartender();
        ToCome = CrowdSize;
        Served = 0;
        Jukebox = 0;
        _spawnTimer = 0.8;
        _burst = Field.Bars;
        Mistake = null;
        SetPhase(Phase.LevelStart);
    }

    internal void AddScore(int points, double x, double y)
    {
        Score += points;
        Effects.Add(new Effect { Kind = EffectKind.Score, X = x, Y = y, Text = points.ToString(), Life = 0.9, Colour = 0xFFFFE14A });
        if (Score >= _nextExtra)
        {
            _nextExtra += ExtraLifeEvery;
            Lives++;
            Play(Sfx.ExtraLife);
            Say("EXTRA BARTENDER!", 2);
        }
    }

    // ───────────────────────────── the step ─────────────────────────────

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        switch (Phase)
        {
            case Phase.LevelStart:
                if (PhaseTime > 2.8)
                {
                    SetPhase(Phase.Serving);
                    Music(true);
                }
                _dyPrev = c.Dy;
                return;
            case Phase.Oops:
                if (PhaseTime > 2.6) AfterOops();
                _dyPrev = c.Dy;
                return;
            case Phase.LevelClear:
                if (PhaseTime > 2.2) StartBonus();
                return;
            case Phase.Bonus:
                Bonus(dt, c);
                return;
        }
        Serve(c, dt);
        if (Phase != Phase.Serving) return;
        if (!Calm) Spawn(dt);
        MoveCustomers(dt);
        if (Phase != Phase.Serving) return;
        MoveMugs(dt);
        if (Phase != Phase.Serving) return;
        foreach (var t in Tips) t.Age += dt;
        Tips.RemoveAll(t => t.Age > 9);
        if (Jukebox > 0) Jukebox -= dt;
        Customers.RemoveAll(x => x.Gone);
        Mugs.RemoveAll(m => m.Gone);
        if (ToCome == 0 && Customers.Count == 0 && !Mugs.Any(m => m.Full))
        {
            Mugs.Clear();
            Music(false);
            Play(Sfx.LevelClear);
            Say("ALL SERVED!", 2);
            SetPhase(Phase.LevelClear);
        }
    }

    // ───────────────────────────── the bartender ─────────────────────────────

    private void Serve(Controls c, double dt)
    {
        var b = Bartender;
        if (b.Switch > 0) b.Switch = Math.Max(0, b.Switch - dt / 0.12);
        // Up and down run to the next tap (round from the bottom bar to the top).
        if (c.Dy != 0 && _dyPrev == 0)
        {
            b.FromBar = b.Bar;
            b.Bar = (b.Bar + c.Dy + Field.Bars) % Field.Bars;
            b.X = Field.PourX;
            b.Fill = 0;
            b.Switch = 1;
            b.Facing = -1;
            Play(Sfx.Step);
        }
        _dyPrev = c.Dy;
        // Pouring: hold at the tap until the mug is full, let go to send it down the bar.
        if (c.Pour && b.AtTap)
        {
            if (b.Fill == 0) Play(Sfx.Pour);
            b.Fill = Math.Min(1, b.Fill + dt / PourSeconds);
        }
        else if (b.PourHeld && b.Fill > 0)
        {
            if (b.Fill >= 1)
            {
                Mugs.Add(new Mug { Bar = b.Bar, X = Field.PourX - 30, Full = true, Speed = FullSpeed });
                Play(Sfx.Slide);
            }
            b.Fill = 0;
        }
        b.PourHeld = c.Pour;
        // Walking along the bar (not while pouring).
        if (c.Dx != 0 && !(c.Pour && b.Fill > 0))
        {
            b.X = Math.Clamp(b.X + c.Dx * Bartender.WalkSpeed * dt, Field.Left(b.Bar) + 30, Field.PourX);
            b.Facing = c.Dx;
            b.WalkPhase += dt * 12;
            if (!b.AtTap) b.Fill = 0;
        }
        // Picking up a tip.
        foreach (var t in Tips.Where(t => t.Bar == b.Bar && Math.Abs(t.X - b.X) < 26).ToList())
        {
            Tips.Remove(t);
            AddScore(1500, t.X, Field.CounterTop(t.Bar) - 30);
            Jukebox = 3.5;
            Play(Sfx.Jukebox);
            Effects.Add(new Effect { Kind = EffectKind.Notes, X = 520, Y = 120, Life = 3.5 });
        }
    }

    // ───────────────────────────── customers ─────────────────────────────

    private void Spawn(double dt)
    {
        _spawnTimer -= dt;
        if (ToCome <= 0 || _spawnTimer > 0) return;
        var open = Enumerable.Range(0, Field.Bars).Where(bar => !Customers.Any(x => x.Bar == bar && x.X < Field.Left(bar) + 70)).ToList();
        if (open.Count == 0) { _spawnTimer = 0.3; return; }
        int bar = open[_rng.Next(open.Count)];
        Customers.Add(new Customer { Bar = bar, X = Field.Left(bar) - 30, Stepping = true, StepTime = 0.7, Look = _rng.Next(16) });
        Play(Sfx.Door);
        ToCome--;
        _spawnTimer = _burst-- > 1 ? 0.5 : SpawnEvery * Rand(0.7, 1.3);
    }

    private void MoveCustomers(double dt)
    {
        foreach (var x in Customers)
        {
            x.Bob += dt;
            switch (x.State)
            {
                case CustomerState.Approaching:
                {
                    if (Jukebox > 0) break;
                    x.StepTime -= dt;
                    if (x.StepTime <= 0)
                    {
                        x.Stepping = !x.Stepping;
                        x.StepTime = x.Stepping ? 0.55 : PauseMean * Rand(0.6, 1.4);
                    }
                    if (!x.Stepping) break;
                    // Not through the one ahead of him.
                    double ahead = Customers.Where(o => o != x && o.Bar == x.Bar && o.X > x.X && !o.Gone).Select(o => o.X).DefaultIfEmpty(double.MaxValue).Min();
                    x.X = Math.Min(x.X + WalkSpeed * dt, ahead - 56);
                    if (x.X >= Field.PourX - 64)
                    {
                        x.State = CustomerState.Grabbing;
                        Lose("A CUSTOMER GOT TO THE TAP!", x.Bar, x.X);
                        return;
                    }
                    break;
                }
                case CustomerState.Pushed:
                    x.X -= 360 * dt;
                    x.Timer -= dt;
                    if (x.X < Field.Left(x.Bar) - 8)
                    {
                        x.State = CustomerState.Leaving;
                        Served++;
                        AddScore(50, Field.Left(x.Bar), Field.CounterTop(x.Bar) - 90);
                        Play(Sfx.Door);
                    }
                    else if (x.Timer <= 0)
                    {
                        x.State = CustomerState.Drinking;
                        x.Timer = 1.3;
                    }
                    break;
                case CustomerState.Drinking:
                    x.Timer -= dt;
                    if (x.Timer <= 0)
                    {
                        // Done: the empty goes back down the bar, and he wants another.
                        Mugs.Add(new Mug { Bar = x.Bar, X = x.X + 26, Full = false, Speed = EmptySpeed });
                        x.State = CustomerState.Approaching;
                        x.Stepping = false;
                        x.StepTime = 0.8;
                    }
                    break;
                case CustomerState.Leaving:
                    x.X -= 220 * dt;
                    if (x.X < Field.Left(x.Bar) - 80) x.Gone = true;
                    break;
            }
        }
    }

    // ───────────────────────────── mugs ─────────────────────────────

    private void MoveMugs(double dt)
    {
        var b = Bartender;
        foreach (var m in Mugs)
        {
            if (m.Gone) continue;
            if (m.Full)
            {
                m.X -= m.Speed * dt;
                // The first customer it reaches who's waiting for one catches it.
                var taker = Customers.Where(x => x.Bar == m.Bar && x.State == CustomerState.Approaching && x.X <= m.X + 4 && m.X - x.X < 24).OrderByDescending(x => x.X).FirstOrDefault();
                if (taker is not null)
                {
                    m.Gone = true;
                    taker.State = CustomerState.Pushed;
                    taker.Timer = PushBack / 360;
                    Play(Sfx.Catch);
                    if (_rng.Next(100) < 16 + Math.Min(10, Level))
                    {
                        Tips.Add(new Tip { Bar = taker.Bar, X = taker.X + 6 });
                        Play(Sfx.Tip);
                    }
                    continue;
                }
                // Off the end, unless someone's just coming in at the door to catch it.
                if (m.X < Field.Left(m.Bar) - 8 && !Customers.Any(x => x.Bar == m.Bar && x.State == CustomerState.Approaching && x.X < m.X))
                {
                    m.Gone = true;
                    Lose("A FULL MUG FELL OFF THE END!", m.Bar, m.X);
                    return;
                }
            }
            else
            {
                m.X += m.Speed * dt;
                if (b.Bar == m.Bar && b.Switch <= 0 && m.X >= b.X - 26 && m.X <= b.X + 14)
                {
                    m.Gone = true;
                    AddScore(100, b.X, Field.CounterTop(m.Bar) - 60);
                    Play(Sfx.Clink);
                    continue;
                }
                if (m.X >= Field.TapX - 10)
                {
                    m.Gone = true;
                    Lose("AN EMPTY MUG SMASHED!", m.Bar, m.X);
                    return;
                }
            }
        }
    }

    // ───────────────────────────── mistakes ─────────────────────────────

    private void Lose(string why, int bar, double x)
    {
        Mistake = why;
        MistakeAt = (bar, x);
        Lives--;
        Music(false);
        Play(why.StartsWith("A CUSTOMER") ? Sfx.Grab : Sfx.Smash);
        Play(Sfx.Oops);
        if (!why.StartsWith("A CUSTOMER"))
            Effects.Add(new Effect { Kind = EffectKind.Smash, X = x, Y = Field.CounterTop(bar) + 50, Life = 2.4 });
        Say(why, 2.6);
        SetPhase(Phase.Oops);
    }

    /// <summary>After a mistake the bar is cleared: mugs and tips gone, customers back by the doors.</summary>
    private void AfterOops()
    {
        if (Lives <= 0) { GameOver(); return; }
        Mugs.Clear();
        Tips.Clear();
        Jukebox = 0;
        foreach (var bar in Customers.GroupBy(x => x.Bar))
        {
            int k = 0;
            foreach (var x in bar.Where(x => x.State != CustomerState.Leaving).OrderBy(x => x.X))
            {
                x.State = CustomerState.Approaching;
                x.X = Field.Left(x.Bar) + 10 + k++ * 60;
                x.Stepping = false;
                x.StepTime = 1.2;
            }
        }
        Bartender.X = Field.PourX;
        Bartender.Fill = 0;
        Mistake = null;
        SetPhase(Phase.Serving);
        Music(true);
    }

    // ───────────────────────────── test hooks ─────────────────────────────

    /// <summary>Test hook: skip the venue's name.</summary>
    internal void Go()
    {
        if (Phase != Phase.LevelStart) return;
        SetPhase(Phase.Serving);
    }

    internal void Run(int ticks, Controls c)
    {
        for (int i = 0; i < ticks; i++) Tick(c);
    }

    internal void Idle(int ticks) => Run(ticks, default);

    internal Customer PutCustomer(int bar, double x)
    {
        var c = new Customer { Bar = bar, X = x, StepTime = 99, Stepping = false };
        Customers.Add(c);
        return c;
    }

    internal Mug PutMug(int bar, double x, bool full) =>
        Add(new Mug { Bar = bar, X = x, Full = full, Speed = full ? FullSpeed : EmptySpeed });

    private Mug Add(Mug m)
    {
        Mugs.Add(m);
        return m;
    }

    internal Tip PutTip(int bar, double x)
    {
        var t = new Tip { Bar = bar, X = x };
        Tips.Add(t);
        return t;
    }

    /// <summary>Test hook: how many customers this level still has to send in.</summary>
    internal void SetToCome(int n) => ToCome = n;

    internal void StartAt(int level) => StartLevel(level);
}
