namespace RTSProje
{
    // ============================================================
    // WindowResizedEvent.cs (Oda Genişletildi Anonsu)
    // ------------------------------------------------------------
    // Düğün salonunun aradaki sürgülü kapılarını açıp salonu
    // büyüttüğünü düşün. Garsonlara hemen "masaların yerini yeni
    // sınırlara göre tekrar dizin" diye anons geçmen gerekir.
    //
    // Neden Pencere Ölçüsü Değil de Renderer Ölçüsü?
    // 4K monitörlerde ya da Windows %125 ölçeklemede pencere boyu ile
    // ekran kartının gerçek fırça alanı birbirini tutmaz.
    // O yüzden buraya ekrandaki net piksel sayısını (NewWidth / NewHeight)
    // koyuyoruz ki haritacı (GridManager) parselleri milimi milimine oturtsun.
    // ============================================================
    public struct WindowResizedEvent
    {
        public int NewWidth;
        public int NewHeight;
    }
}