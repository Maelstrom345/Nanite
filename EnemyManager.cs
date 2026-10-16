using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Nanite;

internal sealed class EnemyManager
{
    public readonly List<Enemy> _enemies = new();
    private float _enemySpawnTimer;
    private float _survivalTime;

    private const float EnemySpawnInterval = 0.4f;
    private const float DifficultyRampRate = 0.015f;
    private const float MinSpawnInterval = 0.05f;
    private const float SameChargeKeepDistance = 170f;
    private const int BaseRedBugSpawnWeight = 10;
    private const int MaxRedBugSpawnWeight = 100;
    public const int RedBugSpawnStageCount = 7;
    private const int KillsPerRedBugSpawnStage = 10;

    public List<Enemy> Enemies => _enemies;

    public void Reset()
    {
        _enemies.Clear();
        _enemySpawnTimer = 0f;
        _survivalTime = 0f;
    }

    // ---- Movement overloads ----

    public void MoveEnemy(Enemy enemy, Vector2 movement, WorldManager worldManager)
    {
        MoveEnemy(enemy, movement, worldManager.CanOccupyTerrain);
    }

    public void MoveEnemy(Enemy enemy, Vector2 movement, Func<Vector2, int, bool> canOccupyTerrain)
    {
        if (movement.LengthSquared() <= 0.0001f) return;

        Vector2 currentPosition = enemy.Position;

        Vector2 horizontalPosition = new Vector2(
            currentPosition.X + movement.X,
            currentPosition.Y);
        if (canOccupyTerrain(horizontalPosition, enemy.Definition.Size))
            currentPosition.X = horizontalPosition.X;

        Vector2 verticalPosition = new Vector2(
            currentPosition.X,
            currentPosition.Y + movement.Y);
        if (canOccupyTerrain(verticalPosition, enemy.Definition.Size))
            currentPosition.Y = verticalPosition.Y;

        enemy.Position = currentPosition;
    }

    // ---- Kill-stage helpers (used by HUD) ----

    public int GetCurrentRedBugSpawnStage(int totalKills)
    {
        return Math.Min(RedBugSpawnStageCount,
            totalKills / KillsPerRedBugSpawnStage + 1);
    }

    public float GetRedBugSpawnChancePercent(int totalKills)
    {
        int redBugSpawnWeight = GetCurrentRedBugSpawnWeight(totalKills);
        int totalSpawnWeight = redBugSpawnWeight;
        foreach (EnemyDefinition definition in EnemyDatabase.AllTypes)
        {
            if (definition.Name != "Red Bug")
                totalSpawnWeight += definition.SpawnWeight;
        }
        return (float)redBugSpawnWeight / totalSpawnWeight * 100f;
    }

    // ---- Main update ----

