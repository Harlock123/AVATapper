namespace AVATapper.Engine;

/// <summary>
/// The computer bartender: runs to catch empties before they reach the end of the bar, pours for
/// whichever bar has the most customers still waiting (never more mugs than takers, so none falls
/// off the end), and nips out for a tip when nothing is coming. In the bonus round it usually
/// follows the right can. Used for the demonstration and the tests.
/// </summary>
public sealed class BarkeepBrain(int seed)
{
    private readonly Random _rng = new(seed);
    private int _dyLast;
    private bool _pouring;
    private int _bonusTarget = -1;
    private bool _pickLast;

    public Controls Decide(GameEngine g)
    {
        if (g.Phase == Phase.Bonus) return BonusChoice(g);
        _bonusTarget = -1;
        if (g.Phase != Phase.Serving) { _pouring = false; return Release(default); }
        var b = g.Bartender;

        // An empty about to smash: be at that tap.
        Mug? danger = null;
        double soonest = double.MaxValue;
        foreach (var m in g.Mugs.Where(m => !m.Full && !m.Gone))
        {
            if (m.Bar == b.Bar && m.X <= b.X) continue;
            double t = (Field.TapX - 10 - m.X) / m.Speed;
            if (t < soonest) { soonest = t; danger = m; }
        }
        if (danger is not null && soonest < 0.5 + Math.Abs(BarsAway(b.Bar, danger.Bar)) * 0.12)
        {
            if (danger.Bar != b.Bar) return Go(b, danger.Bar);
            if (!b.AtTap) return Release(new Controls(1, 0));
        }

        // Out on the bar for a tip.
        if (!b.AtTap)
        {
            var tip = g.Tips.FirstOrDefault(t => t.Bar == b.Bar);
            if (tip is not null && Calm(g, 1.6)) return Release(new Controls(Math.Sign(tip.X - b.X), 0));
            return Release(new Controls(1, 0));
        }

        // Finish a pour under way.
        if (_pouring)
        {
            if (b.Fill < 1) return new Controls(0, 0, true);
            _pouring = false;
            return Release(default);
        }

        // Pour here if someone on this bar is still waiting for a mug.
        if (Waiting(g, b.Bar) > 0)
        {
            _pouring = true;
            return new Controls(0, 0, true);
        }

        // A tip on this bar and a quiet moment.
        var near = g.Tips.FirstOrDefault(t => t.Bar == b.Bar);
        if (near is not null && Calm(g, 2.4)) return Release(new Controls(-1, 0));

        // Otherwise the bar that needs serving most: the customer nearest his tap.
        int best = -1;
        double bestScore = 0;
        for (int bar = 0; bar < Field.Bars; bar++)
        {
            int waiting = Waiting(g, bar);
            if (waiting <= 0) continue;
            double front = g.Customers.Where(x => x.Bar == bar && x.State == CustomerState.Approaching).Select(x => x.X).DefaultIfEmpty(0).Max();
            double score = waiting + front / 200 - Math.Abs(BarsAway(b.Bar, bar)) * 0.3;
            if (best < 0 || score > bestScore) { best = bar; bestScore = score; }
        }
        if (best >= 0 && best != b.Bar) return Go(b, best);
        return Release(default);
    }

    /// <summary>Customers on a bar still waiting for a mug, less the mugs already on their way.</summary>
    private static int Waiting(GameEngine g, int bar) =>
        g.Customers.Count(x => x.Bar == bar && x.State == CustomerState.Approaching) - g.Mugs.Count(m => m.Bar == bar && m.Full && !m.Gone);

    /// <summary>No empty will reach the end, and no customer the tap, for a while.</summary>
    private static bool Calm(GameEngine g, double seconds) =>
        !g.Mugs.Any(m => !m.Full && (Field.TapX - m.X) / m.Speed < seconds + 0.6)
        && !g.Customers.Any(x => x.State == CustomerState.Approaching && (Field.PourX - 64 - x.X) / (g.WalkSpeed * 0.5) < seconds + 1.5);

    /// <summary>The shortest way round from one bar to another (they wrap): negative is up.</summary>
    private static int BarsAway(int from, int to)
    {
        int d = (to - from + Field.Bars) % Field.Bars;
        return d > Field.Bars / 2 ? d - Field.Bars : d;
    }

    private Controls Go(Bartender b, int bar)
    {
        _pouring = false;
        int dy = Math.Sign(BarsAway(b.Bar, bar));
        if (dy == 0 || _dyLast != 0) return Release(default);
        _dyLast = dy;
        return new Controls(0, dy);
    }

    private Controls Release(Controls c)
    {
        _dyLast = c.Dy;
        return c;
    }

    private Controls BonusChoice(GameEngine g)
    {
        if (g.Stage != BonusStage.Choose) { _pickLast = false; return default; }
        if (_bonusTarget < 0)
        {
            int right = Array.IndexOf(g.CanAt, g.Good);
            _bonusTarget = _rng.Next(4) == 0 ? _rng.Next(GameEngine.Cans) : right;
        }
        if (g.Cursor != _bonusTarget)
        {
            // Tap, don't hold.
            bool press = !_pickLast;
            _pickLast = press;
            return press ? new Controls(Math.Sign(_bonusTarget - g.Cursor), 0) : default;
        }
        bool pick = !_pickLast;
        _pickLast = pick;
        return new Controls(0, 0, pick);
    }
}
