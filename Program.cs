using System;
using SDL2.Bindings;

namespace RTSProje
{
    // ============================================================
    // Program.cs
    // ------------------------------------------------------------
    // Burası binanın ana giriş kapısı.
    // Oyuncu düğmeye bastığında ilk burası uyanır.
    // Kullanıcıya harita boyutunu sorar, pencereyi açar,
    // ustaları (GridManager, EventPump, Render) sıraya dizer,
    // sonra da ana motorun şalterini kaldırır.
    // ============================================================
    public class Program
    {
        // Standart ekran eni boyu (16:9 geniş ekran)
        private const int DefaultWindowWidth = 1280;
        private const int DefaultWindowHeight = 720;

        private static CoreEngine? _engine;

        public static void Main(string[] args)
        {
            // 1. ADIM: Oyuncuya haritayı ne büyüklükte kuracağını sor
            int gridWidth = AskForDimension("Grid genişliği (kaç kare arsa olsun)");
            int gridHeight = AskForDimension("Grid yüksekliği (kaç kare arsa olsun)");

            // 2. ADIM: SDL grafik motorunu uyandır, pencereyi aç
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                Console.WriteLine($"Ekran motoru açılamadı! Hata: {SDL.SDL_GetError()}");
                return;
            }

            IntPtr window = SDL.SDL_CreateWindow(
                "RTS Projesi",
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                DefaultWindowWidth,
                DefaultWindowHeight,
                SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE
            );

            if (window == IntPtr.Zero)
            {
                Console.WriteLine($"Pencere açılamadı! Hata: {SDL.SDL_GetError()}");
                SDL.SDL_Quit();
                return;
            }

            // Donanım hızlandırmalı (ekran kartını kullanan) fırçayı (renderer) al
            IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
            if (renderer == IntPtr.Zero)
            {
                Console.WriteLine($"Fırça oluşturulamadı! Hata: {SDL.SDL_GetError()}");
                SDL.SDL_DestroyWindow(window);
                SDL.SDL_Quit();
                return;
            }

            // Ekran kartının bize verdiği gerçek çizim alanını öğren
            SDL.SDL_GetRendererOutputSize(renderer, out int actualWidth, out int actualHeight);

            // 3. ADIM: Ustaları şantiyeye kaydet
            var gridManager = new GridManager(gridWidth, gridHeight, actualWidth, actualHeight);
            var sdlEventPump = new SdlEventPumpSystem(renderer);
            var tempGridRenderer = new TempGridLineRenderSystem(renderer, gridManager);

            _engine = new CoreEngine();
            _engine.RegisterSystem(new RenderFrameStartSystem(renderer));  // EN BAŞTA - perdeyi kapat
            _engine.RegisterSystem(gridManager);     // 1. Kadastro ustası
            _engine.RegisterSystem(sdlEventPump);    // 2. Klavye/fare dinleyen bekçi
            _engine.RegisterSystem(tempGridRenderer); // 3. Izgara çizgilerini çizen boyacı
            _engine.RegisterSystem(new RenderFrameEndSystem(renderer));     // EN SONDA - perdeyi aç
            // Oyuncu pencereyi sağ üstteki çarpıdan kapatırsa motoru durdur
            EventManager.Subscribe<QuitRequestedEvent>(OnQuitRequested);

            Console.WriteLine($"Oyun başlıyor: {gridWidth}x{gridHeight} arsa, {actualWidth}x{actualHeight} ekran alanı.");

            // 4. ADIM: Şalteri kaldır! Pencere kapanana kadar oyun burada akar
            _engine.Start();

            // 5. ADIM: Kapanış temizliği (geride çöp bırakma)
            EventManager.Unsubscribe<QuitRequestedEvent>(OnQuitRequested);
            SDL.SDL_DestroyRenderer(renderer);
            SDL.SDL_DestroyWindow(window);
            SDL.SDL_Quit();

            Console.WriteLine("Dükkan kapandı, herkes evine gitti.");
        }

        // Çarpıya basılınca telsizden gelen anonsu yakalayıp şalteri indiren metod
        private static void OnQuitRequested(QuitRequestedEvent e)
        {
            _engine?.Stop();
        }

        // Kullanıcı geçerli bir sayı girene kadar inatla soran bakkal kafası metod
        private static int AskForDimension(string prompt)
        {
            while (true)
            {
                Console.Write($"{prompt}: ");
                string? input = Console.ReadLine();

                if (int.TryParse(input, out int result) && result > 0 && result <= 256)
                {
                    return result;
                }

                Console.WriteLine("Olmadı! 1 ile 256 arasında aklı başında bir sayı gir.");
            }
        }
    }
}