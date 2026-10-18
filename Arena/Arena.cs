using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nanite;

internal sealed class Arena
{
    public HiveData SourceHive;
    public Vector2 Center = Vector2.Zero;
    public float Radius;
    public string Theme = "Mixed";

    public readonly List<Enemy> Enemies = new();
    public readonly List<EnemyProjectile> Projectiles = new();
    public readonly WaveManager Waves = new();

    public Action<string> OnEnemyKilledReward;
    public Action<Vector2> OnRedBugExplosion;

    public const float RedBugExplosionRadius = 160f;
    public const int RedBugExplosionPlayerDamage = 15;

    private float _brakeCooldown = 0f;
    private const float BrakeCooldownDuration = 0.5f;
    private readonly Random _rng = new();

       public Arena(HiveData hive, float bladeOuterRadius)
    {
        SourceHive = hive;
        Theme = hive.Theme;
        float bladeArea = MathF.PI * bladeOuterRadius * bladeOuterRadius;
        float arenaArea = bladeArea * 200f; // <--- 200x bigger, open space
        Radius = MathF.Sqrt(arenaArea / MathF.PI);
        Center = Vector2.Zero;
    }
    public void Update(float delta, Vector2 playerPosition, CombatManager combat)
    {
        if (_brakeCooldown > 0f) _brakeCooldown -= delta;

        int spawnCount = Waves.Update(delta, Enemies.Count);
        for (int i = 0; i < spawnCount; i++) SpawnEnemyFromWall();

        UpdateEnemyMovement(delta, playerPosition);
        UpdateProjectiles(delta, playerPosition);
    }

