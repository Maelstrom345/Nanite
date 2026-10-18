using Microsoft.Xna.Framework;

namespace Nanite;

public class PlayerState
{
    public Vector2 Position;
    public int Size = 32;
    public float Speed = 250f;

    public bool IsDead;
    public bool IsShellBroken;

    public int Armor = 25;
    public int MaxArmor = 50;
    
    // Ammo has been removed, you shoot forever.
}