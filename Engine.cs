using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.SDL.Ttf;
using TheAdventure.Models;
using TheAdventure.Models.Data;

namespace TheAdventure
{
    public class Engine
    {
        private bool _isPaused = false;
        private bool _isGameOver = false;
        private readonly Dictionary<int, GameObject> _gameObjects = new();
        private readonly Dictionary<string, TileSet> _loadedTileSets = new();
        private Level? _currentLevel;
        private PlayerObject? _player;
        private GameRenderer _renderer;
        private Input? _input;
        private ScriptEngine _scriptEngine;
        private DateTimeOffset _lastUpdate = DateTimeOffset.Now;
        private Sdl _sdl;
        private Ttf _ttf;

        public Engine(GameRenderer renderer, Input? input)
        {
            _renderer = renderer;
            _input = input;
            _scriptEngine = new ScriptEngine();
            _sdl = Sdl.GetApi();
            _ttf = Ttf.GetApi();

            if (_input != null)
            {
                _input.OnMouseClick += (_, coords) => AddBomb(coords.x, coords.y);
            }
        }

        public void SetInput(Input input)
        {
            _input = input;
            _input.OnMouseClick += (_, coords) => AddBomb(coords.x, coords.y);
        }

        public void TogglePause()
        {
            _isPaused = !_isPaused;
            Console.WriteLine("Pause: " + _isPaused);
        }

        public bool IsPaused()
        {
            return _isPaused;
        }

        public void WriteToConsole(string message)
        {
            Console.WriteLine(message);
        }

        public (int x, int y) GetPlayerPosition()
        {
            if (_player == null) throw new InvalidOperationException("Player not initialized.");
            var pos = _player.Position;
            return (pos.X, pos.Y);
        }

        public void InitializeWorld()
        {
            var executableLocation = new FileInfo(Assembly.GetExecutingAssembly().Location);
            _scriptEngine.LoadAll(Path.Combine(executableLocation.Directory.FullName, "Assets", "Scripts"));

            var jsonSerializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            var levelContent = File.ReadAllText(Path.Combine("Assets", "terrain.tmj"));

            var level = JsonSerializer.Deserialize<Level>(levelContent, jsonSerializerOptions);
            if (level == null) return;
            foreach (var refTileSet in level.TileSets)
            {
                var tileSetContent = File.ReadAllText(Path.Combine("Assets", refTileSet.Source));
                if (!_loadedTileSets.TryGetValue(refTileSet.Source, out var tileSet))
                {
                    tileSet = JsonSerializer.Deserialize<TileSet>(tileSetContent, jsonSerializerOptions);

                    foreach (var tile in tileSet.Tiles)
                    {
                        var internalTextureId = _renderer.LoadTexture(Path.Combine("Assets", tile.Image), out _);
                        tile.InternalTextureId = internalTextureId;
                    }

                    _loadedTileSets[refTileSet.Source] = tileSet;
                }

                refTileSet.Set = tileSet;
            }

            _currentLevel = level;
            var spriteSheet = SpriteSheet.LoadSpriteSheet("player.json", "Assets", _renderer);
            if (spriteSheet != null)
            {
                _player = new PlayerObject(spriteSheet, 100, 100);
            }
            _renderer.SetWorldBounds(new Rectangle<int>(0, 0, _currentLevel.Width * _currentLevel.TileWidth,
                _currentLevel.Height * _currentLevel.TileHeight));
        }