         public int ApplyBladeSweep(Vector2 playerPosition, float innerRadius, float outerRadius, CombatManager combat)
    {
        int kills = 0;
        
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            if (i >= Enemies.Count) continue;

            Enemy e = Enemies[i];
            Vector2 toEnemy = e.Position - playerPosition;
            float d = toEnemy.Length();
            
            if (d < innerRadius || d > outerRadius) continue;

            // Bounce logic: push the enemy away from the player
            if (toEnemy.LengthSquared() > 0.01f)
            {
                toEnemy.Normalize();
                Vector2 knockback = toEnemy * 15f; // Push force
                Vector2 nextPos = e.Position + knockback;
                
                // Ensure they don't get pushed through the outer arena wall
                if (Vector2.Distance(nextPos, Center) < Radius - e.Definition.Size)
                    e.Position = nextPos;
            }

            // Do damage
            e.Health -= 1 + combat.BladeDamageBonus();
            
            if (e.Health <= 0)
            {
                Vector2 pos = e.Position;
                string reward = e.Definition.RewardType;
                bool isRed = e.Definition.Name == "Red Bug";
                
                Enemies.RemoveAt(i);
                kills++;

                if (isRed)
                {
                    DetonateRedBug(pos, combat);
                    if (_brakeCooldown <= 0f)
                    {
                        combat.ApplyBrake();
                        _brakeCooldown = BrakeCooldownDuration;
                    }
                }
                else
                {
                    combat.BoostSpinOnKill();
                    OnEnemyKilledReward?.Invoke(reward);
                }
            }
        }
        return kills;
    }

  
            public int ApplyInstakillSweep(Vector2 playerPosition, float innerRadius, float outerRadius, CombatManager combat)
    {
        int kills = 0;
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            // ADD THIS LINE: Protects against chain reactions wiping the list
            if (i >= Enemies.Count) continue; 

            Enemy e = Enemies[i];
            float d = Vector2.Distance(e.Position, playerPosition);
            
            if (d >= innerRadius && d <= outerRadius)
            {
                bool isRed = e.Definition.Name == "Red Bug";
                Vector2 pos = e.Position;
                string reward = e.Definition.RewardType;
                
                Enemies.RemoveAt(i);
                kills++;

                if (isRed)
                {
                    DetonateRedBug(pos, combat);
                }
                else
                {
                    combat.BoostSpinOnKill();
                    OnEnemyKilledReward?.Invoke(reward);
                }
            }
        }
        return kills;
    }
       private void DetonateRedBug(Vector2 position, CombatManager combat)
    {
        combat.AddExplosion(position);
        OnRedBugExplosion?.Invoke(position);

        float r2 = RedBugExplosionRadius * RedBugExplosionRadius;
        
        // Loop backwards so removing items doesn't mess up the index
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            // SAFETY CHECK: Make sure the index still exists
            if (i >= Enemies.Count) continue; 

            Enemy e = Enemies[i];
            if (Vector2.DistanceSquared(e.Position, position) > r2) continue;
            
            e.Health -= 5;
            if (e.Health > 0) continue;

            bool chained = e.Definition.Name == "Red Bug";
            Vector2 chainedPos = e.Position;
            string reward = e.Definition.RewardType;
            
            // Remove the enemy BEFORE triggering the chain explosion
            Enemies.RemoveAt(i);

            if (chained) 
            {
                DetonateRedBug(chainedPos, combat); 
            }
            else 
            {
                OnEnemyKilledReward?.Invoke(reward);
            }
        }
    }
    private void UpdateEnemyMovement(float delta, Vector2 playerPosition)
    {
        Func<Vector2, int, bool> canOccupy = (pos, size) =>
        {
            float dist = Vector2.Distance(pos, Center);
            return dist + size / 2f < Radius;
        };

        for (int i = 0; i < Enemies.Count; i++)
        {
            Enemy enemy = Enemies[i];
            Vector2 toPlayer = playerPosition - enemy.Position;
            if (toPlayer.LengthSquared() > 0.01f)
            {
                toPlayer.Normalize();
                Vector2 movement = toPlayer * enemy.Definition.Speed * delta;
                Vector2 hPos = new Vector2(enemy.Position.X + movement.X, enemy.Position.Y);
                if (canOccupy(hPos, enemy.Definition.Size))
                    enemy.Position = new Vector2(hPos.X, enemy.Position.Y);
                Vector2 vPos = new Vector2(enemy.Position.X, enemy.Position.Y + movement.Y);
                if (canOccupy(vPos, enemy.Definition.Size))
                    enemy.Position = new Vector2(enemy.Position.X, vPos.Y);
            }

            const float fd = 0.1f;
            if (enemy.Definition.FrameCount > 0)
            {
                enemy.AnimTimer += delta;
                if (enemy.AnimTimer >= fd)
                {
                    enemy.AnimTimer -= fd;
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
            Rectangle pRect = new Rectangle((int)p.Position.X - 4, (int)p.Position.Y - 4, 8, 8);
            Rectangle pr = new Rectangle((int)playerPosition.X - 16, (int)playerPosition.Y - 16, 32, 32);
            if (pRect.Intersects(pr) ||
                Vector2.DistanceSquared(p.Position, Center) > (Radius * 2f) * (Radius * 2f))
                Projectiles.RemoveAt(i);
        }
    }

    private void SpawnEnemyFromWall()
    {
        if (EnemyDatabase.AllTypes.Count == 0) return;
        EnemyDefinition def = PickEnemyForWave();
        
        // Spawn random point on a circle slightly inside the wall
        float angle = (float)(_rng.NextDouble() * Math.PI * 2);
        float spawnRadius = Radius - def.Size / 2f - 10f;
        Vector2 spawnPos = Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spawnRadius;
        
        Enemies.Add(new Enemy(spawnPos, def));
    }

    private EnemyDefinition PickEnemyForWave()
    {
        int roll = _rng.Next(100);
        if (roll < 45) return FindByName("Red Bug");
        if (roll < 90) return FindByName("Armor Bug");
        if (roll < 95) return FindByName("Yellow Bug");
        return FindByName("Rapidfire Bug");
    }

    private EnemyDefinition FindByName(string name)
    {
        foreach (var d in EnemyDatabase.AllTypes)
            if (d.Name == name) return d;
        return EnemyDatabase.AllTypes[0];
    }

    public void Draw(SpriteBatch sb, Texture2D pixel,
        Dictionary<string, List<Texture2D>> anims)
    {
        Rectangle floor = new Rectangle(
            (int)(Center.X - Radius - 500f),
            (int)(Center.Y - Radius - 500f),
            (int)(Radius * 2f + 1000f),
            (int)(Radius * 2f + 1000f));
        sb.Draw(pixel, floor, new Color(25, 8, 15));

        int segments = 128;
        float thickness = 24f;
        Color wallColor = new Color(120, 15, 35);
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * MathF.PI * 2f;
            Vector2 pos = Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * Radius;
            float rot = a + MathF.PI / 2f;
            sb.Draw(pixel, pos, null, wallColor, rot,
                new Vector2(0.5f, 0.5f),
                new Vector2(thickness, thickness * 1.5f),
                SpriteEffects.None, 0f);
        }

        foreach (Enemy enemy in Enemies)
        {
            Rectangle r = new Rectangle(
                (int)enemy.Position.X - enemy.Definition.Size / 2,
                (int)enemy.Position.Y - enemy.Definition.Size / 2,
                enemy.Definition.Size, enemy.Definition.Size);
            if (anims.TryGetValue(enemy.Definition.Name, out var frames))
                sb.Draw(frames[enemy.AnimFrame], r, Color.White);
            else
                sb.Draw(pixel, r, enemy.Definition.DrawColor);
        }

        foreach (EnemyProjectile p in Projectiles)
        {
            Rectangle r = new Rectangle((int)p.Position.X - 4, (int)p.Position.Y - 4, 8, 8);
            sb.Draw(pixel, r, Color.DimGray);
        }
    }
}