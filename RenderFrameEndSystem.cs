using System;
using SDL2.Bindings;

namespace RTSProje
{
    // Sahne amirinin "perdeyi aç, seyirciye göster" dediği an. Her
    // karenin EN SONUNDA, tüm çizim sistemlerinden SONRA çalışması
    // lazım. Bunun için CoreEngine'e EN SON bu sistemi kaydedeceğiz.
    public class RenderFrameEndSystem : ISystem
    {
        private readonly IntPtr _renderer;

        public RenderFrameEndSystem(IntPtr renderer)
        {
            _renderer = renderer;
        }

        public void Initialize()
        {
            // Abonelik yok.
        }

        public void Update(float deltaTime)
        {
            // O ana kadar çizilen her şeyi (grid, ileride birimler,
            // binalar) tek seferde ekrana bas.
            SDL.SDL_RenderPresent(_renderer);
        }

        public void Shutdown()
        {
            // Kasıtlı olarak boş.
        }
    }
}