        public void ProcessFrame()
        {
            if (_isPaused || _isGameOver) return; // Skip processing if paused or game over

            var currentTime = DateTimeOffset.Now;
            var secsSinceLastFrame = (currentTime - _lastUpdate).TotalSeconds;
            _lastUpdate = currentTime;

            bool up = _input?.IsUpPressed() ?? false;
            bool down = _input?.IsDownPressed() ?? false;
            bool left = _input?.IsLeftPressed() ?? false;
            bool right = _input?.IsRightPressed() ?? false;
            bool isAttacking = _input?.IsKeyFPressed() ?? false;
            bool addBomb = _input?.IsKeyGPressed() ?? false;

            _scriptEngine.ExecuteAll(this);

            if (_player == null) return;

            if (isAttacking)
            {
                var dir = up ? 1 : 0;
                dir += down ? 1 : 0;
                dir += left ? 1 : 0;
                dir += right ? 1 : 0;
                if (dir <= 1)
                {
                    _player.Attack(up, down, left, right);
                }
                else
                {
                    isAttacking = false;
                }
            }
            if (!isAttacking)
            {
                _player.UpdatePlayerPosition(up ? 1.0 : 0.0, down ? 1.0 : 0.0, left ? 1.0 : 0.0, right ? 1.0 : 0.0,
                    _currentLevel.Width * _currentLevel.TileWidth, _currentLevel.Height * _currentLevel.TileHeight,
                    secsSinceLastFrame);
            }
            var itemsToRemove = new List<int>();
            itemsToRemove.AddRange(GetAllTemporaryGameObjects().Where(gameObject => gameObject.IsExpired)
                .Select(gameObject => gameObject.Id).ToList());

            if (addBomb)
            {
                AddBomb(_player.Position.X, _player.Position.Y, false);
            }

            foreach (var gameObjectId in itemsToRemove)
            {
                var gameObject = _gameObjects[gameObjectId];
                if (gameObject is TemporaryGameObject tempObject)
                {
                    var deltaX = Math.Abs(_player.Position.X - tempObject.Position.X);
                    var deltaY = Math.Abs(_player.Position.Y - tempObject.Position.Y);
                    if (deltaX < 32 && deltaY < 32)
                    {
                        _player.GameOver();
                        _isGameOver = true;
                        ShowGameOverWindow();
                    }
                }
                _gameObjects.Remove(gameObjectId);
            }
        }

        public void RenderFrame()
        {
            _renderer.SetDrawColor(0, 0, 0, 255);
            _renderer.ClearScreen();

            if (_player != null)
            {
                _renderer.CameraLookAt(_player.Position.X, _player.Position.Y);
            }

            RenderTerrain();
            RenderAllObjects();

            _renderer.PresentFrame();
        }

        private Tile? GetTile(int id)
        {
            if (_currentLevel == null) return null;
            foreach (var tileSet in _currentLevel.TileSets)
            {
                foreach (var tile in tileSet.Set.Tiles)
                {
                    if (tile.Id == id)
                    {
                        return tile;
                    }
                }
            }

            return null;
        }

        private void RenderTerrain()
        {
            if (_currentLevel == null) return;
            for (var layer = 0; layer < _currentLevel.Layers.Length; ++layer)
            {
                var cLayer = _currentLevel.Layers[layer];

                for (var i = 0; i < _currentLevel.Width; ++i)
                {
                    for (var j = 0; j < _currentLevel.Height; ++j)
                    {
                        var cTileId = cLayer.Data[j * cLayer.Width + i] - 1;
                        var cTile = GetTile(cTileId);
                        if (cTile == null) continue;

                        var src = new Rectangle<int>(0, 0, cTile.ImageWidth, cTile.ImageHeight);
                        var dst = new Rectangle<int>(i * cTile.ImageWidth, j * cTile.ImageHeight, cTile.ImageWidth,
                            cTile.ImageHeight);

                        _renderer.RenderTexture(cTile.InternalTextureId, src, dst);
                    }
                }
            }
        }

        private IEnumerable<RenderableGameObject> GetAllRenderableObjects()
        {
            foreach (var gameObject in _gameObjects.Values)
            {
                if (gameObject is RenderableGameObject renderableGameObject)
                {
                    yield return renderableGameObject;
                }
            }
        }

        private IEnumerable<TemporaryGameObject> GetAllTemporaryGameObjects()
        {
            foreach (var gameObject in _gameObjects.Values)
            {
                if (gameObject is TemporaryGameObject temporaryGameObject)
                {
                    yield return temporaryGameObject;
                }
            }
        }

