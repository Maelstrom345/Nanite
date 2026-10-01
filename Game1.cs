using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Nanite;
 public enum Polarity { Positive, Negative }
 public enum RangeType { Melee, Ranged }
public class EnemyDefinition
{
    public string Name;
    public Color DrawColor;
    public float Speed;
    public int Size;
    public string RewardType; // "xp", "armor", "ammo"
    public int SpawnWeight;
    public Polarity Charge; // Positive or Negative
    public RangeType AttackRange;
    public int SawDamage;
    public int BulletDamage;
    public float PreferredDistance;
    public int FrameCount;

    public EnemyDefinition(string name, Color color, float speed, int size, string rewardType, int spawnWeight, Polarity charge, RangeType attackRange = RangeType.Melee, int sawDamage = 12, int bulletDamage = 10, float preferredDistance = 60f,int frameCount = 0)
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
    new EnemyDefinition("Red Bug", Color.Red, 150f, 78, "xp", 10, Polarity.Negative, RangeType.Melee, 20, 10, 60f,frameCount:9),
        new EnemyDefinition("Armor Bug", Color.Green, 50f, 78, "armor", 70, Polarity.Negative, RangeType.Melee, 14, 12, 50f,frameCount:8),
        new EnemyDefinition("Yellow Bug", Color.Yellow, 50f, 78, "ammo", 50, Polarity.Negative, RangeType.Ranged, 12, 18, 150f,frameCount:6),
        new EnemyDefinition("Rapidfire Bug", Color.Purple, 50f, 78, "rapidfire", 25, Polarity.Negative, RangeType.Ranged, 16, 22, 180f,frameCount:7)
    };

    public static EnemyDefinition GetRandom(Random rng, int redBugSpawnWeight)
    {
        int totalWeight = 0;
        foreach (var def in AllTypes)
        {
            totalWeight += def.Name == "Red Bug" ? redBugSpawnWeight : def.SpawnWeight;
        }

        int roll = rng.Next(totalWeight);
        int cumulative = 0;
        foreach (var def in AllTypes)
        {
            cumulative += def.Name == "Red Bug" ? redBugSpawnWeight : def.SpawnWeight;
            if (roll < cumulative) return def;
        }
        return AllTypes[0];
    }
}

public class Game1 : Game
{
    private enum GameState { Menu, Playing }
private GameState _state = GameState.Menu;
private float _menuZoomScale = 6f; // starts zoomed in on the eye
private bool _isZoomingOut = false;
private float _zoomOutSpeed = 8f;
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private SpriteFont _font;

    //animation texture
    private Texture2D _bladeTexture;
private Texture2D _targetTexture;
private Texture2D _eyeSocketTexture;
private Texture2D _eyePupilTexture;
private float _eyeSocketScale = 1.5f;
private float _pupilMaxOffset = 5f; // how far the pupil can drift from center
private Vector2 _pupilOffset = Vector2.Zero;

//killtracker
private int _totalKills = 0;
    // Player
    private Vector2 _playerPosition;
    private float _playerSpeed = 200f;
    private int _playerSize = 32;
    private bool _isDead = false;

    // Buzzsaw shell
  
    private float _sawAngle = 0f;
    private float _sawOrbitSpeed = 3f;

    // Armor — now a threshold state instead of gradual segment loss
    private int _maxArmor = 70;
    private int _armor = 70;
    private int _armorBreakThreshold = 25;

    private bool IsShellBroken() => _armor <= _armorBreakThreshold;

    // Ammo
    private int _ammo = 500;
    private int _maxAmmo = 1000;
    private int _ammoCostPerShot = 1;
    private int _ammoRewardAmount = 100;

    // Polarity pickups
    private List<Vector2> _polarityPickupPositions = new List<Vector2>();
    private float _polarityPickupSize = 14f;
    private float _polarityPickupSpawnTimer = 0f;
    private float _polarityPickupSpawnInterval = 2.5f;

    // Enemies
    private List<Enemy> _enemies = new List<Enemy>();
    private List<EnemyProjectile> _enemyProjectiles = new List<EnemyProjectile>();
    private float _enemyProjectileSpeed = 320f;
    private float _yellowBugShotInterval = 0.45f;
    private int EnemyProjectileSize => _playerSize;
    private Random _rng = new Random();
    private float _enemySpawnTimer = 0f;
    private float _enemySpawnInterval = 0.4f;
    private float _survivalTime = 0f;
    private float _difficultyRampRate = 0.015f;
    private float _minSpawnInterval = 0.05f;
    private float _redBugExplosionRadius = 160f;
    private int _redBugExplosionDamage = 5;
    private int _redBugExplosionPlayerDamage = 15;
    private float _explosionEffectDuration = 0.25f;
    private List<ExplosionEffect> _explosionEffects = new List<ExplosionEffect>();
    private float _sameChargeKeepDistance = 170f;


//Polarity
private Polarity _bladePolarity = Polarity.Positive;
    private float _polarityCharge = 0f;
    private int _maxPolarityCharge = 100;
    private int _polarityChargePerPickup = 25;
    private float _polarityChargeDrainPerSecond = 10f;
    private ButtonState _previousRightMouseButton = ButtonState.Released;
    // XP
    private int _xp = 0;
    private int _score = 0;
    private int _baseRedBugSpawnWeight = 10;
    private int _maxRedBugSpawnWeight = 100;
    private int _redBugSpawnStageCount = 7;
    private int _killsPerRedBugSpawnStage = 10;
    private int _armorRewardAmount = 15;

