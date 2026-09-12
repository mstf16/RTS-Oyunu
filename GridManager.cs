using System;

namespace RTSProje
{
    // ============================================================
    // GridManager.cs
    // ------------------------------------------------------------
    // Burası harita kadastro müdürlüğü.
    // Dünyayı satranç tahtası gibi karelere böler (örneğin 64x64).
    //
    // Neden tek boyutlu dizi (_occupancy) kullanıyoruz?
    // 2 boyutlu dizi (dizi[x,y]) bellekte dağınık durur, işlemci
    // hücreleri ararken nefes nefese kalır.
    // Tek boyutlu dizi ise tarladaki karıklar gibidir: 1. sıra biter,
    // hemen ucuna 2. sıra eklenir. `y * genişlik + x` formülüyle
    // aradığın kareyi gözün kapalı pat diye bulursun.
    // ============================================================

    public class GridManager : ISystem
    {
        public int GridWidth { get; private set; }   // Kaç sütun arsa var?
        public int GridHeight { get; private set; }  // Kaç satır arsa var?

        public float CellPixelWidth { get; private set; }  // Ekranda bir arsa kaç piksel eninde?
        public float CellPixelHeight { get; private set; } // Ekranda bir arsa kaç piksel boyunda?

        private int _currentScreenWidth;
        private int _currentScreenHeight;

        // Arsalarda kim oturuyor defteri (Her arsada bir EntityHandle yatar)
        private readonly EntityHandle[] _occupancy;

        private bool _isInitialized;

        public GridManager(int gridWidth, int gridHeight, int initialScreenWidth, int initialScreenHeight)
        {
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            _currentScreenWidth = initialScreenWidth;
            _currentScreenHeight = initialScreenHeight;

            _occupancy = new EntityHandle[GridWidth * GridHeight];
            Array.Fill(_occupancy, EntityHandle.Invalid); // Başta tüm arsalar bomboş

            RecalculateCellSize();
        }

        public void Initialize()
        {
            // Pencere büyürse küçülürse haberimiz olsun, arsaları ekrana yeniden pay edelim
            EventManager.Subscribe<WindowResizedEvent>(OnWindowResized);
            _isInitialized = true;
        }

        public void Update(float deltaTime)
        {
            // GridManager amele değil, haritacıdır. Kendi kendine koşturmaz;
            // birisi "bu arsa boş mu" diye sorduğunda cevap verir.
        }

        public void Shutdown()
        {
            EventManager.Unsubscribe<WindowResizedEvent>(OnWindowResized);
        }

        // Pencere esnetilince arsaların ekrandaki piksel boyunu yeniden biçiyoruz
        private void OnWindowResized(WindowResizedEvent e)
        {
            _currentScreenWidth = e.NewWidth;
            _currentScreenHeight = e.NewHeight;
            RecalculateCellSize();
        }

        private void RecalculateCellSize()
        {
            CellPixelWidth = (float)_currentScreenWidth / GridWidth;
            CellPixelHeight = (float)_currentScreenHeight / GridHeight;
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("[HATA] Dükkanı açmadan (Initialize) haritadan arsa sorgulayamazsın!");
            }
        }

        // ------------------------------------------------------------
        // KOORDİNAT DÖNÜŞÜMLERİ
        // Arsa numarasından ekrandaki piksele, pikselden arsa numarasına geçiş.
        // ------------------------------------------------------------
        public void GridToPixel(int gridX, int gridY, out float pixelX, out float pixelY)
        {
            pixelX = gridX * CellPixelWidth;
            pixelY = gridY * CellPixelHeight;
        }

        public void PixelToGrid(float pixelX, float pixelY, out int gridX, out int gridY)
        {
            gridX = (int)(pixelX / CellPixelWidth);
            gridY = (int)(pixelY / CellPixelHeight);
        }

        // Sınırın dışına, harita dışındaki uçuruma taştık mı?
        public bool IsWithinBounds(int gridX, int gridY)
        {
            return gridX >= 0 && gridX < GridWidth && gridY >= 0 && gridY < GridHeight;
        }

        private int ToIndex(int gridX, int gridY)
        {
            return gridY * GridWidth + gridX;
        }

