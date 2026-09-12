using System;
using SDL2.Bindings;

namespace RTSProje
{
    // ============================================================
    // TempGridLineRenderSystem.cs (GEÇİCİ -- Faz 6'da SpriteRenderer
    // gelince bu dosya tamamen silinecek)
    // ------------------------------------------------------------
    // GÖREVİ: Sadece GridManager'ın hesapladığı hücre boyutlarının
    // gerçekten ekrana doğru oturduğunu GÖZLE doğrulamak için,
    // grid çizgilerini ekrana çizer.
    // ============================================================
    public class TempGridLineRenderSystem : ISystem
    {
        private readonly IntPtr _renderer;
        private readonly GridManager _gridManager;

        public TempGridLineRenderSystem(IntPtr renderer, GridManager gridManager)
        {
            _renderer = renderer;
            _gridManager = gridManager;
        }

        public void Initialize()
        {
            // Abonelik yok, sadece çizim yapıyor.
        }

        public void Update(float deltaTime)
        {

            // Grid çizgilerini çiz (açık gri).
            SDL.SDL_SetRenderDrawColor(_renderer, 80, 80, 90, 255);

            for (int x = 0; x <= _gridManager.GridWidth; x++)
            {
                int pixelX = (int)(x * _gridManager.CellPixelWidth);
                SDL.SDL_RenderDrawLine(_renderer, pixelX, 0, pixelX, (int)(_gridManager.GridHeight * _gridManager.CellPixelHeight));
            }

            for (int y = 0; y <= _gridManager.GridHeight; y++)
            {
                int pixelY = (int)(y * _gridManager.CellPixelHeight);
                SDL.SDL_RenderDrawLine(_renderer, 0, pixelY, (int)(_gridManager.GridWidth * _gridManager.CellPixelWidth), pixelY);
            }

        }

        public void Shutdown()
        {
            // Kasıtlı olarak boş.
        }
    }
}