using System.Diagnostics;
using Silk.NET.SDL;


namespace TheAdventure
{
    public static class Program
    {
        public static void Main()
        {
            var sdl = InitializeSDL();

            using (var window = CreateGameWindow(sdl))
            {
                var renderer = new GameRenderer(sdl, window);
                var input = new Input(sdl, window, renderer, null); // Temporarily pass null for Engine
                var engine = new Engine(renderer, input); // Pass Input instance to Engine
                input.SetEngine(engine); // Set the Engine instance in Input

                engine.InitializeWorld();

                RunGameLoop(input, engine);
            }

            sdl.Quit();
        }

        private static Sdl InitializeSDL()
        {
            var sdl = new Sdl(new SdlContext());

            var sdlInitResult = sdl.Init(Sdl.InitVideo | Sdl.InitEvents | Sdl.InitTimer | Sdl.InitGamecontroller | Sdl.InitJoystick);
            if (sdlInitResult < 0)
            {
                throw new InvalidOperationException("Failed to initialize SDL.");
            }

            return sdl;
        }

        private static GameWindow CreateGameWindow(Sdl sdl)
        {
            return new GameWindow(sdl, 800, 480);
        }

        private static void RunGameLoop(Input input, Engine engine)
        {
            bool quit = false;
            while (!quit)
            {
                quit = input.ProcessInput();
                if (quit) break;

                input.CheckInput(); // Check for pause input

                if (!engine.IsPaused())
                {
                    engine.ProcessFrame();
                }

                engine.RenderFrame();
            }
        }
    }
}