        // ------------------------------------------------------------
        // IS CELL BLOCKED (Bu Arsa Dolu Mu?)
        // Arsa harita dışındaysa zaten geçilemez.
        // İçerde biri varsa da otel odası kontrolü yapıyoruz: Adam öldüyse
        // arsa boşalmış demektir!
        // ------------------------------------------------------------
        public bool IsCellBlocked(int gridX, int gridY)
        {
            EnsureInitialized();
            if (!IsWithinBounds(gridX, gridY)) return true;

            EntityHandle occupant = _occupancy[ToIndex(gridX, gridY)];
            return occupant.IsValid && World.IsAlive(occupant);
        }

        // Arsadaki kiracının kimlik kartını ver
        public EntityHandle GetOccupant(int gridX, int gridY)
        {
            EnsureInitialized();
            if (!IsWithinBounds(gridX, gridY)) return EntityHandle.Invalid;

            EntityHandle occupant = _occupancy[ToIndex(gridX, gridY)];
            return World.IsAlive(occupant) ? occupant : EntityHandle.Invalid;
        }

        // ------------------------------------------------------------
        // PLACE ENTITY (Askeri Arsaya Kondur)
        // ------------------------------------------------------------
        public bool PlaceEntity(EntityHandle entity, int gridX, int gridY)
        {
            EnsureInitialized();
            if (!IsWithinBounds(gridX, gridY)) return false;
            if (!World.IsAlive(entity)) return false; // Mezardaki adamı arsaya dikemezsin

            if (IsCellBlocked(gridX, gridY)) return false; // Arsa zaten kapılmış

            CollisionFlag[] collisionFlags = World.GetArray<CollisionFlag>();
            if (collisionFlags[entity.Index].IsBlocking)
            {
                _occupancy[ToIndex(gridX, gridY)] = entity;
            }

            return true;
        }

        // Asker oradan ayrılınca arsayı boşa çıkar
        public void RemoveEntity(EntityHandle entity, int gridX, int gridY)
        {
            EnsureInitialized();
            if (!IsWithinBounds(gridX, gridY)) return;

            int index = ToIndex(gridX, gridY);
            if (_occupancy[index].Equals(entity))
            {
                _occupancy[index] = EntityHandle.Invalid;
            }
        }

        // ------------------------------------------------------------
        // BİNA TABANI YERLEŞTİRME (Çok Kareli Arsalar)
        // Kışla gibi 2x2 veya 3x3 devasa binalar birden fazla arsayı kaplar.
        // Tek bir karesi bile doluysa inşaata izin verilmez.
        // ------------------------------------------------------------
        public bool PlaceBuildingFootprint(EntityHandle entity, int originGridX, int originGridY, int footprintWidth, int footprintHeight)
        {
            EnsureInitialized();
            if (!World.IsAlive(entity)) return false;

            // Önce tüm arsaları teker teker kolaçan et
            for (int y = 0; y < footprintHeight; y++)
            {
                for (int x = 0; x < footprintWidth; x++)
                {
                    int checkX = originGridX + x;
                    int checkY = originGridY + y;
                    if (!IsWithinBounds(checkX, checkY)) return false;
                    if (IsCellBlocked(checkX, checkY)) return false;
                }
            }

            // Hepsi temizse tapuyu binanın üstüne yap
            for (int y = 0; y < footprintHeight; y++)
            {
                for (int x = 0; x < footprintWidth; x++)
                {
                    _occupancy[ToIndex(originGridX + x, originGridY + y)] = entity;
                }
            }

            return true;
        }

        // Bina yıkılınca kapladığı tüm arsaları temizle
        public void RemoveBuildingFootprint(EntityHandle entity, int originGridX, int originGridY, int footprintWidth, int footprintHeight)
        {
            EnsureInitialized();

            for (int y = 0; y < footprintHeight; y++)
            {
                for (int x = 0; x < footprintWidth; x++)
                {
                    int removeX = originGridX + x;
                    int removeY = originGridY + y;
                    if (!IsWithinBounds(removeX, removeY)) continue;

                    int index = ToIndex(removeX, removeY);
                    if (_occupancy[index].Equals(entity))
                    {
                        _occupancy[index] = EntityHandle.Invalid;
                    }
                }
            }
        }
    }
}