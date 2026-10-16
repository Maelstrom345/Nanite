using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Nanite;


public enum Polarity
{
    Positive,Negative
}
public enum RangeType
{
    Melee,Ranged
}

public class EnemyDefinition
{
    public string Name;
    public Color DrawColor;
    public float Speed;
    public int Size;
    public string RewardType;
    public int SpawnWeight;
    public Polarity Charge;
    public RangeType AttackRange;
    public int SawDamage;
    public int BulletDamage;
    public float PreferredDistance;
    public int FrameCount;

    public EnemyDefinition(string name, Color color, float speed, int size, string rewardType,
        int spawnWeight, Polarity charge, RangeType attackRange = RangeType.Melee,
        int sawDamage = 12, int bulletDamage = 10, float preferredDistance = 60f, int frameCount = 0)
    {
        Name = name;
        DrawColor = color;
        Speed = speed;
        Size = size;
        RewardType = rewardType;
        SpawnWeight = spawnWeight;
        Charge = charge;
        AttackRange = attackRange;
        SawDamage = sawDamage;
        BulletDamage = bulletDamage;
        PreferredDistance = preferredDistance;
        FrameCount = frameCount;
    }
}

public class Enemy
{
    public Vector2 Position;
    public EnemyDefinition Definition;
    public int Health;
    public int AnimFrame = 0;
    public float AnimTimer = 0f;
    public float AttackCooldownTimer = 0f;

    public Enemy(Vector2 position, EnemyDefinition definition)
    {
        Position = position;
        Definition = definition;
        Health = 10;
    }
}

public class ExplosionEffect
{
    public Vector2 Position;
    public float Age;

    public ExplosionEffect(Vector2 position)
    {
        Position = position;
        Age = 0f;
    }
}

public class EnemyProjectile
{
    public Vector2 Position;
    public Vector2 Velocity;
    public int Damage;

    public EnemyProjectile(Vector2 position, Vector2 velocity, int damage)
    {
        Position = position;
        Velocity = velocity;
        Damage = damage;
    }
}

public static class EnemyDatabase
{
    public static List<EnemyDefinition> AllTypes = new List<EnemyDefinition>
    {
        new EnemyDefinition("Red Bug", Color.Red, 150f, 150, "xp", 10,
            Polarity.Negative, RangeType.Melee, 20, 10, 60f, frameCount: 9),
        new EnemyDefinition("Armor Bug", Color.Green, 50f, 150, "armor", 70,
            Polarity.Negative, RangeType.Melee, 14, 12, 50f, frameCount: 8),
        new EnemyDefinition("Yellow Bug", Color.Yellow, 50f, 150, "ammo", 50,
            Polarity.Negative, RangeType.Ranged, 12, 18, 150f, frameCount: 6),
        new EnemyDefinition("Rapidfire Bug", Color.Purple, 50f, 150, "rapidfire", 25,
            Polarity.Negative, RangeType.Ranged, 16, 22, 180f, frameCount: 7)
    };

    public static EnemyDefinition GetRandom(Random rng, int redBugSpawnWeight)
    {
        Span<int> weights = stackalloc int[AllTypes.Count];
        for (int i = 0; i < AllTypes.Count; i++)
        {
            weights[i] = AllTypes[i].Name == "Red Bug" ? redBugSpawnWeight : AllTypes[i].SpawnWeight;
        }
        return SelectWeighted(rng, weights);
    }

    public static EnemyDefinition GetRandomFromHive(Random rng, int redBugSpawnWeight,
        bool dangerousHive, int redBugWeightMultiplier = 1)
    {
        Span<int> weights = stackalloc int[4];
        if (dangerousHive)
        {
            weights[0] = redBugSpawnWeight * 2 * redBugWeightMultiplier;
            weights[1] = 5;
            weights[2] = 65;
            weights[3] = 15;
        }
        else
        {
            weights[0] = 5 * redBugWeightMultiplier;
            weights[1] = 85;
            weights[2] = 12;
            weights[3] = 5;
        }
        return SelectWeighted(rng, weights);
    }

    private static EnemyDefinition SelectWeighted(Random rng, ReadOnlySpan<int> weights)
    {
        int totalWeight = 0;
        for (int i = 0; i < weights.Length; i++) totalWeight += weights[i];

        int roll = rng.Next(totalWeight);
        for (int i = 0; i < weights.Length; i++)
        {
            if (roll < weights[i]) return AllTypes[i];
            roll -= weights[i];
        }
        return AllTypes[0];
    }
}