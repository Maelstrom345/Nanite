using Microsoft.Xna.Framework;

namespace Nanite;

internal sealed class PlayerState
{
    public Vector2 Position { get; set; }
    public float Speed { get; set; } = 1000f;
    public int Size { get; set; } = 32;
    public bool IsDead { get; set; }

    public int MaxArmor { get; set; } = 70;
    public int Armor { get; set; } = 70;
    public int ArmorBreakThreshold { get; set; } = 25;

    public int Ammo { get; set; } = 500;
    public int MaxAmmo { get; set; } = 1000;

    public Polarity BladePolarity { get; set; } = Polarity.Positive;
    public float PolarityCharge { get; set; }
    public int MaxPolarityCharge { get; set; } = 100;

    public int Experience { get; set; }
    public int Score { get; set; }

    public float RapidFireCharge { get; set; } = 300f;
    public float MaxRapidFireCharge { get; set; } = 300f;
    public bool IsShellBroken{get;set;} 
}