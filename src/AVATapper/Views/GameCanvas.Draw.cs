using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVATapper.Engine;

namespace AVATapper.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Dictionary<(uint, double), Pen> RoundCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVATapper/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width);
        return p;
    }

    /// <summary>A thick pen with round ends, for limbs.</summary>
    private static Pen Limb(uint argb, double width)
    {
        if (!RoundCache.TryGetValue((argb, width), out var p))
            RoundCache[(argb, width)] = p = new Pen(Brush(argb), width, lineCap: PenLineCap.Round);
        return p;
    }

    private const uint White = 0xFFF2F2F2, Yellow = 0xFFFFE14A, Red = 0xFFFF4A3A, Dim = 0xFF9AA2B8, Soda = 0xFF6A2A12, Foam = 0xFFF8F0E0, Glass = 0x90C8E8F8;

    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false, bool shadow = true)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        if (shadow) dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(0xB0000000)), new Point(x + 2, at.Y - ft.Height / 2 + 2));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    private static uint Shade(uint argb, double f)
    {
        uint r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
        uint S(uint v) => (uint)Math.Clamp(f >= 1 ? v + (255 - v) * (f - 1) : v * f, 0, 255);
        return (argb & 0xFF000000) | (S(r) << 16) | (S(g) << 8) | S(b);
    }

    private static void Poly(DrawingContext dc, uint fill, uint? edge, params Point[] pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], true);
            foreach (var p in pts.Skip(1)) c.LineTo(p);
            c.EndFigure(true);
        }
        dc.DrawGeometry(fill == 0 ? null : Brush(fill), edge is { } e ? Pen(e, 1.5) : null, g);
    }

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        var v = g.Venue;
        if (g.Phase == Phase.Bonus && g.State is GameState.Playing or GameState.Paused or GameState.Attract)
        {
            DrawWall(dc, v);
            DrawBonus(dc);
        }
        else
        {
            DrawWall(dc, v);
            for (int bar = 0; bar < Field.Bars; bar++) DrawBar(dc, bar, v);
        }
        foreach (var e in g.Effects) DrawEffect(dc, e);
        DrawHud(dc);
        switch (g.State)
        {
            case GameState.Title: DrawTitle(dc); break;
            case GameState.HighScores: DrawHighScores(dc); break;
            case GameState.EnterInitials: DrawInitials(dc); break;
            default: DrawMessages(dc); break;
        }
        if (g.State == GameState.Paused)
        {
            dc.FillRectangle(Brush(0x90000000), new Rect(0, 0, Field.Width, Field.Height));
            Text(dc, "PAUSED", new Point(Field.Width / 2, 340), 80, Yellow, center: true);
            Text(dc, "ANY KEY TO CARRY ON - ESC TO QUIT", new Point(Field.Width / 2, 400), 26, White, center: true);
        }
        if (g.State == GameState.GameOver) Text(dc, "GAME OVER", new Point(Field.Width / 2, 330), 100, Red, center: true);
        if (g.State == GameState.Attract && g.Phase != Phase.LevelStart)
            Text(dc, "DEMONSTRATION - PRESS ENTER TO PLAY", new Point(Field.Width / 2, 88), 28, Yellow, center: true);
    }

    // ───────────────────────────── the venue ─────────────────────────────

    private void DrawWall(DrawingContext dc, Venue v)
    {
        var area = new Rect(0, Field.Top, Field.Width, Field.Height - Field.Top);
        dc.FillRectangle(Brush(v.Wall), area);
        int which = Array.IndexOf(Venues.All, v);
        switch (which)
        {
            case 0:
                // Planks, and the odd wanted poster.
                for (double y = Field.Top; y < Field.Height; y += 28)
                    dc.DrawLine(Pen(Shade(v.Wall, 0.75), 2), new Point(0, y), new Point(Field.Width, y));
                for (int i = 0; i < 40; i++)
                    dc.FillRectangle(Brush(Shade(v.Wall, 0.6)), new Rect(Hash(i, 1, 2) % 1024, Field.Top + Hash(i, 2, 2) % 700, 3, 3));
                break;
            case 1:
                // Stripes, and pennants along the top.
                for (double x = 0; x < Field.Width; x += 64)
                    dc.FillRectangle(Brush(Shade(v.Wall, 1.08)), new Rect(x, Field.Top, 32, Field.Height));
                for (int i = 0; i < 16; i++)
                {
                    double x = 20 + i * 64;
                    uint c = v.Shirts[i % v.Shirts.Length];
                    Poly(dc, c, 0xFF101010, new Point(x, Field.Top + 4), new Point(x + 44, Field.Top + 4), new Point(x + 22, Field.Top + 26));
                }
                break;
            case 2:
                // Bricks, with neon.
                for (int row = 0; row < 26; row++)
                for (int col = 0; col < 18; col++)
                {
                    double x = col * 60 + (row % 2) * 30, y = Field.Top + row * 28;
                    dc.DrawRectangle(Pen(0xFF1A141E, 2), new Rect(x, y, 60, 28));
                }
                break;
            default:
                // Stars through the portholes.
                for (int i = 0; i < 90; i++)
                    dc.FillRectangle(Brush(0xC0FFFFFF), new Rect(Hash(i, 3, 4) % 1024, Field.Top + Hash(i, 4, 4) % 700, 2, 2));
                break;
        }
    }

    private void DrawBar(DrawingContext dc, int bar, Venue v)
    {
        var g = Engine;
        double top = Field.CounterTop(bar), left = Field.Left(bar);
        int which = Array.IndexOf(Venues.All, v);
        // The back wall of this bar: a long mirror with a rail, a sign.
        dc.FillRectangle(Brush(0x30E0F0FF), new Rect(left + 10, top - 78, Field.TapX - left - 40, 62));
        dc.DrawRectangle(Pen(v.Trim, 4), new Rect(left + 10, top - 78, Field.TapX - left - 40, 62));
        for (int k = 0; k < 3; k++)
        {
            double sx = left + 80 + k * 260 + (bar * 70) % 120;
            dc.DrawLine(Pen(0x40FFFFFF, 6), new Point(sx, top - 74), new Point(sx + 26, top - 20));
        }
        if (which == 2 && bar % 2 == 0)
        {
            double glow = 0.75 + 0.25 * Math.Sin(_clock * 3 + bar);
            Text(dc, bar == 0 ? "OPEN LATE" : "LOUD", new Point(Field.TapX - 160, top - 48), 34, Shade(0xFFFF4AC0, glow), center: true, shadow: false);
        }
        // The door the customers come in by.
        double dx = left - 66;
        dc.FillRectangle(Brush(0xFF120E0A), new Rect(dx, top - 106, 60, 136));
        dc.DrawRectangle(Pen(v.Trim, 6), new Rect(dx, top - 106, 60, 136));
        if (which == 0)
        {
            // Swing doors.
            double swing = g.Customers.Any(c => c.Bar == bar && Math.Abs(c.X - left) < 60) ? Math.Sin(_clock * 14) * 6 : 0;
            dc.DrawRectangle(Brush(0xFF9A6A3A), Pen(0xFF3A2A1A, 2), new Rect(dx + 4 + swing, top - 70, 25, 60), 2, 2);
            dc.DrawRectangle(Brush(0xFF9A6A3A), Pen(0xFF3A2A1A, 2), new Rect(dx + 31 - swing, top - 70, 25, 60), 2, 2);
        }
        // Customers, the bartender and the tap stand behind the counter.
        foreach (var c in g.Customers.Where(c => c.Bar == bar)) DrawCustomer(dc, c, v, which);
        if (g.Bartender.Bar == bar) DrawBartender(dc, g.Bartender);
        DrawTap(dc, bar);
        // The counter: its top, its front, a brass rail.
        Poly(dc, Shade(v.Counter, 1.25), 0xFF201810, new Point(left - 4, top - 6), new Point(Field.TapX + 40, top - 6), new Point(Field.TapX + 44, top + 6), new Point(left - 8, top + 6));
        dc.FillRectangle(Brush(v.Counter), new Rect(left - 8, top + 6, Field.TapX + 52 - left, 40));
        for (double x = left + 30; x + 60 < Field.TapX + 40; x += 80)
            dc.DrawRectangle(Pen(Shade(v.Counter, 0.75), 2), new Rect(x, top + 12, 60, 28));
        dc.DrawLine(Pen(0xFFD0A040, 4), new Point(left - 8, top + 52), new Point(Field.TapX + 44, top + 52));
        // On the counter: tips and mugs.
        foreach (var t in g.Tips.Where(t => t.Bar == bar))
        {
            if (t.Age > 7 && (int)(_clock * 8) % 2 == 0) continue;
            for (int k = 0; k < 3; k++) dc.DrawEllipse(Brush(0xFFE8C040), Pen(0xFF7A5A10, 1), new Point(t.X + k * 3, top - 4 - k * 4), 9, 3.5);
        }
        foreach (var m in g.Mugs.Where(m => m.Bar == bar)) DrawMug(dc, m.X, top - 2, m.Full ? 1 : 0, m.Full);
    }

    private void DrawTap(DrawingContext dc, int bar)
    {
        double top = Field.CounterTop(bar), x = Field.TapX;
        dc.DrawRectangle(Brush(0xFFB8C0C8), Pen(0xFF404850, 2), new Rect(x - 6, top - 120, 30, 116), 6, 6);
        dc.FillRectangle(Brush(0xFFE8F0F8), new Rect(x - 2, top - 116, 6, 108));
        dc.DrawRectangle(Brush(0xFFB02A2A), Pen(0xFF401010, 1.5), new Rect(x - 4, top - 104, 26, 22), 3, 3);
        Text(dc, "FIZZ", new Point(x + 9, top - 93), 16, White, center: true, shadow: false);
        // The spout reaching out over the counter, and the handle.
        dc.DrawLine(Limb(0xFF8A9098, 7), new Point(x - 4, top - 70), new Point(x - 26, top - 70));
        dc.DrawLine(Limb(0xFF8A9098, 5), new Point(x - 26, top - 70), new Point(x - 26, top - 60));
        bool pulled = Engine.Bartender.Bar == bar && Engine.Bartender.Fill > 0;
        dc.DrawLine(Limb(0xFF202020, 6), new Point(x - 10, top - 76), pulled ? new Point(x - 30, top - 96) : new Point(x - 10, top - 104));
        if (pulled) dc.DrawLine(Limb(Soda, 3), new Point(x - 26, top - 58), new Point(x - 26, top - 30));
    }

    private static void DrawMug(DrawingContext dc, double x, double bottom, double fill, bool full)
    {
        var body = new Rect(x - 9, bottom - 24, 18, 24);
        if (fill > 0)
        {
            double h = 22 * fill;
            dc.FillRectangle(Brush(Soda), new Rect(body.X + 2, body.Bottom - h, 14, h));
            if (fill >= 1 || full) dc.DrawEllipse(Brush(Foam), null, new Point(x, body.Y + 2), 10, 5);
        }
        dc.DrawRectangle(Brush(Glass), Pen(0xFFE0F0FF, 1.5), body, 2, 2);
        dc.DrawEllipse(null, Pen(0xFFE0F0FF, 2.5), new Point(x + 11, bottom - 12), 5, 7);
    }

    // ───────────────────────────── people ─────────────────────────────

    private void DrawCustomer(DrawingContext dc, Customer c, Venue v, int which)
    {
        double top = Field.CounterTop(c.Bar);
        double bob = Engine.Jukebox > 0 ? Math.Abs(Math.Sin(_clock * 9 + c.Look)) * 8 : c.Stepping && c.State == CustomerState.Approaching ? Math.Abs(Math.Sin(c.Bob * 10)) * 3 : 0;
        double x = c.X, y = top - bob;
        uint shirt = v.Shirts[c.Look % v.Shirts.Length];
        uint[] skins = which == 3 ? [0xFF7AD07A, 0xFF7AB0E0, 0xFFC08AE0, 0xFFE0C060] : [0xFFF0C8A0, 0xFFD8A070, 0xFFA87048, 0xFF6A4428];
        uint skin = skins[(c.Look / 4) % skins.Length];
        int f = c.State is CustomerState.Pushed or CustomerState.Leaving ? -1 : 1;
        double lean = c.State == CustomerState.Pushed ? -10 : c.State == CustomerState.Grabbing ? 12 : 0;
        // Body, shoulders down behind the counter.
        dc.DrawRectangle(Brush(shirt), Pen(0xFF101010, 1.5), new Rect(x - 17 + lean * 0.5, y - 66, 34, 66), 10, 10);
        var neck = new Point(x + lean, y - 64);
        var head = new Point(x + lean, y - 82);
        // Arms: waving for service, holding the mug, drinking, reaching.
        var shoulder = new Point(neck.X + 12 * f, y - 56);
        switch (c.State)
        {
            case CustomerState.Approaching:
            {
                bool bang = (int)(c.Bob * 4 + c.Look) % 4 == 0;
                var hand = bang ? new Point(shoulder.X + 14, y - 4) : new Point(shoulder.X + 10, y - 96);
                dc.DrawLine(Limb(shirt, 8), shoulder, hand);
                dc.DrawEllipse(Brush(skin), Pen(0xFF101010, 1), hand, 5, 5);
                break;
            }
            case CustomerState.Pushed:
            {
                var hand = new Point(x - 6, y - 30);
                dc.DrawLine(Limb(shirt, 8), shoulder, hand);
                DrawMug(dc, hand.X, hand.Y + 14, 1, true);
                break;
            }
            case CustomerState.Drinking:
            {
                var hand = new Point(head.X + 16, head.Y + 8);
                dc.DrawLine(Limb(shirt, 8), shoulder, hand);
                double left = Math.Clamp(c.Timer / 1.3, 0, 1);
                using (dc.PushTransform(Matrix.CreateTranslation(-hand.X, -hand.Y) * Matrix.CreateRotation(-0.7) * Matrix.CreateTranslation(hand.X, hand.Y)))
                    DrawMug(dc, hand.X + 6, hand.Y + 12, left, false);
                break;
            }
            case CustomerState.Grabbing:
            {
                var hand = new Point(x + 44, y - 50);
                dc.DrawLine(Limb(shirt, 8), shoulder, hand);
                dc.DrawEllipse(Brush(skin), Pen(0xFF101010, 1), hand, 6, 6);
                break;
            }
        }
        dc.DrawEllipse(Brush(skin), Pen(0xFF101010, 1.5), head, 15, 16);
        // Face, looking towards the tap (or back at the door).
        dc.DrawEllipse(Brush(0xFF101010), null, new Point(head.X + 6 * f, head.Y - 2), 2, 2.5);
        if (c.State == CustomerState.Approaching && Engine.Jukebox <= 0)
            dc.DrawLine(Pen(0xFF101010, 2), new Point(head.X + 2 * f, head.Y + 8), new Point(head.X + 10 * f, head.Y + 6));
        else
            dc.DrawEllipse(null, Pen(0xFF101010, 1.5), new Point(head.X + 6 * f, head.Y + 7), 3, 2);
        DrawHat(dc, which, c.Look, head, f, skin);
    }

    private static void DrawHat(DrawingContext dc, int which, int look, Point head, int f, uint skin)
    {
        switch (which)
        {
            case 0:
                // Cowboy hats, and moustaches on some.
                uint hat = (look % 3) switch { 0 => 0xFF6A4A2A, 1 => 0xFF2A2A2A, _ => 0xFFD8C8A0 };
                dc.DrawEllipse(Brush(hat), Pen(0xFF101010, 1.2), new Point(head.X, head.Y - 12), 24, 5);
                dc.DrawRectangle(Brush(hat), Pen(0xFF101010, 1.2), new Rect(head.X - 12, head.Y - 30, 24, 18), 6, 6);
                if (look % 2 == 0) dc.DrawLine(Limb(0xFF3A2A1A, 3), new Point(head.X + 1 * f, head.Y + 4), new Point(head.X + 11 * f, head.Y + 4));
                break;
            case 1:
                // Caps, or face paint.
                if (look % 2 == 0)
                {
                    uint cap = (look % 3) switch { 0 => 0xFFD03030, 1 => 0xFF2A5AD0, _ => 0xFFF0C030 };
                    Poly(dc, cap, 0xFF101010, new Point(head.X - 15, head.Y - 6), new Point(head.X - 12, head.Y - 18), new Point(head.X + 12, head.Y - 18), new Point(head.X + 15, head.Y - 6));
                    dc.FillRectangle(Brush(cap), new Rect(head.X + (f > 0 ? 8 : -24), head.Y - 9, 16, 4));
                }
                else
                {
                    dc.FillRectangle(Brush(0xFFD03030), new Rect(head.X - 12, head.Y - 6, 8, 4));
                    dc.FillRectangle(Brush(0xFF2A5AD0), new Rect(head.X + 4, head.Y - 6, 8, 4));
                }
                break;
            case 2:
                // Mohawks and shades.
                uint hair = (look % 3) switch { 0 => 0xFFE02A8A, 1 => 0xFF2AE08A, _ => 0xFFE0E02A };
                for (int k = -2; k <= 2; k++) Poly(dc, hair, null, new Point(head.X + k * 5 - 3, head.Y - 13), new Point(head.X + k * 5 + 3, head.Y - 13), new Point(head.X + k * 5, head.Y - 32 + Math.Abs(k) * 4));
                dc.FillRectangle(Brush(0xFF101010), new Rect(head.X - 11, head.Y - 5, 22, 6));
                break;
            default:
                // Antennae, and a third eye on some.
                dc.DrawLine(Pen(0xFF101010, 2), new Point(head.X - 6, head.Y - 14), new Point(head.X - 12, head.Y - 30));
                dc.DrawLine(Pen(0xFF101010, 2), new Point(head.X + 6, head.Y - 14), new Point(head.X + 12, head.Y - 30));
                dc.DrawEllipse(Brush(Shade(skin, 1.3)), null, new Point(head.X - 12, head.Y - 31), 4, 4);
                dc.DrawEllipse(Brush(Shade(skin, 1.3)), null, new Point(head.X + 12, head.Y - 31), 4, 4);
                if (look % 2 == 1) dc.DrawEllipse(Brush(0xFF101010), null, new Point(head.X, head.Y - 8), 2.5, 2.5);
                break;
        }
    }

    private void DrawBartender(DrawingContext dc, Bartender b)
    {
        double top = Field.CounterTop(b.Bar);
        double x = b.X, y = top;
        double hop = b.Switch > 0 ? Math.Sin(b.Switch * Math.PI) * 10 : 0;
        y -= hop;
        // Shirt, waistcoat, apron.
        dc.DrawRectangle(Brush(0xFFF4F4F0), Pen(0xFF101010, 1.5), new Rect(x - 18, y - 70, 36, 70), 10, 10);
        Poly(dc, 0xFF2A2A3A, null, new Point(x - 18, y - 60), new Point(x - 6, y - 60), new Point(x - 2, y), new Point(x - 18, y));
        Poly(dc, 0xFF2A2A3A, null, new Point(x + 18, y - 60), new Point(x + 6, y - 60), new Point(x + 2, y), new Point(x + 18, y));
        Poly(dc, 0xFFD02A2A, null, new Point(x - 8, y - 70), new Point(x, y - 66), new Point(x - 8, y - 62));
        Poly(dc, 0xFFD02A2A, null, new Point(x + 8, y - 70), new Point(x, y - 66), new Point(x + 8, y - 62));
        var head = new Point(x, y - 88);
        dc.DrawEllipse(Brush(0xFFF0C8A0), Pen(0xFF101010, 1.5), head, 15, 16);
        // Slicked hair with a parting, a handlebar moustache.
        Poly(dc, 0xFF2A1A10, null, new Point(head.X - 15, head.Y - 2), new Point(head.X - 14, head.Y - 14), new Point(head.X, head.Y - 18), new Point(head.X + 14, head.Y - 14), new Point(head.X + 15, head.Y - 2), new Point(head.X + 2, head.Y - 12));
        dc.DrawLine(Limb(0xFF2A1A10, 3), new Point(head.X - 9, head.Y + 6), new Point(head.X + 9, head.Y + 6));
        dc.DrawLine(Limb(0xFF2A1A10, 2), new Point(head.X - 9, head.Y + 6), new Point(head.X - 12, head.Y + 2));
        dc.DrawLine(Limb(0xFF2A1A10, 2), new Point(head.X + 9, head.Y + 6), new Point(head.X + 12, head.Y + 2));
        dc.DrawEllipse(Brush(0xFF101010), null, new Point(head.X - 5 * -b.Facing, head.Y - 3), 2, 2.5);
        // Arms: pouring under the tap, or swinging as he walks.
        var sh = new Point(x - 12, y - 58);
        var sh2 = new Point(x + 12, y - 58);
        if (b.Fill > 0)
        {
            var hand = new Point(Field.TapX - 26, top - 22);
            dc.DrawLine(Limb(0xFFF4F4F0, 8), sh2, hand);
            DrawMug(dc, hand.X, top - 4, b.Fill, false);
            dc.DrawLine(Limb(0xFFF4F4F0, 8), sh, new Point(x - 4, y - 30));
        }
        else
        {
            double s = b.AtTap ? 0 : Math.Sin(b.WalkPhase) * 8;
            dc.DrawLine(Limb(0xFFF4F4F0, 8), sh, new Point(x - 20 + s, y - 22));
            dc.DrawLine(Limb(0xFFF4F4F0, 8), sh2, new Point(x + 20 - s, y - 22));
        }
    }

    // ───────────────────────────── the bonus round ─────────────────────────────

    private const double CanTop = 470;
    private static double SlotX(int slot) => 187 + slot * 130;

    private void DrawBonus(DrawingContext dc)
    {
        var g = Engine;
        Text(dc, "BONUS ROUND", new Point(Field.Width / 2, 112), 58, Yellow, center: true);
        string hint = g.Stage switch
        {
            BonusStage.Show => "THE FIZZ BANDIT IS HERE...",
            BonusStage.Shake => "HE SHAKES FIVE OF THE SIX CANS",
            BonusStage.Shuffle => "KEEP YOUR EYE ON THE ONE HE DIDN'T",
            BonusStage.Choose => "PICK THE UNSHAKEN CAN: LEFT / RIGHT, POUR TO OPEN",
            _ => "",
        };
        Text(dc, hint, new Point(Field.Width / 2, 160), 30, White, center: true);
        // The bandit, behind the counter, shaking a can (or waving as he shuffles).
        double bx = g.Stage == BonusStage.Shake && g.Shaking >= 0 ? SlotX(Array.IndexOf(g.CanAt, g.Shaking)) - 54 : Field.Width / 2;
        if (g.Stage != BonusStage.Reveal && g.Stage != BonusStage.Choose) DrawBandit(dc, bx, CanTop + 40);
        else DrawBartenderAt(dc, SlotX(g.Stage == BonusStage.Reveal ? g.Picked : g.Cursor), CanTop + 40);
        // The counter.
        dc.FillRectangle(Brush(Shade(g.Venue.Counter, 1.25)), new Rect(80, CanTop + 40, 864, 12));
        dc.FillRectangle(Brush(g.Venue.Counter), new Rect(80, CanTop + 52, 864, 60));
        dc.DrawLine(Pen(0xFFD0A040, 4), new Point(80, CanTop + 120), new Point(944, CanTop + 120));
        // The cans: moving if they're being swapped, shaking if he has one.
        for (int slot = 0; slot < GameEngine.Cans; slot++)
        {
            int can = g.CanAt[slot];
            double x = SlotX(slot), lift = 0;
            if (g.Stage == BonusStage.Shuffle && (slot == g.Swap.A || slot == g.Swap.B))
            {
                int other = slot == g.Swap.A ? g.Swap.B : g.Swap.A;
                double t = g.SwapProgress;
                x = SlotX(slot) + (SlotX(other) - SlotX(slot)) * t;
                lift = Math.Sin(Math.PI * t) * (slot == g.Swap.A ? 60 : -20);
            }
            if (g.Stage == BonusStage.Shake && can == g.Shaking)
            {
                x += Math.Sin(_clock * 60) * 6;
                lift = 26 + Math.Cos(_clock * 45) * 8;
            }
            bool open = g.Stage == BonusStage.Reveal && slot == g.Picked;
            DrawCan(dc, x, CanTop + 40 - lift, open);
            if (g.Stage == BonusStage.Choose && slot == g.Cursor)
                Poly(dc, Yellow, 0xFF101010, new Point(x - 14, CanTop - 72), new Point(x + 14, CanTop - 72), new Point(x, CanTop - 52));
        }
        if (g.Stage == BonusStage.Reveal)
        {
            double x = SlotX(g.Picked);
            if (g.PickedRight)
            {
                Text(dc, $"+{GameEngine.BonusPoints}", new Point(x, CanTop - 110), 48, Yellow, center: true);
                dc.DrawLine(Pen(0xFFD02A2A, 4), new Point(x + 4, CanTop - 30), new Point(x + 18, CanTop - 70));
            }
            else
            {
                // The spray, straight up and into his face.
                double t = Math.Min(1, g.StageTime * 2);
                for (int i = 0; i < 26; i++)
                {
                    double a = -Math.PI / 2 + (Hash(i, 1, 7) % 100 / 100.0 - 0.5) * 0.9;
                    double r = (40 + Hash(i, 2, 7) % 140) * t;
                    dc.DrawEllipse(Brush(Foam), null, new Point(x + Math.Cos(a) * r, CanTop - 30 + Math.Sin(a) * r), 6, 6);
                }
            }
        }
    }

    private static void DrawCan(DrawingContext dc, double x, double bottom, bool open)
    {
        var body = new Rect(x - 20, bottom - 70, 40, 70);
        dc.DrawRectangle(Brush(0xFFC02A2A), Pen(0xFF401010, 2), body, 6, 6);
        dc.FillRectangle(Brush(0xFFF0F0F0), new Rect(body.X, body.Y + 24, 40, 18));
        Text(dc, "FIZZ", new Point(x, body.Y + 33), 18, 0xFFC02A2A, center: true, shadow: false);
        dc.DrawEllipse(Brush(0xFFB8C0C8), Pen(0xFF404850, 1.5), new Point(x, body.Y), 20, 5);
        if (open) dc.DrawEllipse(Brush(0xFF202020), null, new Point(x + 4, body.Y), 5, 2);
    }

    private void DrawBandit(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(Brush(0xFF2A2A2A), Pen(0xFF101010, 1.5), new Rect(x - 20, y - 74, 40, 74), 10, 10);
        for (int i = 0; i < 4; i++) dc.FillRectangle(Brush(0xFFF0F0F0), new Rect(x - 20, y - 64 + i * 16, 40, 6));
        var head = new Point(x, y - 92);
        dc.DrawEllipse(Brush(0xFFF0C8A0), Pen(0xFF101010, 1.5), head, 16, 17);
        dc.FillRectangle(Brush(0xFF101010), new Rect(head.X - 16, head.Y - 7, 32, 9));
        dc.DrawEllipse(Brush(White), null, new Point(head.X - 6, head.Y - 3), 3, 2.5);
        dc.DrawEllipse(Brush(White), null, new Point(head.X + 6, head.Y - 3), 3, 2.5);
        dc.DrawEllipse(Brush(0xFF2A2A2A), Pen(0xFF101010, 1), new Point(head.X, head.Y - 16), 17, 7);
        Text(dc, "THE FIZZ BANDIT", new Point(x, y - 130), 20, Dim, center: true);
        double s = Math.Sin(_clock * 30) * 6;
        dc.DrawLine(Limb(0xFF2A2A2A, 8), new Point(x - 14, y - 60), new Point(x - 22 + s, y - 100));
        dc.DrawLine(Limb(0xFF2A2A2A, 8), new Point(x + 14, y - 60), new Point(x + 22 - s, y - 100));
    }

    private void DrawBartenderAt(DrawingContext dc, double x, double y)
    {
        var fake = new Bartender { X = x, Bar = 0 };
        using (dc.PushTransform(Matrix.CreateTranslation(0, y - Field.CounterTop(0))))
            DrawBartender(dc, fake);
    }

    // ───────────────────────────── effects and words ─────────────────────────────

    private void DrawEffect(DrawingContext dc, Engine.Effect e)
    {
        double t = e.Age / e.Life;
        switch (e.Kind)
        {
            case EffectKind.Score:
            case EffectKind.Word:
                Text(dc, e.Text, new Point(e.X, e.Y - t * 30), 26, e.Colour, center: true);
                break;
            case EffectKind.Smash:
                for (int i = 0; i < 10; i++)
                {
                    double a = i * 0.63, r = 10 + Math.Min(1, t * 4) * 40;
                    dc.FillRectangle(Brush(0xC0D8F0FF), new Rect(e.X + Math.Cos(a) * r, e.Y + Math.Min(1, t * 3) * 30 + Math.Sin(a) * r * 0.3, 5, 4));
                }
                dc.DrawEllipse(Brush(0x806A2A12), null, new Point(e.X, e.Y + 36), 30 * Math.Min(1, t * 3), 6);
                break;
            case EffectKind.Notes:
                for (int i = 0; i < 8; i++)
                {
                    double x = 100 + i * 110 + Math.Sin(_clock * 3 + i) * 20, y = 120 + ((e.Age * 60 + i * 37) % 560);
                    dc.DrawEllipse(Brush(0xFFFFE14A), null, new Point(x, y), 8, 6);
                    dc.DrawLine(Pen(0xFFFFE14A, 3), new Point(x + 7, y), new Point(x + 7, y - 28));
                    dc.DrawLine(Pen(0xFFFFE14A, 3), new Point(x + 7, y - 28), new Point(x + 17, y - 20));
                }
                break;
        }
    }

    private void DrawHud(DrawingContext dc)
    {
        var g = Engine;
        dc.FillRectangle(Brush(0xFF100C0A), new Rect(0, 0, Field.Width, Field.Top));
        dc.FillRectangle(Brush(0xFFD0A040), new Rect(0, Field.Top - 3, Field.Width, 3));
        Text(dc, "SCORE", new Point(14, 16), 22, Dim);
        Text(dc, g.Score.ToString("D6"), new Point(14, 42), 36, White);
        Text(dc, "HI", new Point(160, 16), 22, Dim);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString("D6"), new Point(160, 42), 30, Dim);
        Text(dc, $"LEVEL {g.Level}", new Point(Field.Width / 2, 16), 22, Yellow, center: true);
        Text(dc, g.Venue.Name, new Point(Field.Width / 2, 42), 30, White, center: true);
        int left = g.ToCome + g.Customers.Count(c => c.State != CustomerState.Leaving);
        Text(dc, $"CUSTOMERS {left}", new Point(Field.Width - 230, 16), 22, Dim);
        for (int i = 0; i < Math.Min(6, g.Lives); i++) DrawMug(dc, Field.Width - 220 + i * 30, 56, 1, true);
    }

    private void DrawMessages(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        if (g.Phase == Phase.LevelStart && g.State is GameState.Playing or GameState.Attract or GameState.Paused)
        {
            dc.FillRectangle(Brush(0xC0100C0A), new Rect(170, 250, 684, 220));
            dc.DrawRectangle(Pen(0xFFD0A040, 3), new Rect(170, 250, 684, 220));
            Text(dc, $"LEVEL {g.Level}", new Point(cx, 290), 34, Dim, center: true);
            Text(dc, g.Venue.Name, new Point(cx, 350), 66, Yellow, center: true);
            Text(dc, g.Venue.Tagline, new Point(cx, 412), 28, White, center: true);
            return;
        }
        if (g.Message is { } msg) Text(dc, msg, new Point(cx, 112), msg.Length > 22 ? 44 : 56, g.Phase == Phase.Oops ? Red : Yellow, center: true);
        if (g.Jukebox > 0 && g.Phase == Phase.Serving) Text(dc, "JUKEBOX!", new Point(cx, 112), 50, 0xFFFF7AE0, center: true);
    }

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0xB0100C0A), new Rect(0, Field.Top, Field.Width, Field.Height - Field.Top));
        double glow = 0.85 + 0.15 * Math.Sin(_clock * 4);
        Text(dc, "AVA TAPPER", new Point(cx, 170), 130, Shade(0xFFFF5A3A, glow), center: true);
        Text(dc, "AFTER THE 1983 ARCADE CLASSIC - SODA FOUNTAIN EDITION", new Point(cx, 246), 26, Dim, center: true);
        string[] lines =
        [
            "HOLD POUR AT THE TAP, LET GO TO SLIDE THE SODA",
            "UP AND DOWN RUN BETWEEN THE FOUR BARS",
            "CATCH EVERY EMPTY MUG THAT COMES BACK",
            "DON'T LET A MUG FALL OFF THE END",
            "OR A CUSTOMER REACH THE TAP",
            "PICK UP TIPS TO START THE JUKEBOX",
        ];
        for (int i = 0; i < lines.Length; i++) Text(dc, lines[i], new Point(cx, 320 + i * 40), 32, White, center: true);
        DrawMug(dc, cx - 330, 210, 1, true);
        DrawMug(dc, cx + 330, 210, 1, true);
        if ((int)(_clock * 2) % 2 == 0) Text(dc, "PRESS ENTER OR START", new Point(cx, 600), 48, Yellow, center: true);
        Text(dc, "ARROWS: MOVE    Z / SPACE: POUR", new Point(cx, 680), 28, Dim, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0xD0100C0A), new Rect(0, Field.Top, Field.Width, Field.Height - Field.Top));
        Text(dc, "BEST BARTENDERS", new Point(cx, 130), 72, Yellow, center: true);
        Text(dc, "RANK NAME    SCORE   LEVEL", new Point(cx, 196), 30, Dim, center: true);
        var entries = g.HighScores.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool mine = i == g.NewEntryIndex && (int)(_clock * 4) % 2 == 0;
            Text(dc, $"{i + 1,2}.  {e.Initials}  {e.Score,8}   {e.Stage,3}", new Point(cx, 240 + i * 42), 34, mine ? Red : i == 0 ? Yellow : White, center: true);
        }
        Text(dc, "PRESS ENTER TO PLAY", new Point(cx, 700), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0xD0100C0A), new Rect(0, Field.Top, Field.Width, Field.Height - Field.Top));
        Text(dc, "A NEW HIGH SCORE!", new Point(cx, 200), 64, Yellow, center: true);
        Text(dc, g.Score.ToString(), new Point(cx, 270), 52, White, center: true);
        Text(dc, "ENTER YOUR INITIALS", new Point(cx, 350), 34, Dim, center: true);
        if (g.Initials is { } entry)
        {
            string s = entry.Initials;
            for (int i = 0; i < s.Length; i++)
            {
                bool cur = i == entry.Cursor && (int)(_clock * 3) % 2 == 0;
                Text(dc, s[i].ToString(), new Point(cx - 60 + i * 60, 440), 80, cur ? Red : Yellow, center: true);
                dc.FillRectangle(Brush(i == entry.Cursor ? Red : Dim), new Rect(cx - 82 + i * 60, 480, 44, 4));
            }
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE; ENTER WHEN DONE", new Point(cx, 560), 26, White, center: true);
    }
}
