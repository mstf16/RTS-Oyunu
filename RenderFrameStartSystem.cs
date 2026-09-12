using System;
using SDL2.Bindings;

namespace RTSProje
{
    // Sahne amirinin "perdeyi kapat" dediği an. Her karenin EN
    // BAŞINDA, tüm çizim sistemlerinden ÖNCE çalışması lazım.
    // Bunun için CoreEngine'e EN İLK bu sistemi kaydedeceğiz -
    // liste sırasına göre çalıştığı için, ilk kaydedilen ilk çalışır.
    public class RenderFrameStartSystem : ISystem
    {
        private readonly IntPtr _renderer;

        public RenderFrameStartSystem(IntPtr renderer)
        {
            _renderer = renderer;
        }

        public void Initialize()
        {
            // Abonelik yok, sadece kendi işini yapıyor.
        }

        public void Update(float deltaTime)
        {
            // Arka planı koyu bir renkle temizle. Bunu artık HİÇBİR
            // başka sistem yapmayacak - sahnede tek bir perde açıcı var.
            SDL.SDL_SetRenderDrawColor(_renderer, 20, 20, 30, 255);
            SDL.SDL_RenderClear(_renderer);
        }

        public void Shutdown()
        {
            // Kasıtlı olarak boş.
        }
    }
}