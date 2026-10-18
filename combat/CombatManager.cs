using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nanite;

internal sealed class CombatManager
{
    // ---- Blade spin ----
    public float SawAngle { get; private set; }
    public float Spin { get; private set; } = BaseSpin;

    public const float BaseSpin = 1.0f;
    public const float MaxSpin = 5.0f;
    public const float SpinPerKill = 0.15f;
    public const float SpinDecayPerSecond = 0.2f;
    public const float BrakeAmount = 0.5f;
    public const float DamagePerSpinUnit = 0.5f;
private float _contactDamageCooldown = 0f;
    public int AmmoCostPerShot { get; set; } = 1;
    public int TotalKills { get; private set; }

    private readonly List<ExplosionEffect> _explosionEffects = new();
    public IReadOnlyList<ExplosionEffect> ExplosionEffects => _explosionEffects;
    public const float RedBugExplosionRadius = 160f;

    public void Reset()
    {
        TotalKills = 0;
        SawAngle = 0f;
        Spin = BaseSpin;
        _explosionEffects.Clear();
    }

    public void BoostSpinOnKill()
    {
        Spin = MathF.Min(MaxSpin, Spin + SpinPerKill);
        TotalKills++;
    }

    public void ApplyBrake()
    {
        Spin = MathF.Max(BaseSpin, Spin - BrakeAmount);
    }

    public void TickSpin(float delta)
    {
        SawAngle += Spin * delta;
        if (Spin > BaseSpin)
            Spin = MathF.Max(BaseSpin, Spin - SpinDecayPerSecond * delta);

             if (_contactDamageCooldown > 0f) _contactDamageCooldown -= delta;

    }

    public int BladeDamageBonus()
    {
        return (int)MathF.Round((Spin - BaseSpin) * DamagePerSpinUnit);
    }

    public void AddExplosion(Vector2 position)
    {
        _explosionEffects.Add(new ExplosionEffect(position));
    }

    public void UpdateExplosionEffects(float delta)
    {
        for (int i = _explosionEffects.Count - 1; i >= 0; i--)
        {
            _explosionEffects[i].Age += delta;
            if (_explosionEffects[i].Age >= 0.25f)
                _explosionEffects.RemoveAt(i);
        }
    }

    public void DrawExplosionEffects(SpriteBatch sb, Texture2D pixel)
    {
        foreach (ExplosionEffect effect in _explosionEffects)
        {
            float p = effect.Age / 0.25f;
            int radius = (int)(RedBugExplosionRadius * p);
            int d = radius * 2;
            const int t = 5;
            int x = (int)effect.Position.X;
            int y = (int)effect.Position.Y;
            Color c = Color.OrangeRed * (1f - p);
            sb.Draw(pixel, new Rectangle(x - radius, y - radius, d, t), c);
            sb.Draw(pixel, new Rectangle(x - radius, y + radius - t, d, t), c);
            sb.Draw(pixel, new Rectangle(x - radius, y - radius, t, d), c);
            sb.Draw(pixel, new Rectangle(x + radius - t, y - radius, t, d), c);
        }
    }
}