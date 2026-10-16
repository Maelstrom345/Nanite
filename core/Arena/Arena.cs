using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nanite;

internal sealed class Arena
{
    // ---- Identity ----
    public HiveData SourceHive;
    public Vector2 Center = Vector2.Zero;
    public float Radius;
    public float StartingRadius;

    // ---- Contents ----
    public readonly List<Enemy> Enemies = new();
    public readonly List<EnemyProjectile> Projectiles = new();

    // ---- Waves (real wave logic comes in Step 3) ----
    public int CurrentWave = 0;
    public int TotalWaves = 5;
    public float SpawnTimer = 0f;
    public float SpawnInterval = 2.0f;
    public bool WavesComplete = false;

    // ---- Theme ----
    public string Theme = "Mixed";

    private readonly Random _rng = new();

    // ---- Construction ----

    public Arena(HiveData hive, float bladeOuterRadius)
    {
        SourceHive = hive;

        // Doc: arena area = 30x blade area (bigger for crowd combat)
        float bladeArea = MathF.PI * bladeOuterRadius * bladeOuterRadius;
        float arenaArea = bladeArea * 30f;
        Radius = MathF.Sqrt(arenaArea / MathF.PI);
        StartingRadius = Radius;

        Center = Vector2.Zero;
    }

    // ---- Update ----

    public void Update(float delta, Vector2 playerPosition, Polarity bladePolarity)
    {
        if (WavesComplete) return;

        // Trickle spawn for now (Step 3 replaces this with real waves)
        SpawnTimer += delta;
        if (SpawnTimer >= SpawnInterval)
        {
            SpawnTimer = 0f;
            SpawnEnemyFromWall();
        }

        // Movement + AI for enemies
        UpdateEnemyMovement(delta, playerPosition, bladePolarity);

        // Projectiles
        UpdateProjectiles(delta, playerPosition);
    }

    private void UpdateEnemyMovement(float delta, Vector2 playerPosition, Polarity bladePolarity)
    {
        Func<Vector2, int, bool> canOccupy = (pos, size) =>
        {
            float dist = Vector2.Distance(pos, Center);
            return dist + size / 2f < Radius;
        };

        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = Enemies[i];
            Vector2 toPlayer = playerPosition - enemy.Position;
            float distanceToPlayer = toPlayer.Length();

            if (toPlayer.LengthSquared() > 0.01f)
            {
                toPlayer.Normalize();
                Vector2 movement = toPlayer * enemy.Definition.Speed * delta;

                // Horizontal
                Vector2 hPos = new Vector2(enemy.Position.X + movement.X, enemy.Position.Y);
                if (canOccupy(hPos, enemy.Definition.Size))
                    enemy.Position = new Vector2(hPos.X, enemy.Position.Y);

                // Vertical
                Vector2 vPos = new Vector2(enemy.Position.X, enemy.Position.Y + movement.Y);
                if (canOccupy(vPos, enemy.Definition.Size))
                    enemy.Position = new Vector2(enemy.Position.X, vPos.Y);
            }

            // Animation
            const float frameDuration = 0.1f;
            if (enemy.Definition.FrameCount > 0)
            {
                enemy.AnimTimer += delta;
                if (enemy.AnimTimer >= frameDuration)
                {
                    enemy.AnimTimer -= frameDuration;
                    enemy.AnimFrame = (enemy.AnimFrame + 1) % enemy.Definition.FrameCount;
                }
            }
        }
    }

    private void UpdateProjectiles(float delta, Vector2 playerPosition)
    {
        for (int i = Projectiles.Count - 1; i >= 0; i--)
        {
            EnemyProjectile p = Projectiles[i];
            p.Position += p.Velocity * delta;

            Rectangle pRect = new Rectangle(
                (int)p.Position.X - 4, (int)p.Position.Y - 4, 8, 8);
            Rectangle playerRect = new Rectangle(
                (int)playerPosition.X - 16, (int)playerPosition.Y - 16, 32, 32);

            if (pRect.Intersects(playerRect))
            {
                Projectiles.RemoveAt(i);
                continue;
            }

            // Cull if too far from arena center
            if (Vector2.DistanceSquared(p.Position, Center) > (Radius * 3f) * (Radius * 3f))
                Projectiles.RemoveAt(i);
        }
    }

    // ---- Spawning ----

    private void SpawnEnemyFromWall()
    {
        if (EnemyDatabase.AllTypes.Count == 0) return;

        int pick = _rng.Next(EnemyDatabase.AllTypes.Count);
        EnemyDefinition definition = EnemyDatabase.AllTypes[pick];

        float angle = (float)(_rng.NextDouble() * Math.PI * 2);
        float inset = definition.Size / 2f + 40f;
        Vector2 spawnPos = Center + new Vector2(
            MathF.Cos(angle), MathF.Sin(angle)) * (Radius - inset);

        Enemies.Add(new Enemy(spawnPos, definition));
    }

    // ---- Clear ----

    public void Clear()
    {
        Enemies.Clear();
        Projectiles.Clear();
        CurrentWave = 0;
        SpawnTimer = 0f;
        WavesComplete = false;
    }

    // ---- Draw ----

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel,
        Dictionary<string, List<Texture2D>> enemyAnimations)
    {
        // 1. Arena floor
        Rectangle floor = new Rectangle(
            (int)(Center.X - Radius - 500f),
            (int)(Center.Y - Radius - 500f),
            (int)(Radius * 2f + 1000f),
            (int)(Radius * 2f + 1000f));
        spriteBatch.Draw(pixel, floor, new Color(25, 8, 15));

        // 2. Wall ring
        int segments = 128;
        float thickness = 24f;
        Color wallColor = new Color(120, 15, 35);
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * MathF.PI * 2f;
            Vector2 pos = Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * Radius;
            float rot = a + MathF.PI / 2f;
            spriteBatch.Draw(pixel, pos, null, wallColor, rot,
                new Vector2(0.5f, 0.5f),
                new Vector2(thickness, thickness * 1.5f),
                SpriteEffects.None, 0f);
        }

        // 3. Enemies
        foreach (Enemy enemy in Enemies)
        {
            Rectangle enemyRect = new Rectangle(
                (int)enemy.Position.X - enemy.Definition.Size / 2,
                (int)enemy.Position.Y - enemy.Definition.Size / 2,
                enemy.Definition.Size, enemy.Definition.Size);

            if (enemyAnimations.TryGetValue(enemy.Definition.Name, out List<Texture2D> frames))
                spriteBatch.Draw(frames[enemy.AnimFrame], enemyRect, Color.White);
            else
                spriteBatch.Draw(pixel, enemyRect, enemy.Definition.DrawColor);
        }

        // 4. Projectiles
        foreach (EnemyProjectile p in Projectiles)
        {
            Rectangle rect = new Rectangle(
                (int)p.Position.X - 4, (int)p.Position.Y - 4, 8, 8);
            spriteBatch.Draw(pixel, rect, Color.DimGray);
        }
    }
}