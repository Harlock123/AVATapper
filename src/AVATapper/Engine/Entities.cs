namespace AVATapper.Engine;

public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public double Length => Math.Sqrt(X * X + Y * Y);
}

/// <summary>Left/right to walk along the bar, up/down to run to another tap, and the pour (held).</summary>
public readonly record struct Controls(int Dx, int Dy, bool Pour = false);

/// <summary>The four counters, one above the other. Customers come in at the left; the taps are on the right.</summary>
public static class Field
{
    public const double Step = 1.0 / 60;
    public const double Width = 1024, Height = 768;
    /// <summary>The bottom of the score bar.</summary>
    public const double Top = 64;
    public const int Bars = 4;
    public const double TapX = 900;
    /// <summary>Where the bartender stands to pour.</summary>
    public const double PourX = TapX - 34;
    public static double CounterTop(int bar) => 220 + bar * 140;
    /// <summary>The left end of each counter: they're staggered, the lower ones shorter.</summary>
    public static double Left(int bar) => 120 + bar * 40;
}

public sealed class Bartender
{
    public const double WalkSpeed = 300;
    public int Bar;
    public double X = Field.PourX;
    /// <summary>How full the mug under the tap is, 0 to 1.</summary>
    public double Fill;
    public bool PourHeld;
    public int Facing = -1;
    public double WalkPhase;
    /// <summary>Running between taps: from which bar, and how far along.</summary>
    public int FromBar;
    public double Switch;
    public bool AtTap => X >= Field.PourX - 2;
}

public enum CustomerState { Approaching, Pushed, Drinking, Leaving, Grabbing }

public sealed class Customer
{
    public int Bar;
    public double X;
    public CustomerState State;
    public double Timer;
    /// <summary>Walking in steps: moving or waiting, and for how long.</summary>
    public double StepTime;
    public bool Stepping;
    /// <summary>How he looks: which of the venue's regulars.</summary>
    public int Look;
    public double Bob;
    public bool Gone;
}

public sealed class Mug
{
    public int Bar;
    public double X;
    /// <summary>Full mugs slide left to the customers; empties slide right, back to the bartender.</summary>
    public bool Full;
    public double Speed;
    public bool Gone;
}

public sealed class Tip
{
    public int Bar;
    public double X;
    public double Age;
}

public enum EffectKind { Score, Smash, Word, Notes }

public sealed class Effect
{
    public EffectKind Kind;
    public double X, Y, Age, Life = 1;
    public string Text = "";
    public uint Colour = 0xFFFFFFFF;
    public bool Done => Age >= Life;
}

/// <summary>The four places the game is set, in turn. Every name and face here is the game's own.</summary>
public sealed record Venue(string Name, string Tagline, uint Wall, uint Trim, uint Counter, uint[] Shirts);

public static class Venues
{
    public static readonly Venue[] All =
    [
        new("THE DUSTY SPUR", "SARSAPARILLA FOR THE TRAIL-WEARY", 0xFF7A4A2A, 0xFF4A2A14, 0xFF8A5A30, [0xFFB04A3A, 0xFF3A6AA0, 0xFFC8A040, 0xFF6A8A3A]),
        new("HALFTIME GRILL", "FIZZ FOR THE FANS", 0xFF2E6A4A, 0xFF1A3A2A, 0xFF9A6A3A, [0xFFD03030, 0xFF2A5AD0, 0xFFF0C030, 0xFFF0F0F0]),
        new("THE NEON CELLAR", "COLA FOR THE LOUD", 0xFF2A2230, 0xFF4A1A4A, 0xFF4A4450, [0xFF202020, 0xFF6A2A8A, 0xFFD02A6A, 0xFF2A8A8A]),
        new("THE ORBIT LOUNGE", "SPACE SODA, ZERO GRAVITY", 0xFF141A3A, 0xFF3A4A8A, 0xFF6A7A9A, [0xFF6AC0E0, 0xFFE0E0F0, 0xFFB07AE0, 0xFF7AE0A0]),
    ];

    public static Venue Of(int level) => All[(level - 1) % All.Length];
}
