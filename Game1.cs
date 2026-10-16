using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Linq;

namespace Nanite;

public class Game1 : Game
{
    private enum GameState
    {
        Menu,
        Digging,
        HiveCombat,
        Dead
    }

    // ---- Core ----
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private SpriteFont _font;
    private Random _rng = new Random();
    private int _worldSeed = Random.Shared.Next();

    // ---- Managers ----
    private readonly Camera2D _camera = new Camera2D();
    private readonly EnemyManager _enemyManager = new EnemyManager();
    private readonly PlayerState _playerState = new PlayerState();
    private readonly CombatManager _combatManager = new CombatManager();
    private readonly ProjectileManager _projectileManager = new ProjectileManager();
    private WorldManager _worldManager;

    // ---- State ----
    private GameState _state = GameState.Menu;
    private float _menuZoomScale = 6f;
    private bool _isZoomingOut = false;
    private float _zoomOutSpeed = 8f;

    // ---- Overworld save/restore ----
    private Vector2 _savedPlayerPosition;
    private Polarity _savedPlayerPolarity;
    private float _savedPolarityCharge;
    private float _savedArmor;
    private float _savedAmmo;

    // ---- Hive combat arena ----
    private HiveData _currentHive;
    private Vector2 _hiveCenter;
    private float _hiveWallDistance;
    private float _hiveWallStartDistance;
    private float _hiveEnemySpawnTimer;
    private float _hiveEnemySpawnInterval = 2.0f;

    // ---- Blade ----
    private const float BladeInnerRadius = 60f;
    private const float BladeOuterRadius = 90f;
    private const float MaxProjectileDistance = WorldChunk.Size * 8f;

    // ---- Textures ----
    private Texture2D _bladeTexture;
    private Texture2D _targetTexture;
    private Texture2D _eyeSocketTexture;
    private Texture2D _eyePupilTexture;
    private Dictionary<string, List<Texture2D>> _enemyAnimations;

    private float _eyeSocketScale = 1.5f;
    private float _pupilMaxOffset = 5f;
    private Vector2 _pupilOffset = Vector2.Zero;

    // ---- Score / kills ----
    private int _totalKills = 0;
    private int _xp = 0;
    private int _score = 0;
    private int _baseRedBugSpawnWeight = 10;
    private int _maxRedBugSpawnWeight = 100;
    private int _redBugSpawnStageCount = 7;
    private int _killsPerRedBugSpawnStage = 10;
    private int _armorRewardAmount = 15;
    private int _ammoRewardAmount = 40;

    // ---- Polarity ----
    private Polarity _bladePolarity = Polarity.Positive;
    private float _polarityCharge = 0f;
    private int _maxPolarityCharge = 100;
    private int _polarityChargePerPickup = 25;
    private float _polarityChargeDrainPerSecond = 10f;
    private ButtonState _previousRightMouseButton = ButtonState.Released;

    // ---- Pickups ----
    private List<Vector2> _polarityPickupPositions = new List<Vector2>();
    private float _polarityPickupSize = 14f;
    private float _polarityPickupSpawnTimer = 0f;
    private float _polarityPickupSpawnInterval = 2.5f;

    private List<Vector2> _rapidFirePelletPositions = new List<Vector2>();
    private float _rapidFirePelletSpawnTimer = 0f;
    private float _rapidFirePelletSpawnInterval = 6f;

    // ---- Gun ----
    private float _gunCooldownTimer = 0f;
    private float _gunCooldownDuration = 0.18f;
    private MouseState _previousMouse;

    private float _rapidFireCharge = 300f;
    private float _maxRapidFireCharge = 300f;
    private float _rapidFireChargePerPellet = 75f;
    private float _rapidFireDrainPerShot = 3f;
    private float _rapidFireShotInterval = 0f;
    private float _rapidFireShotTimer = 0f;
    private float _samePolarityDamageMultiplier = 0.35f;

    private List<Vector2> _bulletPositions = new List<Vector2>();
    private List<Vector2> _bulletVelocities = new List<Vector2>();
    private float _bulletSpeed = 650f;
    private int _bulletSize = 8;

    // ---- Explosion effects ----
    private List<ExplosionEffect> _explosionEffects = new List<ExplosionEffect>();
    private float _redBugExplosionRadius = 160f;
    private int _redBugExplosionDamage = 5;
    private int _redBugExplosionPlayerDamage = 15;
    private float _explosionEffectDuration = 0.25f;

    // ---- Armor regen ----
    private float _armorRegenRate = 2f;
    private float _armorRegenTimer = 0f;

