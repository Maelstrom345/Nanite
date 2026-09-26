using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Nanite;

public class EnemyDefinition
{
    public string Name;
    public Color DrawColor;
    public float Speed;
    public int Size;
    public string RewardType; // "xp", "armor", "ammo"
    public int SpawnWeight;

    public EnemyDefinition(string name, Color color, float speed, int size, string rewardType, int spawnWeight)
    {
        Name = name;
        DrawColor = color;
        Speed = speed;
        Size = size;
        RewardType = rewardType;
        SpawnWeight = spawnWeight;
    }
}

public class Enemy
{
    public Vector2 Position;
    public EnemyDefinition Definition;

    public Enemy(Vector2 position, EnemyDefinition definition)
    {
        Position = position;
        Definition = definition;
    }
}

public static class EnemyDatabase
{
    public static List<EnemyDefinition> AllTypes = new List<EnemyDefinition>
    {
        new EnemyDefinition("Grunt", Color.Red, 60f, 28, "xp", 70),
        new EnemyDefinition("Armor Bug", Color.Green, 50f, 26, "armor", 15),
        new EnemyDefinition("Ammo Bug", Color.Orange, 50f, 26, "ammo", 15),
    };

    public static EnemyDefinition GetRandom(Random rng)
    {
        int totalWeight = 0;
        foreach (var def in AllTypes) totalWeight += def.SpawnWeight;

        int roll = rng.Next(totalWeight);
        int cumulative = 0;
        foreach (var def in AllTypes)
        {
            cumulative += def.SpawnWeight;
            if (roll < cumulative) return def;
        }
        return AllTypes[0];
    }
}

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private SpriteFont _font;

    // Player
    private Vector2 _playerPosition;
    private float _playerSpeed = 200f;
    private int _playerSize = 32;
    private bool _isDead = false;

    // Buzzsaw shell
    private int _sawSlotCount = 24;
    private float _sawSegmentWidth = 16f;
    private float _sawSegmentHeight = 6f;
    private float _sawAngle = 0f;
    private float _sawOrbitSpeed = 3f;
    private float _sawOrbitRadius = 80f;

    // Armor — now a threshold state instead of gradual segment loss
    private int _maxArmor = 100;
    private int _armor = 100;
    private int _armorLossPerKill = 12;
    private int _armorBreakThreshold = 25;

    private bool IsShellBroken() => _armor <= _armorBreakThreshold;

    // Ammo
    private int _ammo = 40;
    private int _maxAmmo = 40;
    private int _ammoCostPerShot = 1;
    private int _ammoRewardAmount = 10;

    // Armor pellets (healing)
    private List<Vector2> _pelletPositions = new List<Vector2>();
    private float _pelletSize = 14f;
    private int _pelletHealAmount = 20;
    private float _pelletSpawnTimer = 0f;
    private float _pelletSpawnInterval = 4f;

    // Enemies
    private List<Enemy> _enemies = new List<Enemy>();
    private Random _rng = new Random();
    private float _enemySpawnTimer = 0f;
    private float _enemySpawnInterval = 1.5f;
    private float _survivalTime = 0f;
    private float _difficultyRampRate = 0.015f;
    private float _minSpawnInterval = 0.15f;

    // XP
    private int _xp = 0;
    private int _armorRewardAmount = 15;

    // Bullets
    private List<Vector2> _bulletPositions = new List<Vector2>();
    private List<Vector2> _bulletVelocities = new List<Vector2>();
    private float _bulletSpeed = 400f;
    private int _bulletSize = 8;

    // Gun / firing
    private float _gunCooldownTimer = 0f;
    private float _gunCooldownDuration = 0.3f;
    private MouseState _previousMouse;

    // Rapid fire charge
    private float _rapidFireCharge = 100f;
    private float _maxRapidFireCharge = 100f;
    private float _rapidFireChargePerPellet = 35f;
    private float _rapidFireDrainPerShot = 8f;
    private float _rapidFireShotInterval = 0.08f;
    private float _rapidFireShotTimer = 0f;
    private List<Vector2> _rapidFirePelletPositions = new List<Vector2>();
    private float _rapidFirePelletSpawnTimer = 0f;
    private float _rapidFirePelletSpawnInterval = 12f;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _playerPosition = new Vector2(400, 300);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = Content.Load<SpriteFont>("font");
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape)) Exit();

        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

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

        UpdateEnemies(delta);
        UpdateBuzzsaw(delta);
        UpdatePellets(delta);
        UpdateGun(delta);
        UpdateRapidFirePellets(delta);
        UpdateBullets(delta);
        CheckPlayerContact();

        base.Update(gameTime);
    }

    private void GrantEnemyReward(string rewardType)
    {
        switch (rewardType)
        {
            case "xp":
                _xp += 1;
                break;
            case "armor":
                _armor = Math.Min(_maxArmor, _armor + _armorRewardAmount);
                break;
            case "ammo":
                _ammo = Math.Min(_maxAmmo, _ammo + _ammoRewardAmount);
                break;
        }
    }

    private void RestartGame()
    {
        _playerPosition = new Vector2(_graphics.PreferredBackBufferWidth / 2, _graphics.PreferredBackBufferHeight / 2);
        _armor = _maxArmor;
        _ammo = _maxAmmo;
        _rapidFireCharge = 0f;
        _xp = 0;
        _enemies.Clear();
        _pelletPositions.Clear();
        _rapidFirePelletPositions.Clear();
        _bulletPositions.Clear();
        _bulletVelocities.Clear();
        _sawAngle = 0f;
        _enemySpawnTimer = 0f;
        _pelletSpawnTimer = 0f;
        _rapidFirePelletSpawnTimer = 0f;
        _gunCooldownTimer = 0f;
        _rapidFireShotTimer = 0f;
        _rapidFireCharge = 100f;
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

        foreach (var enemy in _enemies)
        {
            Vector2 toPlayer = _playerPosition - enemy.Position;
            if (toPlayer.LengthSquared() > 1f)
            {
                toPlayer.Normalize();
                enemy.Position += toPlayer * enemy.Definition.Speed * delta;
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

        EnemyDefinition def = EnemyDatabase.GetRandom(_rng);
        _enemies.Add(new Enemy(spawnPos, def));
    }

    private void UpdateBuzzsaw(float delta)
    {
        if (_isDead) return;

        _sawAngle += _sawOrbitSpeed * delta;

        if (IsShellBroken()) return; // shell passable — enemies pass through untouched

        var sawSegments = GetSawSegments();

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = _enemies[i];
            Rectangle enemyRect = new Rectangle(
                (int)enemy.Position.X - enemy.Definition.Size / 2,
                (int)enemy.Position.Y - enemy.Definition.Size / 2,
                enemy.Definition.Size,
                enemy.Definition.Size);

            bool hitBySaw = false;
            foreach (var segment in sawSegments)
            {
                Rectangle sawRect = new Rectangle(
                    (int)segment.position.X - (int)(_sawSegmentWidth / 2),
                    (int)segment.position.Y - (int)(_sawSegmentWidth / 2),
                    (int)_sawSegmentWidth,
                    (int)_sawSegmentWidth);
                if (sawRect.Intersects(enemyRect))
                {
                    hitBySaw = true;
                    break;
                }
            }

            if (hitBySaw)
            {
                GrantEnemyReward(enemy.Definition.RewardType);
                _armor = Math.Max(0, _armor - _armorLossPerKill);
                _enemies.RemoveAt(i);
            }
        }
    }

    private List<(Vector2 position, float angle)> GetSawSegments()
    {
        var segments = new List<(Vector2, float)>();

        for (int i = 0; i < _sawSlotCount; i++)
        {
            float slotAngle = _sawAngle + (i * (MathHelper.TwoPi / _sawSlotCount));
            Vector2 offset = new Vector2(
                (float)Math.Cos(slotAngle) * _sawOrbitRadius,
                (float)Math.Sin(slotAngle) * _sawOrbitRadius
            );
            segments.Add((_playerPosition + offset, slotAngle));
        }

        return segments;
    }

    private void UpdatePellets(float delta)
    {
        if (_isDead) return;

        _pelletSpawnTimer += delta;
        if (_pelletSpawnTimer >= _pelletSpawnInterval)
        {
            _pelletSpawnTimer = 0f;
            int screenW = _graphics.PreferredBackBufferWidth;
            int screenH = _graphics.PreferredBackBufferHeight;
            _pelletPositions.Add(new Vector2(_rng.Next(50, screenW - 50), _rng.Next(50, screenH - 50)));
        }

        Rectangle playerRect = new Rectangle((int)_playerPosition.X - _playerSize / 2, (int)_playerPosition.Y - _playerSize / 2, _playerSize, _playerSize);

        for (int i = _pelletPositions.Count - 1; i >= 0; i--)
        {
            Rectangle pelletRect = new Rectangle((int)_pelletPositions[i].X - (int)(_pelletSize / 2), (int)_pelletPositions[i].Y - (int)(_pelletSize / 2), (int)_pelletSize, (int)_pelletSize);
            if (playerRect.Intersects(pelletRect))
            {
                _armor = Math.Min(_maxArmor, _armor + _pelletHealAmount);
                _pelletPositions.RemoveAt(i);
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

        Rectangle playerRect = new Rectangle((int)_playerPosition.X - _playerSize / 2, (int)_playerPosition.Y - _playerSize / 2, _playerSize, _playerSize);

        for (int i = _rapidFirePelletPositions.Count - 1; i >= 0; i--)
        {
            Rectangle pelletRect = new Rectangle((int)_rapidFirePelletPositions[i].X - 7, (int)_rapidFirePelletPositions[i].Y - 7, 14, 14);
            if (playerRect.Intersects(pelletRect))
            {
                _rapidFireCharge = Math.Min(_maxRapidFireCharge, _rapidFireCharge + _rapidFireChargePerPellet);
                _rapidFirePelletPositions.RemoveAt(i);
            }
        }
    }

    private void Shoot(MouseState mouse)
    {
        if (_ammo <= 0) return;
        _ammo -= _ammoCostPerShot;

        Vector2 mousePosition = new Vector2(mouse.X, mouse.Y);
        Vector2 direction = mousePosition - _playerPosition;
        if (direction.LengthSquared() > 0) direction.Normalize();

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
                    GrantEnemyReward(enemy.Definition.RewardType);
                    _enemies.RemoveAt(j);
                    bulletHit = true;
                    break;
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
                _isDead = true;
            }
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _spriteBatch.Begin();

        Color playerColor = _rapidFireCharge > 0 ? Color.Purple : Color.Black;
        Rectangle playerRectangle = new Rectangle((int)_playerPosition.X - _playerSize / 2, (int)_playerPosition.Y - _playerSize / 2, _playerSize, _playerSize);
        _spriteBatch.Draw(_pixel, playerRectangle, playerColor);

        foreach (var enemy in _enemies)
        {
            Rectangle enemyRect = new Rectangle(
                (int)enemy.Position.X - enemy.Definition.Size / 2,
                (int)enemy.Position.Y - enemy.Definition.Size / 2,
                enemy.Definition.Size, enemy.Definition.Size);
            _spriteBatch.Draw(_pixel, enemyRect, enemy.Definition.DrawColor);
        }

        // Buzzsaw shell — color reflects broken/intact state
        var sawSegments = GetSawSegments();
        Color segmentColor = IsShellBroken() ? Color.DarkRed : Color.Gray;
        foreach (var segment in sawSegments)
        {
            Rectangle segmentRect = new Rectangle(0, 0, (int)_sawSegmentWidth, (int)_sawSegmentHeight);
            Vector2 origin = new Vector2(_sawSegmentWidth / 2, _sawSegmentHeight / 2);
            _spriteBatch.Draw(_pixel, segment.position, segmentRect, segmentColor, segment.angle + MathHelper.PiOver2, origin, 1f, SpriteEffects.None, 0f);
        }

        foreach (var pelletPos in _pelletPositions)
        {
            Rectangle pelletRect = new Rectangle((int)pelletPos.X - (int)(_pelletSize / 2), (int)pelletPos.Y - (int)(_pelletSize / 2), (int)_pelletSize, (int)_pelletSize);
            _spriteBatch.Draw(_pixel, pelletRect, Color.LightGray);
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
        _spriteBatch.DrawString(_font, "Caught: " + _xp, new Vector2(10, 10), Color.White);

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

        _spriteBatch.End();
        base.Draw(gameTime);
    }
}