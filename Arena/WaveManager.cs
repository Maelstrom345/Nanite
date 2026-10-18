using System;

namespace Nanite;

internal sealed class WaveManager
{
    public int CurrentWave { get; private set; } = 0;
    public int TotalWaves { get; } = 5;
    public bool BetweenWaves { get; private set; } = true;
    public bool WavesComplete { get; private set; } = false;
    public float BreakTimer { get; private set; } = 1.0f; // Shorter initial break

    private const float BreakDuration = 2f; // Shorter break between waves
    private const int BaseEnemies = 40;     // Massive wave 1
    private const int GrowthPerWave = 30;   // Wave 5 will have 60 enemies

    public int EnemiesForWave(int wave) => BaseEnemies + (wave - 1) * GrowthPerWave;

    public int Update(float delta, int currentEnemyCount)
    {
        if (WavesComplete) return 0;

        if (BetweenWaves)
        {
            BreakTimer -= delta;
            if (BreakTimer <= 0f)
            {
                BetweenWaves = false;
                CurrentWave++;
                return EnemiesForWave(CurrentWave);
            }
            return 0;
        }

        if (currentEnemyCount == 0)
        {
            if (CurrentWave >= TotalWaves)
            {
                WavesComplete = true;
                return 0;
            }
            BetweenWaves = true;
            BreakTimer = BreakDuration;
        }
        return 0;
    }

    public void Reset()
    {
        CurrentWave = 0;
        BetweenWaves = true;
        WavesComplete = false;
        BreakTimer = 1.0f;
    }
}