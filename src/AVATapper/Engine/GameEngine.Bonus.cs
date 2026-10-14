namespace AVATapper.Engine;

public enum BonusStage { Show, Shake, Shuffle, Choose, Reveal }

/// <summary>
/// The bonus round between levels: the Fizz Bandit shakes five of six soda cans, shuffles them
/// round, and you pick the one he didn't shake. Pick right and it's 3,000 points; pick wrong and it
/// sprays in your face.
/// </summary>
public sealed partial class GameEngine
{
    public const int Cans = 6;
    public const int BonusPoints = 3000;

    public BonusStage Stage { get; private set; }
    public double StageTime { get; private set; }
    /// <summary>Which can is in each place on the counter.</summary>
    public int[] CanAt { get; } = new int[Cans];
    /// <summary>The can he didn't shake.</summary>
    public int Good { get; private set; }
    /// <summary>The can he's shaking now, or -1.</summary>
    public int Shaking { get; private set; } = -1;
    /// <summary>The two places swapping now, and how far through the swap.</summary>
    public (int A, int B) Swap { get; private set; } = (-1, -1);
    public double SwapProgress { get; private set; }
    public int Cursor { get; private set; }
    public int Picked { get; private set; } = -1;
    public bool PickedRight => Picked >= 0 && CanAt[Picked] == Good;

    private int _shaken, _swapsLeft;
    private bool _pickPrev;
    private int _dxPrev;

    public double SwapSeconds => Math.Max(0.2, 0.5 - Level * 0.03);

    private void StartBonus()
    {
        for (int i = 0; i < Cans; i++) CanAt[i] = i;
        Good = _rng.Next(Cans);
        Shaking = -1;
        _shaken = 0;
        _swapsLeft = Math.Min(14, 5 + Level);
        Swap = (-1, -1);
        Cursor = Cans / 2;
        Picked = -1;
        _pickPrev = true;
        SetPhase(Phase.Bonus);
        SetStage(BonusStage.Show);
    }

    private void SetStage(BonusStage s)
    {
        Stage = s;
        StageTime = 0;
    }

    private void Bonus(double dt, Controls c)
    {
        StageTime += dt;
        switch (Stage)
        {
            case BonusStage.Show:
                if (StageTime > 1.4) { SetStage(BonusStage.Shake); NextShake(); }
                break;
            case BonusStage.Shake:
                if (StageTime > 0.55)
                {
                    StageTime = 0;
                    if (_shaken >= Cans - 1) { Shaking = -1; SetStage(BonusStage.Shuffle); NextSwap(); }
                    else NextShake();
                }
                break;
            case BonusStage.Shuffle:
                SwapProgress = Math.Min(1, StageTime / SwapSeconds);
                if (SwapProgress >= 1)
                {
                    var (a, b) = Swap;
                    (CanAt[a], CanAt[b]) = (CanAt[b], CanAt[a]);
                    if (--_swapsLeft <= 0) { Swap = (-1, -1); SetStage(BonusStage.Choose); }
                    else { StageTime = 0; NextSwap(); }
                }
                break;
            case BonusStage.Choose:
                if (c.Dx != 0 && _dxPrev == 0) Cursor = Math.Clamp(Cursor + c.Dx, 0, Cans - 1);
                if (c.Pour && !_pickPrev || StageTime > 10)
                {
                    Picked = Cursor;
                    SetStage(BonusStage.Reveal);
                    if (PickedRight)
                    {
                        AddScore(BonusPoints, 180 + Picked * 130, 400);
                        Play(Sfx.Correct);
                        Say("THE FIZZ BANDIT IS FOILED!", 2.5);
                    }
                    else
                    {
                        Play(Sfx.Fizz);
                        Say("SPLOOSH!", 2.5);
                    }
                }
                break;
            case BonusStage.Reveal:
                if (StageTime > 2.8) StartLevel(Level + 1);
                break;
        }
        _pickPrev = c.Pour;
        _dxPrev = c.Dx;
    }

    private void NextShake()
    {
        // The next can along that isn't the good one.
        int can = Enumerable.Range(0, Cans).Where(i => i != Good).ElementAt(_shaken);
        Shaking = can;
        _shaken++;
        Play(Sfx.Shake);
    }

    private void NextSwap()
    {
        int a = _rng.Next(Cans), b = (a + 1 + _rng.Next(Cans - 1)) % Cans;
        Swap = (Math.Min(a, b), Math.Max(a, b));
        SwapProgress = 0;
        Play(Sfx.Swap);
    }

    /// <summary>Test hook: straight to the bonus round.</summary>
    internal void BonusNow() => StartBonus();
}