    // ---- Enemy projectiles ----
    private float _enemyProjectileSpeed = 320f;
    private float _yellowBugShotInterval = 0.45f;

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
        _playerState.Position = new Vector2(
            WorldChunk.Size / 2f,
            WorldChunk.Size / 2f);
        base.Initialize();
        UpdateCamera();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _font = Content.Load<SpriteFont>("font");
        _eyeSocketTexture = Content.Load<Texture2D>("eye_socket");
        _eyePupilTexture = Content.Load<Texture2D>("eye_pupil");
        _bladeTexture = Content.Load<Texture2D>("Blade1_Frame1");
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
        for (int i = 1; i <= 7; i++)
            purpleFrames.Add(Content.Load<Texture2D>($"Purple_bug_frame{i}"));
        _enemyAnimations["Rapidfire Bug"] = purpleFrames;

        var yellowFrames = new List<Texture2D>();
        for (int i = 1; i <= 6; i++)
            yellowFrames.Add(Content.Load<Texture2D>($"Yellow_bug_frame{i}"));
        _enemyAnimations["Yellow Bug"] = yellowFrames;

        _worldManager = new WorldManager(_worldSeed);
        _worldManager.PreGenerateArea(GraphicsDevice, _playerState.Position, 20);
        _worldManager.Reset(_worldSeed);
        UpdateCamera();

        _worldManager.UpdateActiveChunks(
            GraphicsDevice,
            _playerState.Position,
            GraphicsDevice.Viewport.Width,
            GraphicsDevice.Viewport.Height);

        // Test hive
        _worldManager.Hives.Register(new HiveData(
            "TestHive_01",
            HiveType.Normal,
            new Vector2(WorldChunk.Size / 2f + 200f, WorldChunk.Size / 2f),
            new Point(0, 0),
            150,
            150,
            _worldSeed,
            1));

