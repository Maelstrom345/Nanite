using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nanite;

internal sealed class ProjectileManager
{
    private readonly List<EnemyProjectile> _enemyProjectiles = new();

    /// <summary>Read-only view of active enemy projectiles.</summary>
    public IReadOnlyList<EnemyProjectile> EnemyProjectiles => _enemyProjectiles;

    /// <summary>Used by EnemyManager (yellow bug attacks) to add a projectile.</summary>
    public void TryAddEnemyProjectile(EnemyProjectile projectile)
    {
        _enemyProjectiles.Add(projectile);
    }

    /// <summary>Used by Game1 bullets to remove a projectile that was hit.</summary>
    public void RemoveEnemyProjectile(EnemyProjectile projectile)
    {
        _enemyProjectiles.Remove(projectile);
    }

    /// <summary>Clears everything (used when entering/exiting a hive).</summary>
    public void Reset()
    {
        _enemyProjectiles.Clear();
    }

    /// <summary>
    /// Moves enemy projectiles each frame, checks player collision, and removes
    /// any that traveled beyond <paramref name="maxDistance"/>.
    /// </summary>
    public void UpdateEnemyProjectiles(
        float delta,
        Vector2 playerPosition,
        int playerSize,
        float maxDistance)
    {
        float maxDistSq = maxDistance * maxDistance;

        Rectangle playerRect = new Rectangle(
            (int)playerPosition.X - playerSize / 2,
            (int)playerPosition.Y - playerSize / 2,
            playerSize, playerSize);

        for (int i = _enemyProjectiles.Count - 1; i >= 0; i--)
        {
            EnemyProjectile projectile = _enemyProjectiles[i];
            projectile.Position += projectile.Velocity * delta;

            Rectangle projectileRect = new Rectangle(
                (int)projectile.Position.X - playerSize / 2,
                (int)projectile.Position.Y - playerSize / 2,
                playerSize, playerSize);

            if (projectileRect.Intersects(playerRect))
            {
                _enemyProjectiles.RemoveAt(i);
                continue;
            }

            if (Vector2.DistanceSquared(projectile.Position, playerPosition) > maxDistSq)
            {
                _enemyProjectiles.RemoveAt(i);
            }
        }
    }

    /// <summary>Draws all enemy projectiles.</summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        foreach (EnemyProjectile projectile in _enemyProjectiles)
        {
            Rectangle rect = new Rectangle(
                (int)projectile.Position.X - 4,
                (int)projectile.Position.Y - 4,
                8, 8);
            spriteBatch.Draw(pixel, rect, Color.DimGray);
        }
    }
}