namespace RTSProje
{
    // ============================================================
    // QuitRequestedEvent.cs (Dükkanı Kapat / Şalteri İndir Sinyali)
    // ------------------------------------------------------------
    // Yangın alarm butonuna basmak gibi düşün. Butonun üstünde
    // uzun uzun mektup yazmaz; sadece düğmeye basılması bile
    // "herkes işi gücü bıraksın, binayı boşaltıyoruz" demektir.
    //
    // Neden İçi Boş Struct?
    // Bellekte tek bir bayt bile yer tutmaz. EventManager struct
    // şartı koştuğu için en hafif, sıfır maliyetli telsiz sinyali
    // budur. Motora "oyuncu çarpıya bastı, toparlanın" der.
    // ============================================================
    public struct QuitRequestedEvent { }
}