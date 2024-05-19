using Silk.NET.SDL;

namespace TheAdventure
{
    public unsafe class Input
    {
        private Sdl _sdl;
        private GameWindow _gameWindow;
        private GameRenderer _renderer;
        private Engine _engine; // Reference to the Engine
        private IntPtr _keyboardState;
        private int _numKeys;
        private bool _lastSpaceState = false;

        byte[] _mouseButtonStates = new byte[(int)MouseButton.Count];

        public EventHandler<(int x, int y)>? OnMouseClick;

        public Input(Sdl sdl, GameWindow window, GameRenderer renderer, Engine engine)
        {
            _sdl = sdl;
            _gameWindow = window;
            _renderer = renderer;
            _engine = engine; // Initialize the Engine reference
            UpdateKeyboardState(); // Properly initialize keyboard state
        }

        public void SetEngine(Engine engine)
        {
            _engine = engine;
        }

        private void UpdateKeyboardState()
        {
            int numKeysLocal = 0; // Initialize with 0 to ensure it has a defined value
            unsafe
            {
                _keyboardState = (IntPtr)_sdl.GetKeyboardState((int*)&numKeysLocal);
            }
            _numKeys = numKeysLocal;
        }

        public void CheckInput()
        {
            UpdateKeyboardState();
            unsafe
            {
                ReadOnlySpan<byte> keys = new ReadOnlySpan<byte>((void*)_keyboardState, _numKeys);
                bool currentSpaceState = keys[(int)KeyCode.Space] == 1;
                bool wasJustPressed = currentSpaceState && !_lastSpaceState;

                if (wasJustPressed)
                {
                    _engine?.TogglePause(); // Toggle the pause state
                }

                _lastSpaceState = currentSpaceState; // Update the last known state
            }
        }

        public bool IsKeyFPressed()
        {
            ReadOnlySpan<byte> _keyboardStateSpan = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
            return _keyboardStateSpan[(int)KeyCode.F] == 1;
        }

        public bool IsKeyGPressed()
        {
            ReadOnlySpan<byte> _keyboardStateSpan = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
            return _keyboardStateSpan[(int)KeyCode.G] == 1;
        }

        public bool IsLeftPressed()
        {
            ReadOnlySpan<byte> _keyboardStateSpan = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
            return _keyboardStateSpan[(int)KeyCode.A] == 1;
        }

        public bool IsRightPressed()
        {
            ReadOnlySpan<byte> _keyboardStateSpan = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
            return _keyboardStateSpan[(int)KeyCode.D] == 1;
        }

        public bool IsUpPressed()
        {
            ReadOnlySpan<byte> _keyboardStateSpan = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
            return _keyboardStateSpan[(int)KeyCode.W] == 1;
        }

        public bool IsDownPressed()
        {
            ReadOnlySpan<byte> _keyboardStateSpan = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
            return _keyboardStateSpan[(int)KeyCode.S] == 1;
        }

        public bool ProcessInput()
        {
            Event ev = new Event();
            var mouseX = 0;
            var mouseY = 0;
            while (_sdl.PollEvent(ref ev) != 0)
            {
                if (ev.Type == (uint)EventType.Quit)
                {
                    return true;
                }

                switch (ev.Type)
                {
                    case (uint)EventType.Windowevent:
                        switch (ev.Window.Event)
                        {
                            case (byte)WindowEventID.Shown:
                            case (byte)WindowEventID.Exposed:
                                break;
                            case (byte)WindowEventID.Hidden:
                                break;
                            case (byte)WindowEventID.Moved:
                                break;
                            case (byte)WindowEventID.SizeChanged:
                                break;
                            case (byte)WindowEventID.Minimized:
                            case (byte)WindowEventID.Maximized:
                            case (byte)WindowEventID.Restored:
                                break;
                            case (byte)WindowEventID.Enter:
                                break;
                            case (byte)WindowEventID.Leave:
                                break;
                            case (byte)WindowEventID.FocusGained:
                                break;
                            case (byte)WindowEventID.FocusLost:
                                break;
                            case (byte)WindowEventID.Close:
                                break;
                            case (byte)WindowEventID.TakeFocus:
                                _sdl.SetWindowInputFocus(_sdl.GetWindowFromID(ev.Window.WindowID));
                                break;
                        }
                        break;

                    case (uint)EventType.Fingermotion:
                        break;

                    case (uint)EventType.Mousemotion:
                        break;

                    case (uint)EventType.Fingerdown:
                        _mouseButtonStates[(byte)MouseButton.Primary] = 1;
                        break;
                    case (uint)EventType.Mousebuttondown:
                        mouseX = ev.Motion.X;
                        mouseY = ev.Motion.Y;
                        _mouseButtonStates[ev.Button.Button] = 1;

                        if (ev.Button.Button == (byte)MouseButton.Primary)
                        {
                            OnMouseClick?.Invoke(this, (mouseX, mouseY));
                        }

                        break;

                    case (uint)EventType.Fingerup:
                        _mouseButtonStates[(byte)MouseButton.Primary] = 0;
                        break;

                    case (uint)EventType.Mousebuttonup:
                        _mouseButtonStates[ev.Button.Button] = 0;
                        break;

                    case (uint)EventType.Mousewheel:
                        break;

                    case (uint)EventType.Keyup:
                        break;

                    case (uint)EventType.Keydown:
                        break;
                }
            }

            return false;
        }
    }
}