        private void RenderAllObjects()
        {
            foreach (var gameObject in GetAllRenderableObjects())
            {
                gameObject.Render(_renderer);
            }

            if (_player != null)
            {
                _player.Render(_renderer);
            }
        }

        public void AddBomb(int x, int y, bool translateCoordinates = true)
        {
            var translated = translateCoordinates ? _renderer.TranslateFromScreenToWorldCoordinates(x, y) : new Vector2D<int>(x, y);
            var spriteSheet = SpriteSheet.LoadSpriteSheet("bomb.json", "Assets", _renderer);
            if (spriteSheet != null)
            {
                spriteSheet.ActivateAnimation("Explode");
                TemporaryGameObject bomb = new(spriteSheet, 2.1, (translated.X, translated.Y));
                _gameObjects.Add(bomb.Id, bomb);
            }
        }

        private unsafe void ShowGameOverWindow()
        {
            // Initialize SDL_ttf
            if (_ttf.TTF_Init() == -1)
            {
                throw new InvalidOperationException("Failed to initialize SDL_ttf.");
            }

            // Load a font
            IntPtr font = _ttf.TTF_OpenFont("path/to/font.ttf", 24);
            if (font == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to load font.");
            }

            // Create a new SDL window for the Game Over message
            var gameOverWindow = _sdl.CreateWindow("Game Over", Sdl.WindowPosCentered, Sdl.WindowPosCentered, 300, 150, (uint)SDL_WindowFlags.SDL_WINDOW_SHOWN);

            // Create a renderer for the window
            var gameOverRenderer = _sdl.CreateRenderer(gameOverWindow, -1, (uint)SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

            // Clear the screen
            _sdl.SetRenderDrawColor(gameOverRenderer, 0, 0, 0, 255);
            _sdl.RenderClear(gameOverRenderer);

            // Create the "Game Over" text surface
            SDL_Color white = new SDL_Color { r = 255, g = 255, b = 255, a = 255 };
            var textSurface = _ttf.TTF_RenderText_Solid(font, "Game Over", white);
            if (textSurface == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create text surface.");
            }

            // Create texture from surface
            var textTexture = _sdl.CreateTextureFromSurface(gameOverRenderer, textSurface);
            _sdl.FreeSurface(textSurface);

            // Get the width and height of the texture
            _sdl.QueryTexture(textTexture, out _, out _, out int textWidth, out int textHeight);

            // Define the destination rectangle for the text
            SDL_Rect textRect = new SDL_Rect { x = 50, y = 20, w = textWidth, h = textHeight };

            // Render the text
            _sdl.RenderCopy(gameOverRenderer, textTexture, IntPtr.Zero, &textRect);
            _sdl.DestroyTexture(textTexture);

            // Draw the "OK" button
            SDL_Rect okButton = new SDL_Rect { x = 100, y = 80, w = 100, h = 40 };
            _sdl.SetRenderDrawColor(gameOverRenderer, 255, 255, 255, 255);
            _sdl.RenderFillRect(gameOverRenderer, &okButton);

            // Update the screen
            _sdl.RenderPresent(gameOverRenderer);

            // Event loop to handle the OK button click
            SDL_Event e;
            while (true)
            {
                while (_sdl.PollEvent(&e) != 0)
                {
                    if (e.type == SDL_EventType.SDL_QUIT)
                    {
                        _sdl.DestroyWindow(gameOverWindow);
                        _sdl.Quit();
                        Environment.Exit(0);
                    }

                    if (e.type == SDL_EventType.SDL_MOUSEBUTTONDOWN)
                    {
                        int mouseX = e.button.x;
                        int mouseY = e.button.y;

                        if (mouseX >= okButton.x && mouseX <= okButton.x + okButton.w && mouseY >= okButton.y && mouseY <= okButton.y + okButton.h)
                        {
                            _sdl.DestroyWindow(gameOverWindow);
                            _sdl.Quit();
                            Environment.Exit(0);
                        }
                    }
                }
            }
        }
    }
}