    // Bullets
    private List<Vector2> _bulletPositions = new List<Vector2>();
    private List<Vector2> _bulletVelocities = new List<Vector2>();
    private float _bulletSpeed = 650f;
    private int _bulletSize = 8;

    // Gun / firing
    private float _gunCooldownTimer = 0f;
    private float _gunCooldownDuration = 0.18f;
    private MouseState _previousMouse;

    // Rapid fire charge
    private float _rapidFireCharge = 300f;
    private float _maxRapidFireCharge = 300f;
    private float _rapidFireChargePerPellet = 75f;
    private float _rapidFireDrainPerShot = 3f;
    private float _rapidFireShotInterval = 0f;
    private float _rapidFireShotTimer = 0f;
    private float _samePolarityDamageMultiplier = 0.35f;
    private List<Vector2> _rapidFirePelletPositions = new List<Vector2>();
    private float _rapidFirePelletSpawnTimer = 0f;
    private float _rapidFirePelletSpawnInterval = 6f;
    private float _armorRegenRate = 2f; // armor per second, tune to taste
private float _armorRegenTimer = 0f;
private Dictionary<string, List<Texture2D>> _enemyAnimations;
    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        DisplayMode displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.PreferredBackBufferWidth = displayMode.Width;
        _graphics.PreferredBackBufferHeight = displayMode.Height;
        _graphics.IsFullScreen = true;
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
    }

    protected override void Initialize()
    {
        _playerPosition = new Vector2(
            _graphics.PreferredBackBufferWidth / 2f,
            _graphics.PreferredBackBufferHeight / 2f);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = Content.Load<SpriteFont>("font");

_eyeSocketTexture = Content.Load<Texture2D>("eye_socket");
_eyePupilTexture = Content.Load<Texture2D>("eye_pupil");
_bladeTexture   = Content.Load<Texture2D>("Blade1_Frame1");
_targetTexture = Content.Load<Texture2D>("Target_icon");

_enemyAnimations = new Dictionary<string, List<Texture2D>>();

var greenFrames = new List<Texture2D>();
for (int i = 1; i <= 8; i++)
    greenFrames.Add(Content.Load<Texture2D>($"Green_bug_frame{i}"));
_enemyAnimations["Armor Bug"] = greenFrames;

var redFrames = new List<Texture2D>();
for (int i = 1; i <= 9; i++)
    redFrames.Add(Content.Load<Texture2D>($"Red_bug_frame{i}"));
_enemyAnimations["Red Bug"] = redFrames;

var purpleFrames = new List<Texture2D>();
for (int i =1; i<= 7; i++)
purpleFrames.Add(Content.Load<Texture2D>($"Purple_bug_frame{i}") );
_enemyAnimations["Rapidfire Bug"] = purpleFrames;
var yellowFrames = new List<Texture2D>();
for (int i = 1; i<=6; i++)
yellowFrames.Add(Content.Load<Texture2D>($"Yellow_bug_frame{i}"));
_enemyAnimations["Yellow Bug"] = yellowFrames;
    }

    protected override void Update(GameTime gameTime)
    {
        
        var keyboard = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();
        if (_state == GameState.Playing && !_isDead && mouse.RightButton == ButtonState.Pressed && _previousRightMouseButton == ButtonState.Released)
        {
            if (_bladePolarity == Polarity.Negative)
            {
                _bladePolarity = Polarity.Positive;
            }
            else if (_polarityCharge > 0f)
            {
                _bladePolarity = Polarity.Negative;
            }
        }
        _previousRightMouseButton = mouse.RightButton;
        if (keyboard.IsKeyDown(Keys.Escape)) Exit();
        

        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
// In Update(), before your existing gameplay logic:
if (_state == GameState.Menu)
{
    if (_isZoomingOut)
    {
        _menuZoomScale = Math.Max(1f, _menuZoomScale - _zoomOutSpeed * delta);
        if (_menuZoomScale <= 1f) _state = GameState.Playing;
    }
    else if (keyboard.IsKeyDown(Keys.Enter))
    {
        _isZoomingOut = true;
    }
    base.Update(gameTime);
    return; // skip gameplay updates while in menu
}
        if (_isDead)
        {
            if (keyboard.IsKeyDown(Keys.R)) RestartGame();
            base.Update(gameTime);
            return;
        }

        if (keyboard.IsKeyDown(Keys.W)) _playerPosition.Y -= _playerSpeed * delta;
        if (keyboard.IsKeyDown(Keys.S)) _playerPosition.Y += _playerSpeed * delta;
        if (keyboard.IsKeyDown(Keys.A)) _playerPosition.X -= _playerSpeed * delta;
        if (keyboard.IsKeyDown(Keys.D)) _playerPosition.X += _playerSpeed * delta;

        float playerHalfSize = _playerSize / 2f;
        _playerPosition.X = Math.Clamp(_playerPosition.X, playerHalfSize, _graphics.PreferredBackBufferWidth - playerHalfSize);
        _playerPosition.Y = Math.Clamp(_playerPosition.Y, playerHalfSize, _graphics.PreferredBackBufferHeight - playerHalfSize);

        UpdatePolarityCharge(delta);
        UpdateEnemies(delta);
        UpdateYellowBugAttacks(delta);
        UpdateEnemyProjectiles(delta);
        UpdateBuzzsaw(delta);
        UpdatePolarityPickups(delta);
        UpdateGun(delta);
        UpdateRapidFirePellets(delta);
        UpdateBullets(delta);
        UpdateExplosionEffects(delta);
        CheckPlayerContact();
        UpdatePupilTrackMouse();

        if (_armor < _maxArmor)
{
    _armorRegenTimer += delta;
    if (_armorRegenTimer >= 1f)
    {
        _armorRegenTimer -= 1f;
        _armor = Math.Min(_maxArmor, _armor + (int)_armorRegenRate);
    }
}

        base.Update(gameTime);
    }

    private void UpdatePupilTrackMouse()
{
    MouseState mouse = Mouse.GetState();
    Vector2 mousePos = new Vector2(mouse.X, mouse.Y);
    Vector2 toMouse = mousePos - _playerPosition;

    if (toMouse.LengthSquared() > 0.01f)
    {
        // Clamp so the pupil never drifts further than the eye socket allows
        float distance = Math.Min(toMouse.Length(), _pupilMaxOffset);
        toMouse.Normalize();
        _pupilOffset = toMouse * distance;
    }
}

    private void GrantEnemyReward(string rewardType)
    {
        switch (rewardType)
        {
            case "xp":
                _xp += 1;
                _score += 1;
                break;
            case "armor":
                _armor = Math.Min(_maxArmor, _armor + _armorRewardAmount);
                break;
            case "ammo":
                _ammo = Math.Min(_maxAmmo, _ammo + _ammoRewardAmount);
                break;
                case "rapidfire":
                _rapidFireCharge = Math.Min(_maxRapidFireCharge, _rapidFireCharge + _rapidFireChargePerPellet);
                break;
        }
    }

    private void RestartGame()
    {
        _playerPosition = new Vector2(_graphics.PreferredBackBufferWidth / 2, _graphics.PreferredBackBufferHeight / 2);
        _armor = _maxArmor;
        _ammo = _maxAmmo;
        _totalKills = 0;
        _xp = 0;
        _score = 0;
        _bladePolarity = Polarity.Positive;
        _polarityCharge = 0f;
        _previousRightMouseButton = ButtonState.Released;
        _enemies.Clear();
        _enemyProjectiles.Clear();
        _explosionEffects.Clear();
        _polarityPickupPositions.Clear();
        _rapidFirePelletPositions.Clear();
        _bulletPositions.Clear();
        _bulletVelocities.Clear();
        _sawAngle = 0f;
        _enemySpawnTimer = 0f;
        _polarityPickupSpawnTimer = 0f;
        _rapidFirePelletSpawnTimer = 0f;
        _gunCooldownTimer = 0f;
        _rapidFireShotTimer = 0f;
        _rapidFireCharge = _maxRapidFireCharge;
        _survivalTime = 0f;
        _isDead = false;
    }

    private void UpdateEnemies(float delta)
    {
        _survivalTime += delta;
        float currentSpawnInterval = Math.Max(_minSpawnInterval, _enemySpawnInterval - (_survivalTime * _difficultyRampRate));

        _enemySpawnTimer += delta;
        if (_enemySpawnTimer >= currentSpawnInterval)
        {
            _enemySpawnTimer = 0f;
            SpawnEnemy();
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            Enemy enemy = _enemies[i];

            Vector2 toPlayer = _playerPosition - enemy.Position;
            float distanceToPlayer = toPlayer.Length();
            bool holdYellowBugPosition = enemy.Definition.Name == "Yellow Bug" &&
                distanceToPlayer <= enemy.Definition.PreferredDistance + 30f;

            bool sameChargeAsBlade = enemy.Definition.Charge == _bladePolarity;
            if (holdYellowBugPosition)
            {
            }
            else if (sameChargeAsBlade)
            {
                const float distanceTolerance = 15f;
                if (distanceToPlayer > _sameChargeKeepDistance + distanceTolerance)
                {
                    if (toPlayer.LengthSquared() > 0.01f)
                    {
                        toPlayer.Normalize();
                        enemy.Position += toPlayer * enemy.Definition.Speed * 0.8f * delta;
                    }
                }
                else if (distanceToPlayer < _sameChargeKeepDistance - distanceTolerance)
                {
                    if (toPlayer.LengthSquared() > 0.01f)
                    {
                        toPlayer.Normalize();
                        enemy.Position -= toPlayer * enemy.Definition.Speed * 1.4f * delta;
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
                        enemy.Position += toPlayer * enemy.Definition.Speed * 0.9f * delta;
                    }
                }
                else if (distanceToPlayer < preferredDistance - 20f)
                {
                    if (toPlayer.LengthSquared() > 0.01f)
                    {
                        toPlayer.Normalize();
                        enemy.Position -= toPlayer * enemy.Definition.Speed * 0.7f * delta;
                    }
                }

                int crossIndex = i % 5;
                int row = i / 5;
                float spread = 52f;
                float crossX = (crossIndex - 2) * spread;
                float crossY = (row - 1) * spread;
                if (crossIndex == 2) crossY = 0f;

                Vector2 desiredFormationPosition = _playerPosition + new Vector2(crossX, crossY);
                Vector2 toFormation = desiredFormationPosition - enemy.Position;
                if (toFormation.LengthSquared() > 0.01f)
                {
                    toFormation.Normalize();
                    enemy.Position += toFormation * 18f * delta;
                }
            }
            else if (!holdYellowBugPosition)
            {
                bool isRed = enemy.Definition.DrawColor == Color.Red;
                int formationRank = 0;
                for (int previous = 0; previous < i; previous++)
                {
                    if (_enemies[previous].Definition.DrawColor == enemy.Definition.DrawColor)
                    {
                        formationRank++;
                    }
                }

                int row = (formationRank + 1) / 2;
                float side = isRed ? -1f : 1f;
                float lane = formationRank % 2 == 1 ? -1f : 1f;
                Vector2 arrowOffset = new Vector2(
                    side * (90f + row * 44f),
                    row == 0 ? 0f : lane * row * 42f);
                Vector2 desiredFormationPosition = _playerPosition + arrowOffset;
                Vector2 toFormation = desiredFormationPosition - enemy.Position;
                if (toFormation.LengthSquared() > 0.01f)
                {
                    toFormation.Normalize();
                    enemy.Position += toFormation * enemy.Definition.Speed * delta;
                }
            }

            if (!holdYellowBugPosition)
            {
                foreach (var other in _enemies)
                {
                    if (other == enemy) continue;

                    Vector2 offset = enemy.Position - other.Position;
                    float dist = offset.Length();
                    if (dist < 90f && dist > 0.1f)
                    {
                        offset /= dist;
                        float sign = (enemy.Definition.Charge == other.Definition.Charge) ? 1f : -1f;
                        enemy.Position += offset * sign * 18f * delta;
                    }
                }
            }
            float frameDuration = 0.1f; // seconds per frame, tune to taste

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

    private void UpdatePolarityCharge(float delta)
    {
        if (_bladePolarity != Polarity.Negative)
        {
            return;
        }

        _polarityCharge = Math.Max(0f, _polarityCharge - _polarityChargeDrainPerSecond * delta);
        if (_polarityCharge <= 0f)
        {
            _bladePolarity = Polarity.Positive;
        }
    }

    private void UpdateYellowBugAttacks(float delta)
    {
        foreach (Enemy enemy in _enemies)
        {
            if (enemy.Definition.Name != "Yellow Bug")
            {
                continue;
            }

            enemy.AttackCooldownTimer = Math.Max(0f, enemy.AttackCooldownTimer - delta);
            float attackRange = enemy.Definition.PreferredDistance + 30f;
            Vector2 direction = _playerPosition - enemy.Position;
            if (direction.LengthSquared() > attackRange * attackRange || enemy.AttackCooldownTimer > 0f)
            {
                continue;
            }

            if (direction.LengthSquared() <= 0.01f)
            {
                continue;
            }

            direction.Normalize();
            _enemyProjectiles.Add(new EnemyProjectile(
                enemy.Position,
                direction * _enemyProjectileSpeed,
                enemy.Definition.BulletDamage));
            enemy.AttackCooldownTimer = _yellowBugShotInterval;
        }
    }

    private void UpdateEnemyProjectiles(float delta)
    {
        Rectangle playerRect = new Rectangle(
            (int)_playerPosition.X - _playerSize / 2,
            (int)_playerPosition.Y - _playerSize / 2,
            _playerSize,
            _playerSize);

        for (int i = _enemyProjectiles.Count - 1; i >= 0; i--)
        {
            EnemyProjectile projectile = _enemyProjectiles[i];
            projectile.Position += projectile.Velocity * delta;

            Rectangle projectileRect = new Rectangle(
                (int)projectile.Position.X - EnemyProjectileSize / 2,
                (int)projectile.Position.Y - EnemyProjectileSize / 2,
                EnemyProjectileSize,
                EnemyProjectileSize);

            if (projectileRect.Intersects(playerRect))
            {
                _armor = Math.Max(0, _armor - projectile.Damage);
                if (_armor <= 0)
                {
                    _isDead = true;
                }

                _enemyProjectiles.RemoveAt(i);
                continue;
            }

            if (projectile.Position.X < -50 || projectile.Position.X > _graphics.PreferredBackBufferWidth + 50 ||
                projectile.Position.Y < -50 || projectile.Position.Y > _graphics.PreferredBackBufferHeight + 50)
            {
                _enemyProjectiles.RemoveAt(i);
            }
        }
    }

    private void SpawnEnemy()
    {
        int screenW = _graphics.PreferredBackBufferWidth;
        int screenH = _graphics.PreferredBackBufferHeight;
        int edge = _rng.Next(4);

        Vector2 spawnPos = edge switch
        {
            0 => new Vector2(_rng.Next(0, screenW), -40),
            1 => new Vector2(_rng.Next(0, screenW), screenH + 40),
            2 => new Vector2(-40, _rng.Next(0, screenH)),
            _ => new Vector2(screenW + 40, _rng.Next(0, screenH))
        };

        int redBugSpawnWeight = GetCurrentRedBugSpawnWeight();
        EnemyDefinition def = EnemyDatabase.GetRandom(_rng, redBugSpawnWeight);
        _enemies.Add(new Enemy(spawnPos, def));
    }

    private int GetCurrentRedBugSpawnWeight()
    {
        int stage = GetCurrentRedBugSpawnStage();
        return _baseRedBugSpawnWeight +
            (stage - 1) * (_maxRedBugSpawnWeight - _baseRedBugSpawnWeight) / (_redBugSpawnStageCount - 1);
    }

    private int GetCurrentRedBugSpawnStage()
    {
        return Math.Min(_redBugSpawnStageCount, _totalKills / _killsPerRedBugSpawnStage + 1);
    }

    private float GetRedBugSpawnChancePercent()
    {
        int redBugSpawnWeight = GetCurrentRedBugSpawnWeight();
        int totalSpawnWeight = redBugSpawnWeight;

        foreach (var definition in EnemyDatabase.AllTypes)
        {
            if (definition.Name != "Red Bug")
            {
                totalSpawnWeight += definition.SpawnWeight;
            }
        }

        return (float)redBugSpawnWeight / totalSpawnWeight * 100f;
    }

private float _bladeInnerRadius = 60f; // enemies inside this are past the blade, touching the player
private float _bladeOuterRadius = 90f; // enemies outside this haven't reached the blade yet

private void UpdateBuzzsaw(float delta)
{
    if (_isDead) return;

    _sawAngle += _sawOrbitSpeed * delta;

    if (IsShellBroken()) return; // blade inactive, enemies pass straight through

    for (int i = _enemies.Count - 1; i >= 0; i--)
    {
        Enemy enemy = _enemies[i];
        float dist = Vector2.Distance(enemy.Position, _playerPosition);

        if (dist >= _bladeInnerRadius && dist <= _bladeOuterRadius)
        {
            bool sameCharge = enemy.Definition.Charge == _bladePolarity;

            if (sameCharge)
            {
                // Same polarity still hurts, but much less than the opposite charge.
                int reducedSawDamage = Math.Max(1, (int)Math.Round(enemy.Definition.SawDamage * _samePolarityDamageMultiplier));
                _armor = Math.Max(0, _armor - reducedSawDamage);
                Vector2 pushDir = enemy.Position - _playerPosition;
                if (pushDir.LengthSquared() > 0.01f)
                {
                    pushDir.Normalize();
                    enemy.Position += pushDir * 60f;
                }

                if (_armor <= 0)
                {
                    _isDead = true;
                }
                continue;
            }

            // Opposite polarity: valid hit, but damage scales by enemy type and range profile.
            int sawDamage = enemy.Definition.SawDamage;
            GrantEnemyReward(enemy.Definition.RewardType);
            _armor = Math.Max(0, _armor - sawDamage);
            _totalKills++;
            _enemies.RemoveAt(i);
        }
    }
}

    private void UpdatePolarityPickups(float delta)
    {
        if (_isDead) return;

        _polarityPickupSpawnTimer += delta;
        if (_polarityPickupSpawnTimer >= _polarityPickupSpawnInterval)
        {
            _polarityPickupSpawnTimer = 0f;
            int screenW = _graphics.PreferredBackBufferWidth;
            int screenH = _graphics.PreferredBackBufferHeight;
            _polarityPickupPositions.Add(new Vector2(_rng.Next(50, screenW - 50), _rng.Next(50, screenH - 50)));
        }

        for (int i = _polarityPickupPositions.Count - 1; i >= 0; i--)
        {
            if (IsTouchingBlade(_polarityPickupPositions[i], _polarityPickupSize / 2f))
            {
                _polarityCharge = Math.Min(_maxPolarityCharge, _polarityCharge + _polarityChargePerPickup);
                _polarityPickupPositions.RemoveAt(i);
            }
        }
    }

    private void UpdateGun(float delta)
    {
        MouseState mouse = Mouse.GetState();

        if (_gunCooldownTimer > 0)
            _gunCooldownTimer -= delta;

        bool holdingFire = mouse.LeftButton == ButtonState.Pressed;
        bool hasCharge = _rapidFireCharge > 0;

        if (holdingFire && hasCharge)
        {
            _rapidFireShotTimer -= delta;
            if (_rapidFireShotTimer <= 0 && _ammo > 0)
            {
                Shoot(mouse);
                _rapidFireCharge = Math.Max(0, _rapidFireCharge - _rapidFireDrainPerShot);
                _rapidFireShotTimer = _rapidFireShotInterval;
            }
        }
        else
        {
            bool justClicked = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
            if (justClicked && _gunCooldownTimer <= 0)
            {
                Shoot(mouse);
                _gunCooldownTimer = _gunCooldownDuration;
            }
        }

        _previousMouse = mouse;
    }

    private void UpdateRapidFirePellets(float delta)
    {
        if (_isDead) return;

        _rapidFirePelletSpawnTimer += delta;
        if (_rapidFirePelletSpawnTimer >= _rapidFirePelletSpawnInterval)
        {
            _rapidFirePelletSpawnTimer = 0f;
            int screenW = _graphics.PreferredBackBufferWidth;
            int screenH = _graphics.PreferredBackBufferHeight;
            _rapidFirePelletPositions.Add(new Vector2(_rng.Next(50, screenW - 50), _rng.Next(50, screenH - 50)));
        }

        for (int i = _rapidFirePelletPositions.Count - 1; i >= 0; i--)
        {
            if (IsTouchingBlade(_rapidFirePelletPositions[i], 7f))
            {
                _rapidFireCharge = Math.Min(_maxRapidFireCharge, _rapidFireCharge + _rapidFireChargePerPellet);
                _rapidFirePelletPositions.RemoveAt(i);
            }
        }
    }

    private bool IsTouchingBlade(Vector2 pickupPosition, float pickupRadius)
    {
        if (IsShellBroken())
        {
            return false;
        }

        float distance = Vector2.Distance(_playerPosition, pickupPosition);
        return distance + pickupRadius >= _bladeInnerRadius &&
            distance - pickupRadius <= _bladeOuterRadius;
    }

    private void Shoot(MouseState mouse)
    {
        if (_ammo <= 0) return;
        _ammo -= _ammoCostPerShot;

        Vector2 mousePosition = new Vector2(mouse.X, mouse.Y);
        Vector2 direction = mousePosition - _playerPosition;
        if (direction.LengthSquared() > 0.01f)
            direction.Normalize();

        _bulletPositions.Add(_playerPosition);
        _bulletVelocities.Add(direction * _bulletSpeed);
    }

    private void UpdateBullets(float delta)
    {
        if (_isDead) return;

        for (int i = _bulletPositions.Count - 1; i >= 0; i--)
        {
            _bulletPositions[i] += _bulletVelocities[i] * delta;
            Rectangle bulletRect = new Rectangle(
                (int)_bulletPositions[i].X - _bulletSize / 2,
                (int)_bulletPositions[i].Y - _bulletSize / 2,
                _bulletSize, _bulletSize);

            bool bulletHit = false;
            for (int j = _enemies.Count - 1; j >= 0; j--)
            {
                Enemy enemy = _enemies[j];
                Rectangle enemyRect = new Rectangle(
                    (int)enemy.Position.X - enemy.Definition.Size / 2,
                    (int)enemy.Position.Y - enemy.Definition.Size / 2,
                    enemy.Definition.Size, enemy.Definition.Size);

                if (bulletRect.Intersects(enemyRect))
                {
                    enemy.Health /= 5;
                    if (enemy.Health <= 0)
                    {
                        Vector2 killedEnemyPosition = enemy.Position;
                        bool isRedBug = enemy.Definition.Name == "Red Bug";
                        GrantEnemyReward(enemy.Definition.RewardType);
                        _enemies.RemoveAt(j);
                        _totalKills++;
                        if (isRedBug)
                        {
                            DetonateRedBug(killedEnemyPosition);
                        }
                    }

                    bulletHit = true;
                    break;
                }
            }

            if (!bulletHit)
            {
                for (int j = _enemyProjectiles.Count - 1; j >= 0; j--)
                {
                    EnemyProjectile projectile = _enemyProjectiles[j];
                    Rectangle projectileRect = new Rectangle(
                        (int)projectile.Position.X - EnemyProjectileSize / 2,
                        (int)projectile.Position.Y - EnemyProjectileSize / 2,
                        EnemyProjectileSize,
                        EnemyProjectileSize);

                    if (bulletRect.Intersects(projectileRect))
                    {
                        _enemyProjectiles.RemoveAt(j);
                        bulletHit = true;
                        break;
                    }
                }
            }

            if (bulletHit)
            {
                _bulletPositions.RemoveAt(i);
                _bulletVelocities.RemoveAt(i);
                continue;
            }

            int screenW = _graphics.PreferredBackBufferWidth;
            int screenH = _graphics.PreferredBackBufferHeight;
            if (_bulletPositions[i].X < -50 || _bulletPositions[i].X > screenW + 50 ||
                _bulletPositions[i].Y < -50 || _bulletPositions[i].Y > screenH + 50)
            {
                _bulletPositions.RemoveAt(i);
                _bulletVelocities.RemoveAt(i);
            }
            
        }
        
    }

    private void DetonateRedBug(Vector2 position)
    {
        Queue<Vector2> pendingExplosions = new Queue<Vector2>();
        pendingExplosions.Enqueue(position);
        float explosionRadiusSquared = _redBugExplosionRadius * _redBugExplosionRadius;

        while (pendingExplosions.Count > 0)
        {
            Vector2 explosionPosition = pendingExplosions.Dequeue();
            _explosionEffects.Add(new ExplosionEffect(explosionPosition));

            if (Vector2.DistanceSquared(_playerPosition, explosionPosition) <= explosionRadiusSquared)
            {
                _armor = Math.Max(0, _armor - _redBugExplosionPlayerDamage);
                if (_armor <= 0)
                {
                    _isDead = true;
                }
            }

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = _enemies[i];
                if (Vector2.DistanceSquared(enemy.Position, explosionPosition) > explosionRadiusSquared)
                {
                    continue;
                }

                enemy.Health -= _redBugExplosionDamage;
                if (enemy.Health > 0)
                {
                    continue;
                }

                bool chainedRedBug = enemy.Definition.Name == "Red Bug";
                Vector2 chainedPosition = enemy.Position;
                GrantEnemyReward(enemy.Definition.RewardType);
                _enemies.RemoveAt(i);
                _totalKills++;

                if (chainedRedBug)
                {
                    pendingExplosions.Enqueue(chainedPosition);
                }
            }
        }
    }

    private void UpdateExplosionEffects(float delta)
    {
        for (int i = _explosionEffects.Count - 1; i >= 0; i--)
        {
            _explosionEffects[i].Age += delta;
            if (_explosionEffects[i].Age >= _explosionEffectDuration)
            {
                _explosionEffects.RemoveAt(i);
            }
        }
    }

    private void CheckPlayerContact()
    {
        if (_isDead) return;

        Rectangle playerRect = new Rectangle((int)_playerPosition.X - _playerSize / 2, (int)_playerPosition.Y - _playerSize / 2, _playerSize, _playerSize);

        foreach (var enemy in _enemies)
        {
            Rectangle enemyRect = new Rectangle(
                (int)enemy.Position.X - enemy.Definition.Size / 2,
                (int)enemy.Position.Y - enemy.Definition.Size / 2,
                enemy.Definition.Size, enemy.Definition.Size);
            if (playerRect.Intersects(enemyRect))
            {
                if (enemy.Definition.Charge == _bladePolarity)
                {
                    int reducedContactDamage = Math.Max(1, (int)Math.Round(enemy.Definition.SawDamage * _samePolarityDamageMultiplier));
                    _armor = Math.Max(0, _armor - reducedContactDamage);
                    Vector2 repel = enemy.Position - _playerPosition;
                    if (repel.LengthSquared() > 0.01f)
                    {
                        repel.Normalize();
                        enemy.Position += repel * 30f;
                    }

                    if (_armor <= 0)
                    {
                        _isDead = true;
                    }
                    continue;
                }

                int meleeDamage = enemy.Definition.SawDamage;
                _armor = Math.Max(0, _armor - meleeDamage);
                if (_armor <= 0)
                {
                    _isDead = true;
                }
            }
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _spriteBatch.Begin();

// In Draw(), for the menu state:
if (_state == GameState.Menu)
{
    Vector2 menuCenter = new Vector2(_graphics.PreferredBackBufferWidth / 2f, _graphics.PreferredBackBufferHeight / 2f);

    Vector2 menuSocketOrigin = new Vector2(_eyeSocketTexture.Width / 2f - 24.5f, _eyeSocketTexture.Height / 2f - 68.5f);
    float menuSocketScale = _menuZoomScale * _eyeSocketScale * _playerSize / (float)_eyeSocketTexture.Width;
    _spriteBatch.Draw(_eyeSocketTexture, menuCenter, null, Color.White, 0f, menuSocketOrigin, menuSocketScale, SpriteEffects.None, 0f);

    float animationTime = (float)gameTime.TotalGameTime.TotalSeconds;
    Vector2 menuPupilOffset = new Vector2(
        MathF.Cos(animationTime),
        MathF.Sin(animationTime * 0.7f)) * (_pupilMaxOffset * _menuZoomScale * 0.5f);

    Vector2 menuPupilOrigin = new Vector2(_eyePupilTexture.Width / 2f - 27f, _eyePupilTexture.Height / 2f - 55f);
    float menuPupilScale = _menuZoomScale * _playerSize / (float)_eyeSocketTexture.Width;
    _spriteBatch.Draw(_eyePupilTexture, menuCenter + menuPupilOffset, null, Color.White, 0f, menuPupilOrigin, menuPupilScale, SpriteEffects.None, 0f);

    if (!_isZoomingOut)
    {
        string prompt = "Press ENTER";
        Vector2 size = _font.MeasureString(prompt);
        _spriteBatch.DrawString(_font, prompt, new Vector2(menuCenter.X - size.X / 2, menuCenter.Y + 150), Color.White);
    }
    _spriteBatch.End();
    base.Draw(gameTime);
    return;
}

       foreach (var enemy in _enemies)
{
    Rectangle enemyRect = new Rectangle(
        (int)enemy.Position.X - enemy.Definition.Size / 2,
        (int)enemy.Position.Y - enemy.Definition.Size / 2,
        enemy.Definition.Size, enemy.Definition.Size);

    if (_enemyAnimations.TryGetValue(enemy.Definition.Name, out List<Texture2D> frames))
    {
        _spriteBatch.Draw(frames[enemy.AnimFrame], enemyRect, Color.White);
    }
    else
    {
        _spriteBatch.Draw(_pixel, enemyRect, enemy.Definition.DrawColor);
    }
}

        foreach (EnemyProjectile projectile in _enemyProjectiles)
        {
            Rectangle projectileRect = new Rectangle(
                (int)projectile.Position.X - EnemyProjectileSize / 2,
                (int)projectile.Position.Y - EnemyProjectileSize / 2,
                EnemyProjectileSize,
                EnemyProjectileSize);
            _spriteBatch.Draw(_pixel, projectileRect, Color.DimGray);
        }

        foreach (var effect in _explosionEffects)
        {
            float progress = effect.Age / _explosionEffectDuration;
            int radius = (int)(_redBugExplosionRadius * progress);
            int diameter = radius * 2;
            int thickness = 5;
            int x = (int)effect.Position.X;
            int y = (int)effect.Position.Y;
            Color effectColor = Color.OrangeRed * (1f - progress);

            _spriteBatch.Draw(_pixel, new Rectangle(x - radius, y - radius, diameter, thickness), effectColor);
            _spriteBatch.Draw(_pixel, new Rectangle(x - radius, y + radius - thickness, diameter, thickness), effectColor);
            _spriteBatch.Draw(_pixel, new Rectangle(x - radius, y - radius, thickness, diameter), effectColor);
            _spriteBatch.Draw(_pixel, new Rectangle(x + radius - thickness, y - radius, thickness, diameter), effectColor);
        }

        // Buzzsaw shell — color reflects broken/intact state
       // Blade — rotates as one piece, tinted red when broken
Color bladeTint = IsShellBroken() ? Color.DarkRed : Color.White;
Rectangle bladeRect = new Rectangle((int)_playerPosition.X, (int)_playerPosition.Y, (int)(_bladeOuterRadius * 2), (int)(_bladeOuterRadius * 2));
Vector2 bladeOrigin = new Vector2(_bladeTexture.Width / 2f, _bladeTexture.Height / 2f);
_spriteBatch.Draw(_bladeTexture, _playerPosition, null, bladeTint, _sawAngle, bladeOrigin, (_bladeOuterRadius * 2) / _bladeTexture.Width, SpriteEffects.None, 0f);

// Eye — the player's actual visual, centered
// Socket/iris — stays fixed at player center
Vector2 socketOrigin = new Vector2(_eyeSocketTexture.Width / 2f - 24.5f, _eyeSocketTexture.Height / 2f - 68.5f);
_spriteBatch.Draw(_eyeSocketTexture, _playerPosition, null, Color.White, 0f, socketOrigin, _eyeSocketScale * _playerSize / (float)_eyeSocketTexture.Width, SpriteEffects.None, 0f);

// Pupil — drawn at player center PLUS the tracked offset
Vector2 pupilOrigin = new Vector2(_eyePupilTexture.Width / 2f - 27f, _eyePupilTexture.Height / 2f - 55f);
Vector2 pupilDrawPosition = _playerPosition + _pupilOffset;
float pupilScale = _playerSize / (float)_eyeSocketTexture.Width; // match the socket's scale so proportions stay consistent
_spriteBatch.Draw(_eyePupilTexture, pupilDrawPosition, null, Color.White, 0f, pupilOrigin, pupilScale, SpriteEffects.None, 0f);
        foreach (var pickupPos in _polarityPickupPositions)
        {
            Rectangle pickupRect = new Rectangle(
                (int)pickupPos.X - (int)(_polarityPickupSize / 2),
                (int)pickupPos.Y - (int)(_polarityPickupSize / 2),
                (int)_polarityPickupSize,
                (int)_polarityPickupSize);
            _spriteBatch.Draw(_pixel, pickupRect, Color.Cyan);
        }

        foreach (var pos in _rapidFirePelletPositions)
        {
            Rectangle rect = new Rectangle((int)pos.X - 7, (int)pos.Y - 7, 14, 14);
            _spriteBatch.Draw(_pixel, rect, Color.Purple);
        }

        foreach (var bulletPos in _bulletPositions)
        {
            Rectangle bulletRect = new Rectangle((int)bulletPos.X - _bulletSize / 2, (int)bulletPos.Y - _bulletSize / 2, _bulletSize, _bulletSize);
            _spriteBatch.Draw(_pixel, bulletRect, Color.Yellow);
        }

        // HUD
        _spriteBatch.DrawString(_font, "Score: " + _score + "  XP: " + _xp, new Vector2(10, 10), Color.White);

        int barWidth = 200;
        int barHeight = 20;
        Rectangle barBackground = new Rectangle(10, 40, barWidth, barHeight);
        _spriteBatch.Draw(_pixel, barBackground, Color.Gray);
        int filledWidth = (int)((float)_armor / _maxArmor * barWidth);
        Rectangle barFill = new Rectangle(10, 40, filledWidth, barHeight);
        _spriteBatch.Draw(_pixel, barFill, IsShellBroken() ? Color.DarkRed : Color.LimeGreen);

        Rectangle ammoBarBackground = new Rectangle(10, 70, barWidth, 14);
        _spriteBatch.Draw(_pixel, ammoBarBackground, Color.DarkGray);
        int ammoFilledWidth = (int)((float)_ammo / _maxAmmo * barWidth);
        Rectangle ammoBarFill = new Rectangle(10, 70, ammoFilledWidth, 14);
        _spriteBatch.Draw(_pixel, ammoBarFill, Color.Orange);

        Rectangle chargeBarBackground = new Rectangle(10, 90, barWidth, 14);
        _spriteBatch.Draw(_pixel, chargeBarBackground, Color.DarkGray);
        int chargeFilledWidth = (int)(_rapidFireCharge / _maxRapidFireCharge * barWidth);
        Rectangle chargeBarFill = new Rectangle(10, 90, chargeFilledWidth, 14);
        _spriteBatch.Draw(_pixel, chargeBarFill, Color.Purple);

        if (_isDead)
        {
            Rectangle overlay = new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
            _spriteBatch.Draw(_pixel, overlay, Color.Black * 0.7f);

            string deathText = "YOU DIED";
            Vector2 deathSize = _font.MeasureString(deathText) * 2f;
            _spriteBatch.DrawString(_font, deathText,
                new Vector2(_graphics.PreferredBackBufferWidth / 2 - deathSize.X / 2, _graphics.PreferredBackBufferHeight / 2 - deathSize.Y / 2),
                Color.Red, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);

            string restartText = "Press R to restart";
            Vector2 restartSize = _font.MeasureString(restartText);
            _spriteBatch.DrawString(_font, restartText,
                new Vector2(_graphics.PreferredBackBufferWidth / 2 - restartSize.X / 2, _graphics.PreferredBackBufferHeight / 2 + 40),
                Color.White);
        }
_spriteBatch.DrawString(_font, "Kills: " + _totalKills, new Vector2(10, 120), Color.White);
_spriteBatch.DrawString(_font, "Red spawn stage: " + GetCurrentRedBugSpawnStage() + "/" + _redBugSpawnStageCount + " (" + GetRedBugSpawnChancePercent().ToString("F0") + "%)", new Vector2(10, 145), Color.Red);
string bladeChargeText = _bladePolarity == Polarity.Positive ? "Positive" : "Negative";
Color bladeChargeColor = _bladePolarity == Polarity.Positive ? Color.Cyan : Color.OrangeRed;
_spriteBatch.DrawString(_font, "Blade: " + bladeChargeText + "  Charge: " + _polarityCharge.ToString("F0") + "/" + _maxPolarityCharge, new Vector2(10, 170), bladeChargeColor);
Rectangle polarityMeterBackground = new Rectangle(10, 194, 200, 16);
_spriteBatch.Draw(_pixel, polarityMeterBackground, Color.DarkGray);
int polarityMeterWidth = (int)((float)_polarityCharge / _maxPolarityCharge * polarityMeterBackground.Width);
Rectangle polarityMeterFill = new Rectangle(10, 194, polarityMeterWidth, polarityMeterBackground.Height);
_spriteBatch.Draw(_pixel, polarityMeterFill, bladeChargeColor);
_spriteBatch.DrawString(_font, "Right-click: switch  Charge drain: " + _polarityChargeDrainPerSecond.ToString("F0") + "/s", new Vector2(10, 214), Color.White);
       
    MouseState mouseState = Mouse.GetState();
Vector2 mousePos = new Vector2(mouseState.X, mouseState.Y);
int targetSize = 96;
Rectangle targetRect = new Rectangle((int)mousePos.X - targetSize / 2, (int)mousePos.Y - targetSize / 2, targetSize, targetSize);
_spriteBatch.Draw(_targetTexture, targetRect, Color.White);
        _spriteBatch.End();
        base.Draw(gameTime);
    }
}