        _worldManager.Hives.Hives.First().Discovered = true;
    }

    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        return _camera.ScreenToWorld(screenPosition);
    }

    private void UpdateCamera()
    {
        _camera.Follow(
            _playerState.Position,
            GraphicsDevice.Viewport.Width,
            GraphicsDevice.Viewport.Height);
    }

    private bool CanMoveTo(Vector2 position, int size)
    {
        if (_state == GameState.HiveCombat)
        {
            float dist = Vector2.Distance(position, _hiveCenter);
            return dist + size / 2f < _hiveWallDistance;
        }
        return _worldManager.CanOccupyTerrain(position, _playerState.Size);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();

        if (_state == GameState.Digging && !_playerState.IsDead &&
            mouse.RightButton == ButtonState.Pressed &&
            _previousRightMouseButton == ButtonState.Released)
        {
            if (_bladePolarity == Polarity.Negative)
                _bladePolarity = Polarity.Positive;
            else if (_polarityCharge > 0f)
                _bladePolarity = Polarity.Negative;
        }
        _previousRightMouseButton = mouse.RightButton;

        if (keyboard.IsKeyDown(Keys.Escape)) Exit();

        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // ---- Menu ----
        if (_state == GameState.Menu)
        {
            if (_isZoomingOut)
            {
                _menuZoomScale = Math.Max(1f, _menuZoomScale - _zoomOutSpeed * delta);
                if (_menuZoomScale <= 1f) _state = GameState.Digging;
            }
            else if (keyboard.IsKeyDown(Keys.Enter))
            {
                _isZoomingOut = true;
            }
            base.Update(gameTime);
            return;
        }

        // ---- Dead ----
        if (_playerState.IsDead)
        {
            if (keyboard.IsKeyDown(Keys.R)) RestartGame();
            base.Update(gameTime);
            return;
        }

        // ---- Player movement ----
        float moveX = (keyboard.IsKeyDown(Keys.D) ? 1f : 0f) - (keyboard.IsKeyDown(Keys.A) ? 1f : 0f);
        float moveY = (keyboard.IsKeyDown(Keys.S) ? 1f : 0f) - (keyboard.IsKeyDown(Keys.W) ? 1f : 0f);
        Vector2 desiredPosition = _playerState.Position
            + new Vector2(moveX, moveY) * _playerState.Speed * delta;

        if (CanMoveTo(new Vector2(desiredPosition.X, _playerState.Position.Y), _playerState.Size))
            _playerState.Position = new Vector2(desiredPosition.X, _playerState.Position.Y);

        if (CanMoveTo(new Vector2(_playerState.Position.X, desiredPosition.Y), _playerState.Size))
            _playerState.Position = new Vector2(_playerState.Position.X, desiredPosition.Y);

        UpdateCamera();

        // ---- Enemy update (with arena-aware collision) ----
        if (_state == GameState.HiveCombat)
        {
            _enemyManager.UpdateEnemies(
                delta,
                _playerState.Position,
                _bladePolarity,
                _totalKills,
                _rng,
                _worldManager,
                spawnFromOverworld: false,
                terrainCollision: (pos, size) =>
                {
                    float dist = Vector2.Distance(pos, _hiveCenter);
                    return dist + size / 2f < _hiveWallDistance;
                });
        }
        else
        {
            _enemyManager.UpdateEnemies(
                delta,
                _playerState.Position,
                _bladePolarity,
                _totalKills,
                _rng,
                _worldManager,
                spawnFromOverworld: true,
                terrainCollision: null);
        }

        _enemyManager.UpdateYellowBugAttacks(
            delta,
            _playerState.Position,
            _projectileManager,
            _enemyProjectileSpeed,
            _yellowBugShotInterval);

        _projectileManager.UpdateEnemyProjectiles(
            delta,
            _playerState.Position,
            _playerState.Size,
            MaxProjectileDistance);

        UpdatePolarityCharge(delta);

        // ---- Digging-only logic ----
        if (_state == GameState.Digging)
        {
            _worldManager.UpdateActiveChunks(
                GraphicsDevice,
                _playerState.Position,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height);
            _worldManager.UpdatePlayerPosition(_playerState.Position);

            foreach (HiveData hive in _worldManager.Hives.Hives)
            {
                if (hive.Completed) continue;

                float distance = Vector2.Distance(_playerState.Position, hive.WorldPosition);
                float entryRadius = Math.Max(hive.RadiusX, hive.RadiusY) + 50f;
                if (distance <= entryRadius)
                {
                    EnterHive(hive);
                    break;
                }
            }

            UpdatePolarityPickups(delta);
            UpdateRapidFirePellets(delta);
            UpdatePupilTrackMouse();
        }

        // ---- Gun / bullets / explosions / contact (both states) ----
        UpdateGun(delta);
        UpdateBullets(delta);
        UpdateExplosionEffects(delta);
        CheckPlayerContact();

        _combatManager.UpdateBuzzsaw(
            delta,
            _playerState,
            _enemyManager,
            CanMoveTo,
            (position, radius) => _worldManager.DigTerrainAroundPosition(
                GraphicsDevice,
                position,
                radius),
            BladeInnerRadius,
            BladeOuterRadius,
            _combatManager.SawOrbitSpeed,
            _samePolarityDamageMultiplier,
            GrantEnemyReward);

        // ---- Armor regen ----
        if (_playerState.Armor < _playerState.MaxArmor)
        {
            _armorRegenTimer += delta;
            if (_armorRegenTimer >= 1f)
            {
                _armorRegenTimer -= 1f;
                _playerState.Armor = Math.Min(
                    _playerState.MaxArmor,
                    _playerState.Armor + (int)_armorRegenRate);
            }
        }

        // ---- Hive combat: spawn enemies from wall + escape ----
        if (_state == GameState.HiveCombat)
        {
            _hiveEnemySpawnTimer += delta;
            if (_hiveEnemySpawnTimer >= _hiveEnemySpawnInterval)
            {
                _hiveEnemySpawnTimer = 0f;
                SpawnEnemyFromHiveWall();
            }

            if (keyboard.IsKeyDown(Keys.Q))
                ExitHive();
        }

        base.Update(gameTime);
    }

    // ---- Hive combat ----

    private void EnterHive(HiveData hive)
    {
        _savedPlayerPosition = _playerState.Position;
        _savedPlayerPolarity = _bladePolarity;
        _savedPolarityCharge = _polarityCharge;
        _savedArmor = _playerState.Armor;
        _savedAmmo = _playerState.Ammo;

        _state = GameState.HiveCombat;
        _currentHive = hive;
        _hiveCenter = Vector2.Zero;

        // Doc: arena area = 10x blade area
        float bladeArea = MathF.PI * BladeOuterRadius * BladeOuterRadius;
        float arenaArea = bladeArea * 10f;
        float arenaRadius = MathF.Sqrt(arenaArea / MathF.PI);

        _hiveWallStartDistance = arenaRadius;
        _hiveWallDistance = arenaRadius;

        _playerState.Position = _hiveCenter;

        UpdateCamera();

        _enemyManager.Reset();
        _projectileManager.Reset();
        _hiveEnemySpawnTimer = 0f;
    }

    private void ExitHive()
    {
        _playerState.Position = _savedPlayerPosition;
        _bladePolarity = _savedPlayerPolarity;
        _polarityCharge = _savedPolarityCharge;
        _playerState.Armor = (int)_savedArmor;
        _playerState.Ammo = (int)_savedAmmo;

        _state = GameState.Digging;
        _enemyManager.Reset();
        _projectileManager.Reset();
        _currentHive = null;

        UpdateCamera();

        _worldManager.UpdateActiveChunks(
            GraphicsDevice,
            _playerState.Position,
            GraphicsDevice.Viewport.Width,
            GraphicsDevice.Viewport.Height);
    }

    private void SpawnEnemyFromHiveWall()
    {
        if (_currentHive == null) return;
        if (EnemyDatabase.AllTypes == null || EnemyDatabase.AllTypes.Count == 0) return;

        int pick = _rng.Next(EnemyDatabase.AllTypes.Count);
        EnemyDefinition definition = EnemyDatabase.AllTypes[pick];

        // Spawn just inside the wall — enough inset to satisfy collision check
        float angle = (float)(_rng.NextDouble() * Math.PI * 2);
        float inset = definition.Size / 2f + 40f;
        Vector2 spawnPos = _hiveCenter + new Vector2(
            MathF.Cos(angle), MathF.Sin(angle)) * (_hiveWallDistance - inset);

        _enemyManager._enemies.Add(new Enemy(spawnPos, definition));
    }

    private void DrawHiveInterior(SpriteBatch spriteBatch)
    {
        Rectangle screenRect = new Rectangle(
            (int)(_hiveCenter.X - _hiveWallDistance - 2000f),
            (int)(_hiveCenter.Y - _hiveWallDistance - 2000f),
            (int)(_hiveWallDistance * 2f + 4000f),
            (int)(_hiveWallDistance * 2f + 4000f));
        spriteBatch.Draw(_pixel, screenRect, new Color(25, 8, 15));

        int segments = 128;
        float thickness = 24f;
        Color wallColor = new Color(120, 15, 35);

        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * MathF.PI * 2f;
            Vector2 pos = _hiveCenter + new Vector2(MathF.Cos(a), MathF.Sin(a)) * _hiveWallDistance;
            float rot = a + MathF.PI / 2f;
            spriteBatch.Draw(
                _pixel,
                pos,
                null,
                wallColor,
                rot,
                new Vector2(0.5f, 0.5f),
                new Vector2(thickness, thickness * 1.5f),
                SpriteEffects.None,
                0f);
        }
    }

    // ---- Rewards / pickups ----

    private void GrantEnemyReward(string rewardType)
    {
        switch (rewardType)
        {
            case "xp":
                _xp += 1;
                _score += 1;
                break;
            case "armor":
                _playerState.Armor = Math.Min(_playerState.MaxArmor, _playerState.Armor + _armorRewardAmount);
                break;
            case "ammo":
                _playerState.Ammo = Math.Min(_playerState.MaxAmmo, _playerState.Ammo + _ammoRewardAmount);
                break;
            case "rapidfire":
                _rapidFireCharge = Math.Min(_maxRapidFireCharge, _rapidFireCharge + _rapidFireChargePerPellet);
                break;
        }
    }

    private void RestartGame()
    {
        _worldSeed = Random.Shared.Next();
        _worldManager.Reset(_worldSeed);

        _playerState.Position = new Vector2(WorldChunk.Size / 2f, WorldChunk.Size / 2f);
        UpdateCamera();

        _worldManager.UpdateActiveChunks(
            GraphicsDevice,
            _camera.Position,
            GraphicsDevice.Viewport.Width,
            GraphicsDevice.Viewport.Height);

        _playerState.Armor = _playerState.MaxArmor;
        _playerState.Ammo = _playerState.MaxAmmo;
        _totalKills = 0;
        _xp = 0;
        _score = 0;
        _bladePolarity = Polarity.Positive;
        _polarityCharge = 0f;
        _previousRightMouseButton = ButtonState.Released;

        _enemyManager.Reset();
        _projectileManager.Reset();
        _explosionEffects.Clear();
        _polarityPickupPositions.Clear();
        _rapidFirePelletPositions.Clear();
        _bulletPositions.Clear();
        _bulletVelocities.Clear();

        _polarityPickupSpawnTimer = 0f;
        _rapidFirePelletSpawnTimer = 0f;
        _gunCooldownTimer = 0f;
        _rapidFireShotTimer = 0f;
        _rapidFireCharge = _maxRapidFireCharge;

        _playerState.IsDead = false;
    }

    private void UpdatePolarityCharge(float delta)
    {
        if (_bladePolarity != Polarity.Negative) return;

        _polarityCharge = Math.Max(0f, _polarityCharge - _polarityChargeDrainPerSecond * delta);
        if (_polarityCharge <= 0f)
            _bladePolarity = Polarity.Positive;
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
                totalSpawnWeight += definition.SpawnWeight;
        }

        return (float)redBugSpawnWeight / totalSpawnWeight * 100f;
    }

    private void UpdatePolarityPickups(float delta)
    {
        if (_playerState.IsDead) return;

        _polarityPickupSpawnTimer += delta;
        if (_polarityPickupSpawnTimer >= _polarityPickupSpawnInterval)
        {
            _polarityPickupSpawnTimer = 0f;
            _polarityPickupPositions.Add(_playerState.Position + new Vector2(
                _rng.Next(-GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Width / 2),
                _rng.Next(-GraphicsDevice.Viewport.Height / 2, GraphicsDevice.Viewport.Height / 2)));
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
            if (_rapidFireShotTimer <= 0 && _playerState.Ammo > 0)
            {
                Shoot(mouse);
                _rapidFireCharge = Math.Max(0, _rapidFireCharge - _rapidFireDrainPerShot);
                _rapidFireShotTimer = _rapidFireShotInterval;
            }
        }
        else
        {
            bool justClicked = mouse.LeftButton == ButtonState.Pressed &&
                _previousMouse.LeftButton == ButtonState.Released;
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
        if (_playerState.IsDead) return;

        _rapidFirePelletSpawnTimer += delta;
        if (_rapidFirePelletSpawnTimer >= _rapidFirePelletSpawnInterval)
        {
            _rapidFirePelletSpawnTimer = 0f;
            _rapidFirePelletPositions.Add(_playerState.Position + new Vector2(
                _rng.Next(-GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Width / 2),
                _rng.Next(-GraphicsDevice.Viewport.Height / 2, GraphicsDevice.Viewport.Height / 2)));
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
        if (_playerState.IsShellBroken) return false;

        float distance = Vector2.Distance(_playerState.Position, pickupPosition);
        return distance + pickupRadius >= BladeInnerRadius &&
               distance - pickupRadius <= BladeOuterRadius;
    }

    private void Shoot(MouseState mouse)
    {
        if (_playerState.Ammo <= 0) return;
        _playerState.Ammo -= _combatManager.AmmoCostPerShot;

        Vector2 mousePosition = ScreenToWorld(new Vector2(mouse.X, mouse.Y));
        Vector2 direction = mousePosition - _playerState.Position;
        if (direction.LengthSquared() > 0.01f)
            direction.Normalize();

        _bulletPositions.Add(_playerState.Position);
        _bulletVelocities.Add(direction * _bulletSpeed);
    }

    private void UpdateBullets(float delta)
    {
        if (_playerState.IsDead) return;

        for (int i = _bulletPositions.Count - 1; i >= 0; i--)
        {
            _bulletPositions[i] += _bulletVelocities[i] * delta;
            Rectangle bulletRect = new Rectangle(
                (int)_bulletPositions[i].X - _bulletSize / 2,
                (int)_bulletPositions[i].Y - _bulletSize / 2,
                _bulletSize, _bulletSize);

            bool bulletHit = false;

            for (int j = _enemyManager._enemies.Count - 1; j >= 0; j--)
            {
                Enemy enemy = _enemyManager._enemies[j];
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
                        _enemyManager._enemies.RemoveAt(j);
                        _totalKills++;
                        if (isRedBug)
                            DetonateRedBug(killedEnemyPosition);
                    }
                    bulletHit = true;
                    break;
                }
            }

            if (!bulletHit)
            {
                foreach (EnemyProjectile projectile in _projectileManager.EnemyProjectiles)
                {
                    Rectangle projectileRect = new Rectangle(
                        (int)projectile.Position.X - _playerState.Size / 2,
                        (int)projectile.Position.Y - _playerState.Size / 2,
                        _playerState.Size,
                        _playerState.Size);

                    if (bulletRect.Intersects(projectileRect))
                    {
                        _projectileManager.RemoveEnemyProjectile(projectile);
                        bulletHit = true;
                        break;
                    }
                }
            }

            if (bulletHit || Vector2.DistanceSquared(_bulletPositions[i], _playerState.Position) > 800f * 800f)
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

            if (Vector2.DistanceSquared(_playerState.Position, explosionPosition) <= explosionRadiusSquared)
            {
                _playerState.Armor = Math.Max(0, _playerState.Armor - _redBugExplosionPlayerDamage);
                if (_playerState.Armor <= 0) _playerState.IsDead = true;
            }

            for (int i = _enemyManager._enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = _enemyManager._enemies[i];
                if (Vector2.DistanceSquared(enemy.Position, explosionPosition) > explosionRadiusSquared)
                    continue;

                enemy.Health -= _redBugExplosionDamage;
                if (enemy.Health > 0) continue;

                bool chainedRedBug = enemy.Definition.Name == "Red Bug";
                Vector2 chainedPosition = enemy.Position;
                GrantEnemyReward(enemy.Definition.RewardType);
                _enemyManager._enemies.RemoveAt(i);
                _totalKills++;
                if (chainedRedBug) pendingExplosions.Enqueue(chainedPosition);
            }
        }
    }

    private void UpdateExplosionEffects(float delta)
    {
        for (int i = _explosionEffects.Count - 1; i >= 0; i--)
        {
            _explosionEffects[i].Age += delta;
            if (_explosionEffects[i].Age >= _explosionEffectDuration)
                _explosionEffects.RemoveAt(i);
        }
    }

    private void CheckPlayerContact()
    {
        if (_playerState.IsDead) return;

        Rectangle playerRect = new Rectangle(
            (int)_playerState.Position.X - _playerState.Size / 2,
            (int)_playerState.Position.Y - _playerState.Size / 2,
            _playerState.Size, _playerState.Size);

        foreach (var enemy in _enemyManager._enemies)
        {
            Rectangle enemyRect = new Rectangle(
                (int)enemy.Position.X - enemy.Definition.Size / 2,
                (int)enemy.Position.Y - enemy.Definition.Size / 2,
                enemy.Definition.Size, enemy.Definition.Size);

            if (!playerRect.Intersects(enemyRect)) continue;

            if (enemy.Definition.Charge == _bladePolarity)
            {
                int reducedContactDamage = Math.Max(1,
                    (int)Math.Round(enemy.Definition.SawDamage * _samePolarityDamageMultiplier));
                _playerState.Armor = Math.Max(0, _playerState.Armor - reducedContactDamage);

                Vector2 repel = enemy.Position - _playerState.Position;
                if (repel.LengthSquared() > 0.01f)
                {
                    repel.Normalize();
                    _enemyManager.MoveEnemy(enemy, repel * 30f, CanMoveTo);
                }

                if (_playerState.Armor <= 0) _playerState.IsDead = true;
                continue;
            }

            int meleeDamage = enemy.Definition.SawDamage;
            _playerState.Armor = Math.Max(0, _playerState.Armor - meleeDamage);
            if (_playerState.Armor <= 0) _playerState.IsDead = true;
        }
    }

    private void UpdatePupilTrackMouse()
    {
        MouseState mouse = Mouse.GetState();
        Vector2 mousePos = ScreenToWorld(new Vector2(mouse.X, mouse.Y));
        Vector2 toMouse = mousePos - _playerState.Position;

        if (toMouse.LengthSquared() > 0.01f)
        {
            float distance = Math.Min(toMouse.Length(), _pupilMaxOffset);
            toMouse.Normalize();
            _pupilOffset = toMouse * distance;
        }
    }
    

    // ---- Draw ----

    protected override void Draw(GameTime gameTime)
    {
        if (_state == GameState.HiveCombat)
            GraphicsDevice.Clear(new Color(20, 5, 15));
        else
            GraphicsDevice.Clear(Color.CornflowerBlue);

        _spriteBatch.Begin();

        // ---- Menu ----
        if (_state == GameState.Menu)
        {
            Vector2 menuCenter = new Vector2(
                _graphics.PreferredBackBufferWidth / 2f,
                _graphics.PreferredBackBufferHeight / 2f);

            Vector2 menuSocketOrigin = new Vector2(_eyeSocketTexture.Width / 2f - 24.5f, _eyeSocketTexture.Height / 2f - 68.5f);
            float menuSocketScale = _menuZoomScale * _eyeSocketScale * _playerState.Size / _eyeSocketTexture.Width;
            _spriteBatch.Draw(_eyeSocketTexture, menuCenter, null, Color.White, 0f, menuSocketOrigin, menuSocketScale, SpriteEffects.None, 0f);

            float animationTime = (float)gameTime.TotalGameTime.TotalSeconds;
            Vector2 menuPupilOffset = new Vector2(
                MathF.Cos(animationTime),
                MathF.Sin(animationTime * 0.7f)) * (_pupilMaxOffset * _menuZoomScale * 0.5f);

            Vector2 menuPupilOrigin = new Vector2(_eyePupilTexture.Width / 2f - 27f, _eyePupilTexture.Height / 2f - 55f);
            float menuPupilScale = _menuZoomScale * _playerState.Size / _eyeSocketTexture.Width;
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

        _spriteBatch.End();
        _spriteBatch.Begin(transformMatrix: _camera.TransformMatrix);

        // ---- World ----
        if (_state == GameState.HiveCombat)
            DrawHiveInterior(_spriteBatch);
        else
            _worldManager.DrawActiveChunks(_spriteBatch);

        // ---- Enemies ----
        foreach (var enemy in _enemyManager._enemies)
        {
            Rectangle enemyRect = new Rectangle(
                (int)enemy.Position.X - enemy.Definition.Size / 2,
                (int)enemy.Position.Y - enemy.Definition.Size / 2,
                enemy.Definition.Size, enemy.Definition.Size);

            if (_enemyAnimations.TryGetValue(enemy.Definition.Name, out List<Texture2D> frames))
                _spriteBatch.Draw(frames[enemy.AnimFrame], enemyRect, Color.White);
            else
                _spriteBatch.Draw(_pixel, enemyRect, enemy.Definition.DrawColor);
        }

        // ---- Enemy projectiles ----
        _projectileManager.Draw(_spriteBatch, _pixel);

        // ---- Explosions ----
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

        // ---- Blade ----
        Color bladeTint = _playerState.IsShellBroken ? Color.DarkRed : Color.White;
        Vector2 bladeOrigin = new Vector2(_bladeTexture.Width / 2f, _bladeTexture.Height / 2f);
        _spriteBatch.Draw(
            _bladeTexture,
            _playerState.Position,
            null,
            bladeTint,
            _combatManager.SawAngle,
            bladeOrigin,
            (BladeOuterRadius * 2) / _bladeTexture.Width,
            SpriteEffects.None,
            0f);

        // ---- Eye socket ----
        Vector2 socketOrigin = new Vector2(_eyeSocketTexture.Width / 2f - 24.5f, _eyeSocketTexture.Height / 2f - 68.5f);
        _spriteBatch.Draw(_eyeSocketTexture, _playerState.Position, null, Color.White, 0f, socketOrigin,
            _eyeSocketScale * _playerState.Size / _eyeSocketTexture.Width, SpriteEffects.None, 0f);

        // ---- Pupil ----
        Vector2 pupilOrigin = new Vector2(_eyePupilTexture.Width / 2f - 27f, _eyePupilTexture.Height / 2f - 55f);
        Vector2 pupilDrawPosition = _playerState.Position + _pupilOffset;
        float pupilScale = _playerState.Size / (float)_eyeSocketTexture.Width;
        _spriteBatch.Draw(_eyePupilTexture, pupilDrawPosition, null, Color.White, 0f, pupilOrigin, pupilScale, SpriteEffects.None, 0f);

        // ---- Pickups ----
        foreach (var pickupPos in _polarityPickupPositions)
        {
            Rectangle pickupRect = new Rectangle(
                (int)pickupPos.X - (int)(_polarityPickupSize / 2),
                (int)pickupPos.Y - (int)(_polarityPickupSize / 2),
                (int)_polarityPickupSize, (int)_polarityPickupSize);
            _spriteBatch.Draw(_pixel, pickupRect, Color.Cyan);
        }

        foreach (var pos in _rapidFirePelletPositions)
        {
            Rectangle rect = new Rectangle((int)pos.X - 7, (int)pos.Y - 7, 14, 14);
            _spriteBatch.Draw(_pixel, rect, Color.Purple);
        }

        foreach (var bulletPos in _bulletPositions)
        {
            Rectangle bulletRect = new Rectangle(
                (int)bulletPos.X - _bulletSize / 2,
                (int)bulletPos.Y - _bulletSize / 2,
                _bulletSize, _bulletSize);
            _spriteBatch.Draw(_pixel, bulletRect, Color.Yellow);
        }

        _spriteBatch.End();
        _spriteBatch.Begin();

        // ---- HUD ----
        Rectangle mapBounds = new Rectangle(
            _graphics.PreferredBackBufferWidth - 210,
            10,
            200,
            200);
        _worldManager.Map.Draw(_spriteBatch, _pixel, mapBounds, _playerState.Position, true);

        _spriteBatch.DrawString(_font, "Score: " + _score + "  XP: " + _xp, new Vector2(10, 10), Color.White);

        int barWidth = 200;
        int barHeight = 20;
        Rectangle barBackground = new Rectangle(10, 40, barWidth, barHeight);
        _spriteBatch.Draw(_pixel, barBackground, Color.Gray);
        int filledWidth = (int)((float)_playerState.Armor / _playerState.MaxArmor * barWidth);
        Rectangle barFill = new Rectangle(10, 40, filledWidth, barHeight);
        _spriteBatch.Draw(_pixel, barFill, _playerState.IsShellBroken ? Color.DarkRed : Color.LimeGreen);

        Rectangle ammoBarBackground = new Rectangle(10, 70, barWidth, 14);
        _spriteBatch.Draw(_pixel, ammoBarBackground, Color.DarkGray);
        int ammoFilledWidth = (int)((float)_playerState.Ammo / _playerState.MaxAmmo * barWidth);
        Rectangle ammoBarFill = new Rectangle(10, 70, ammoFilledWidth, 14);
        _spriteBatch.Draw(_pixel, ammoBarFill, Color.Orange);

        Rectangle chargeBarBackground = new Rectangle(10, 90, barWidth, 14);
        _spriteBatch.Draw(_pixel, chargeBarBackground, Color.DarkGray);
        int chargeFilledWidth = (int)(_rapidFireCharge / _maxRapidFireCharge * barWidth);
        Rectangle chargeBarFill = new Rectangle(10, 90, chargeFilledWidth, 14);
        _spriteBatch.Draw(_pixel, chargeBarFill, Color.Purple);

        if (_playerState.IsDead)
        {
            Rectangle overlay = new Rectangle(0, 0,
                _graphics.PreferredBackBufferWidth,
                _graphics.PreferredBackBufferHeight);
            _spriteBatch.Draw(_pixel, overlay, Color.Black * 0.7f);

            string deathText = "YOU DIED";
            Vector2 deathSize = _font.MeasureString(deathText) * 2f;
            _spriteBatch.DrawString(_font, deathText,
                new Vector2(_graphics.PreferredBackBufferWidth / 2 - deathSize.X / 2,
                            _graphics.PreferredBackBufferHeight / 2 - deathSize.Y / 2),
                Color.Red, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);

            string restartText = "Press R to restart";
            Vector2 restartSize = _font.MeasureString(restartText);
            _spriteBatch.DrawString(_font, restartText,
                new Vector2(_graphics.PreferredBackBufferWidth / 2 - restartSize.X / 2,
                            _graphics.PreferredBackBufferHeight / 2 + 40),
                Color.White);
        }

        _spriteBatch.DrawString(_font, "Kills: " + _totalKills, new Vector2(10, 120), Color.White);
        _spriteBatch.DrawString(_font,
            "Red spawn stage: " + GetCurrentRedBugSpawnStage() + "/" + _redBugSpawnStageCount +
            " (" + GetRedBugSpawnChancePercent().ToString("F0") + "%)",
            new Vector2(10, 145), Color.Red);

        string bladeChargeText = _bladePolarity == Polarity.Positive ? "Positive" : "Negative";
        Color bladeChargeColor = _bladePolarity == Polarity.Positive ? Color.Cyan : Color.OrangeRed;
        _spriteBatch.DrawString(_font,
            "Blade: " + bladeChargeText + "  Charge: " + _polarityCharge.ToString("F0") + "/" + _maxPolarityCharge,
            new Vector2(10, 170), bladeChargeColor);

        Rectangle polarityMeterBackground = new Rectangle(10, 194, 200, 16);
        _spriteBatch.Draw(_pixel, polarityMeterBackground, Color.DarkGray);
        int polarityMeterWidth = (int)((float)_polarityCharge / _maxPolarityCharge * polarityMeterBackground.Width);
        Rectangle polarityMeterFill = new Rectangle(10, 194, polarityMeterWidth, polarityMeterBackground.Height);
        _spriteBatch.Draw(_pixel, polarityMeterFill, bladeChargeColor);

        _spriteBatch.DrawString(_font,
            "Right-click: switch  Charge drain: " + _polarityChargeDrainPerSecond.ToString("F0") + "/s",
            new Vector2(10, 214), Color.White);

        // ---- Crosshair ----
        MouseState mouseState = Mouse.GetState();
        Vector2 mousePos = new Vector2(mouseState.X, mouseState.Y);
        int targetSize = 96;
        Rectangle targetRect = new Rectangle(
            (int)mousePos.X - targetSize / 2,
            (int)mousePos.Y - targetSize / 2,
            targetSize, targetSize);
        _spriteBatch.Draw(_targetTexture, targetRect, Color.White);

        _spriteBatch.End();
        base.Draw(gameTime);
    }
}