    /// <summary>
    /// Updates all enemies. During digging, enemies use world collision.
    /// During hive combat, pass a custom <paramref name="terrainCollision"/>
    /// (e.g. arena-wall-only collision) so nothing leaks from the overworld.
    /// </summary>
    public void UpdateEnemies(
        float delta,
        Vector2 playerPosition,
        Polarity bladePolarity,
        int totalKills,
        Random random,
        WorldManager worldManager,
        bool spawnFromOverworld = false,
        Func<Vector2, int, bool> terrainCollision = null)
    {
        Func<Vector2, int, bool> canOccupyTerrain =
            terrainCollision ?? worldManager.CanOccupyTerrain;

        _survivalTime += delta;

        // --- Spawning (overworld only) ---
        if (spawnFromOverworld)
        {
            float currentSpawnInterval = Math.Max(MinSpawnInterval,
                EnemySpawnInterval - (_survivalTime * DifficultyRampRate));

            _enemySpawnTimer += delta;
            if (_enemySpawnTimer >= currentSpawnInterval)
            {
                _enemySpawnTimer = 0f;
                SpawnEnemy(totalKills, random, worldManager);
            }
        }

        // --- Movement / AI ---
        for (int enemyIndex = 0; enemyIndex < _enemies.Count; enemyIndex++)
        {
            Enemy enemy = _enemies[enemyIndex];
            Vector2 toPlayer = playerPosition - enemy.Position;
            float distanceToPlayer = toPlayer.Length();
            bool holdYellowBugPosition = enemy.Definition.Name == "Yellow Bug" &&
                distanceToPlayer <= enemy.Definition.PreferredDistance + 30f;

            bool sameChargeAsBlade = enemy.Definition.Charge == bladePolarity;

            if (holdYellowBugPosition)
            {
                // holds position
            }
            else if (sameChargeAsBlade)
            {
                const float distanceTolerance = 15f;
                if (distanceToPlayer > SameChargeKeepDistance + distanceTolerance)
                {
                    if (toPlayer.LengthSquared() > 0.01f)
                    {
                        toPlayer.Normalize();
                        MoveEnemy(enemy, toPlayer * enemy.Definition.Speed * 0.8f * delta, canOccupyTerrain);
                    }
                }
                else if (distanceToPlayer < SameChargeKeepDistance - distanceTolerance)
                {
                    if (toPlayer.LengthSquared() > 0.01f)
                    {
                        toPlayer.Normalize();
                        MoveEnemy(enemy, -toPlayer * enemy.Definition.Speed * 1.4f * delta, canOccupyTerrain);
                    }
                }
            }
            else if (enemy.Definition.AttackRange == RangeType.Ranged)
            {
                float preferredDistance = enemy.Definition.PreferredDistance;
                if (distanceToPlayer > preferredDistance + 20f)
                {
                    if (toPlayer.LengthSquared() > 0.01f)
                    {
                        toPlayer.Normalize();
                        MoveEnemy(enemy, toPlayer * enemy.Definition.Speed * 0.9f * delta, canOccupyTerrain);
                    }
                }
                else if (distanceToPlayer < preferredDistance - 20f)
                {
                    if (toPlayer.LengthSquared() > 0.01f)
                    {
                        toPlayer.Normalize();
                        MoveEnemy(enemy, -toPlayer * enemy.Definition.Speed * 0.7f * delta, canOccupyTerrain);
                    }
                }

                // Formation offset
                int crossIndex = enemyIndex % 5;
                int row = enemyIndex / 5;
                float spread = 52f;
                float crossX = (crossIndex - 2) * spread;
                float crossY = (row - 1) * spread;
                if (crossIndex == 2) crossY = 0f;

                Vector2 desiredFormationPosition =
                    playerPosition + new Vector2(crossX, crossY);
                Vector2 toFormation = desiredFormationPosition - enemy.Position;
                if (toFormation.LengthSquared() > 0.01f)
                {
                    toFormation.Normalize();
                    MoveEnemy(enemy, toFormation * 18f * delta, canOccupyTerrain);
                }
            }
            else // melee, opposite polarity
            {
                bool isRed = enemy.Definition.DrawColor == Color.Red;
                int formationRank = 0;
                for (int previousIndex = 0; previousIndex < enemyIndex; previousIndex++)
                {
                    if (_enemies[previousIndex].Definition.DrawColor == enemy.Definition.DrawColor)
                        formationRank++;
                }

                int row = (formationRank + 1) / 2;
                float side = isRed ? -1f : 1f;
                float lane = formationRank % 2 == 1 ? -1f : 1f;
                Vector2 arrowOffset = new Vector2(
                    side * (90f + row * 44f),
                    row == 0 ? 0f : lane * row * 42f);
                Vector2 desiredFormationPosition = playerPosition + arrowOffset;
                Vector2 toFormation = desiredFormationPosition - enemy.Position;
                if (toFormation.LengthSquared() > 0.01f)
                {
                    toFormation.Normalize();
                    MoveEnemy(enemy, toFormation * enemy.Definition.Speed * delta, canOccupyTerrain);
                }
            }

            // Enemy-enemy separation
            if (!holdYellowBugPosition)
            {
                foreach (Enemy other in _enemies)
                {
                    if (other == enemy) continue;
                    Vector2 offset = enemy.Position - other.Position;
                    float distance = offset.Length();
                    if (distance < 90f && distance > 0.1f)
                    {
                        offset /= distance;
                        float sign = enemy.Definition.Charge == other.Definition.Charge ? 1f : -1f;
                        MoveEnemy(enemy, offset * sign * 18f * delta, canOccupyTerrain);
                    }
                }
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

    public void UpdateYellowBugAttacks(float delta, Vector2 playerPosition,
        ProjectileManager projectileManager, float projectileSpeed, float shotInterval)
    {
        foreach (Enemy enemy in _enemies)
        {
            if (enemy.Definition.Name != "Yellow Bug") continue;

            enemy.AttackCooldownTimer = Math.Max(0f, enemy.AttackCooldownTimer - delta);
            float attackRange = enemy.Definition.PreferredDistance + 30f;
            Vector2 direction = playerPosition - enemy.Position;
            if (direction.LengthSquared() > attackRange * attackRange ||
                enemy.AttackCooldownTimer > 0f)
                continue;
            if (direction.LengthSquared() <= 0.01f) continue;

            direction.Normalize();
            projectileManager.TryAddEnemyProjectile(new EnemyProjectile(
                enemy.Position,
                direction * projectileSpeed,
                enemy.Definition.BulletDamage));
            enemy.AttackCooldownTimer = shotInterval;
        }
    }

    // ---- Internal ----

    private void SpawnEnemy(int totalKills, Random random, WorldManager worldManager)
    {
        bool dangerousHive = worldManager.DangerousHiveLocations.Count > 0 &&
            (worldManager.NormalHiveLocations.Count == 0 || random.Next(3) == 0);
        IReadOnlyList<Vector2> hiveLocations = dangerousHive
            ? worldManager.DangerousHiveLocations
            : worldManager.NormalHiveLocations;
        if (hiveLocations.Count == 0) return;

        Vector2 hiveLocation = hiveLocations[random.Next(hiveLocations.Count)];
        int redBugSpawnWeight = GetCurrentRedBugSpawnWeight(totalKills);

        EnemyDefinition definition =
            EnemyDatabase.GetRandomFromHive(random, redBugSpawnWeight, dangerousHive);
        _enemies.Add(new Enemy(hiveLocation, definition));
    }

    private int GetCurrentRedBugSpawnWeight(int totalKills)
    {
        int stage = GetCurrentRedBugSpawnStage(totalKills);
        return BaseRedBugSpawnWeight +
            (stage - 1) * (MaxRedBugSpawnWeight - BaseRedBugSpawnWeight) /
            (RedBugSpawnStageCount - 1);
    }
}