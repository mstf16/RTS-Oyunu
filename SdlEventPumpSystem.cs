using SDL2.Bindings;

namespace RTSProje
{
    // ============================================================
    // SdlEventPumpSystem.cs
    // ------------------------------------------------------------
    // GÖREVİ: SDL'in kendi olay kuyruğunu (pencere kapatma, klavye,
    // fare vb.) her karede okumak (SDL_PollEvent) ve bunları bizim
    // EventManager'ımız üzerinden oyunun geri kalanına iletmek.
    //
    // NEDEN AYRI BİR SİSTEM (SdlEventPumpSystem) VE NEDEN CoreEngine
    // İÇİNDE DEĞİL?
    // CoreEngine, SDL'den tamamen habersiz olmalı (SRP -- tek
    // sorumluluk: zamanlama ve sistem sırası). SDL'e özel kod
    // buraya, ayrı bir dosyaya izole edildi.
    // ============================================================
     public class SdlEventPumpSystem : ISystem
    {
        private readonly IntPtr _renderer;

        // Renderer'a ihtiyacımız var çünkü resize olduğunda gerçek
        // piksel boyutunu (SDL_GetRendererOutputSize) sorgulayacağız.
        public SdlEventPumpSystem(IntPtr renderer)
        {
            _renderer = renderer;
        }

        public void Initialize()
        {
            // Bu sistem sadece YAYINLIYOR, hiçbir şeye abone değil.
        }

        public void Update(float deltaTime)
        {
            while (SDL.SDL_PollEvent(out SDL.SDL_Event sdlEvent) != 0)
            {
                if (sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
                {
                    EventManager.Publish(new QuitRequestedEvent());
                }
                else if (sdlEvent.type == SDL.SDL_EventType.SDL_WINDOWEVENT &&
                         sdlEvent.window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED)
                {
                    SDL.SDL_GetRendererOutputSize(_renderer, out int width, out int height);
                    EventManager.Publish(new WindowResizedEvent { NewWidth = width, NewHeight = height });
                }
            }
        }

        public void Shutdown()
        {
            // Kasıtlı olarak boş.
        }
    }
}