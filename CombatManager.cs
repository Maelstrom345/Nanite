using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nanite;

internal sealed class CombatManager
{
    private const int RedBugExplosionDamage = 5;
    private const int RedBugExplosionPlayerDamage = 15;
    private const float ExplosionEffectDuration = 0.25f;
    private readonly List<ExplosionEffect> _explosionEffects = new();
public int AmmoCostPerShot {get; set;} =1 ;
    public int TotalKills { get; private set; }
    public float SawAngle { get; private set; }
    public float SawOrbitSpeed{get; set;}=1f;
    public float RedBugExplosionRadius { get; } = 160f;
    public IReadOnlyList<ExplosionEffect> ExplosionEffects => _explosionEffects;

    public void Reset()
    {
        TotalKills = 0;
        SawAngle = 0f;
        _explosionEffects.Clear();
    }

    public void UpdateBuzzsaw(float delta, PlayerState player, EnemyManager enemyManager,
        Func<Vector2, int, bool> canOccupyTerrain, Action<Vector2, float> digTerrain,
        float bladeInnerRadius,
        float bladeOuterRadius, float sawOrbitSpeed, float samePolarityDamageMultiplier,
        Action<string> grantEnemyReward)
    {
        if (player.IsDead) return;

        SawAngle += sawOrbitSpeed * delta;
        if (player.Armor <= player.ArmorBreakThreshold) return;

        digTerrain(player.Position, bladeOuterRadius);
        List<Enemy> enemies = enemyManager.Enemies;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            float distance = Vector2.Distance(enemy.Position, player.Position);
            if (distance < bladeInnerRadius || distance > bladeOuterRadius) continue;

            if (enemy.Definition.Charge == player.BladePolarity)
            {
                int reducedSawDamage = Math.Max(1,
                    (int)Math.Round(enemy.Definition.SawDamage * samePolarityDamageMultiplier));
                player.Armor = Math.Max(0, player.Armor - reducedSawDamage);
                Vector2 pushDirection = enemy.Position - player.Position;
                if (pushDirection.LengthSquared() > 0.01f)
                {
                    pushDirection.Normalize();
                    enemyManager.MoveEnemy(enemy, pushDirection * 60f, canOccupyTerrain);
                }
                if (player.Armor <= 0) player.IsDead = true;
                continue;
            }

            grantEnemyReward(enemy.Definition.RewardType);
            player.Armor = Math.Max(0, player.Armor - enemy.Definition.SawDamage);
            TotalKills++;
            enemies.RemoveAt(i);
        }
    }

    public void HandlePlayerBulletHit(Enemy enemy, int enemyIndex, PlayerState player,
        EnemyManager enemyManager, Action<string> grantEnemyReward)
    {
        enemy.Health /= 5;
        if (enemy.Health > 0) return;

        Vector2 killedEnemyPosition = enemy.Position;
        bool isRedBug = enemy.Definition.Name == "Red Bug";
        grantEnemyReward(enemy.Definition.RewardType);
        enemyManager.Enemies.RemoveAt(enemyIndex);
        TotalKills++;
        if (isRedBug)
        {
            DetonateRedBug(killedEnemyPosition, player, enemyManager, grantEnemyReward);
        }
    }

    public void UpdatePlayerContact(PlayerState player, EnemyManager enemyManager,
        Func<Vector2, int, bool> canOccupyTerrain, float samePolarityDamageMultiplier)
    {
        if (player.IsDead) return;

        Rectangle playerRect = new Rectangle(
            (int)player.Position.X - player.Size / 2,
            (int)player.Position.Y - player.Size / 2,
            player.Size,
            player.Size);

        foreach (Enemy enemy in enemyManager.Enemies)
        {
            Rectangle enemyRect = new Rectangle(
                (int)enemy.Position.X - enemy.Definition.Size / 2,
                (int)enemy.Position.Y - enemy.Definition.Size / 2,
                enemy.Definition.Size,
                enemy.Definition.Size);
            if (!playerRect.Intersects(enemyRect)) continue;

            if (enemy.Definition.Charge == player.BladePolarity)
            {
                int reducedContactDamage = Math.Max(1,
                    (int)Math.Round(enemy.Definition.SawDamage * samePolarityDamageMultiplier));
                player.Armor = Math.Max(0, player.Armor - reducedContactDamage);
                Vector2 repel = enemy.Position - player.Position;
                if (repel.LengthSquared() > 0.01f)
                {
                    repel.Normalize();
                    enemyManager.MoveEnemy(enemy, repel * 30f, canOccupyTerrain);
                }
                if (player.Armor <= 0) player.IsDead = true;
                continue;
            }

            player.Armor = Math.Max(0, player.Armor - enemy.Definition.SawDamage);
            if (player.Armor <= 0) player.IsDead = true;
        }
    }

    public void UpdateExplosionEffects(float delta)
    {
        for (int i = _explosionEffects.Count - 1; i >= 0; i--)
        {
            _explosionEffects[i].Age += delta;
            if (_explosionEffects[i].Age >= ExplosionEffectDuration)
            {
                _explosionEffects.RemoveAt(i);
            }
        }
    }

    public void DrawExplosionEffects(SpriteBatch spriteBatch, Texture2D pixel)
    {
        foreach (ExplosionEffect effect in _explosionEffects)
        {
            float progress = effect.Age / ExplosionEffectDuration;
            int radius = (int)(RedBugExplosionRadius * progress);
            int diameter = radius * 2;
            const int thickness = 5;
            int x = (int)effect.Position.X;
            int y = (int)effect.Position.Y;
            Color effectColor = Color.OrangeRed * (1f - progress);

            spriteBatch.Draw(pixel, new Rectangle(x - radius, y - radius, diameter, thickness), effectColor);
            spriteBatch.Draw(pixel, new Rectangle(x - radius, y + radius - thickness, diameter, thickness), effectColor);
            spriteBatch.Draw(pixel, new Rectangle(x - radius, y - radius, thickness, diameter), effectColor);
            spriteBatch.Draw(pixel, new Rectangle(x + radius - thickness, y - radius, thickness, diameter), effectColor);
        }
    }

    private void DetonateRedBug(Vector2 position, PlayerState player,
        EnemyManager enemyManager, Action<string> grantEnemyReward)
    {
        var pendingExplosions = new Queue<Vector2>();
        pendingExplosions.Enqueue(position);
        float explosionRadiusSquared = RedBugExplosionRadius * RedBugExplosionRadius;
        List<Enemy> enemies = enemyManager.Enemies;

        while (pendingExplosions.Count > 0)
        {
            Vector2 explosionPosition = pendingExplosions.Dequeue();
            _explosionEffects.Add(new ExplosionEffect(explosionPosition));

            if (Vector2.DistanceSquared(player.Position, explosionPosition) <= explosionRadiusSquared)
            {
                player.Armor = Math.Max(0, player.Armor - RedBugExplosionPlayerDamage);
                if (player.Armor <= 0) player.IsDead = true;
            }

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                if (Vector2.DistanceSquared(enemy.Position, explosionPosition) > explosionRadiusSquared)
                    continue;
                enemy.Health -= RedBugExplosionDamage;
                if (enemy.Health > 0) continue;

                bool chainedRedBug = enemy.Definition.Name == "Red Bug";
                Vector2 chainedPosition = enemy.Position;
                grantEnemyReward(enemy.Definition.RewardType);
                enemies.RemoveAt(i);
                TotalKills++;
                if (chainedRedBug) pendingExplosions.Enqueue(chainedPosition);
            }
        }
    }
}