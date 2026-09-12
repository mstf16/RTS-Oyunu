using System;
using System.Collections.Generic;

// EventManager.cs
// ------------------------------------------------------------
// Burası şantiyenin megafonu (telsiz sistemi).
// Biri kaza geçirdiğinde gidip şantiye şefinin yakasına yapışmaz.
// Megafondan "3. blokta kaza var!" diye anons geçer (Publish).
// İlk yardımla kim ilgileniyorsa kulak kesilip koşar (Subscribe).
// Böylece kimse kimseyi tanımak, kimseye el pençe durmak zorunda kalmaz.
//
// Neden listelerle, sözlüklerle boğuşmuyoruz?
// Normalde her anonsta liste karıştırıp "kim dinliyor" diye baksan
// ortalık çöplüğe döner. Burada C#'ın kendi telsiz kablosunu
// (multicast delegate) bağladık; sinyal fişek gibi tek hamlede
// dinleyen herkese ulaşır, arkasında zerre kadar çöp (GC) bırakmaz.
// 


namespace RTSProje
{
    public static class EventManager
    {
        private static readonly List<Action> _clearActions = new List<Action>();

        // Her farklı olay türü için ayrı, bağımsız bir "hat" -
        // sözlükte arama yapmak yerine doğrudan o hatta bağlanıyoruz.
        private static class EventChannel<T> where T : struct
        {
            public static Action<T>? OnEvent;

            static EventChannel()
            {
                lock (_clearActions)
                {
                    _clearActions.Add(() => OnEvent = null);
                }
            }
        }

        // SUBSCRIBE (Kulağını Aç / Abone Ol)
        // "Usta, şu olay olursa bana da haber uçur" deme şekli.
        // Örnek: EventManager.Subscribe<UnitMovedEvent>(OnUnitMoved);
        public static void Subscribe<T>(Action<T> listener) where T : struct
        {
            if (listener == null) return;
            EventChannel<T>.OnEvent += listener;
        }

        // UNSUBSCRIBE (Abonelikten Çık)
        // Adam öldü ya da bina yıkıldı; artık megafondan gelen anonsları
        // dinlemesine lüzum yok. Hattı koparmazsak bellek şişer.

        public static void Unsubscribe<T>(Action<T> listener) where T : struct
        {
            if (listener == null) return;
            EventChannel<T>.OnEvent -= listener;
        }

        // PUBLISH (Megafondan Anons Geç)
        // "Dikkat! Falanca olay gerçekleşti!" diyerek haberi salar.
        public static void Publish<T>(T gameEvent) where T : struct
        {
            EventChannel<T>.OnEvent?.Invoke(gameEvent);
        }

        // CLEAR ALL (Santrali Sıfırla)
        // Oyun baştan başlarken ya da harita değişirken masayı
        // tamamen temizleyip sıfırdan başlamak için.

        public static void ClearAll()
        {
            lock (_clearActions)
            {
                for (int i = 0; i < _clearActions.Count; i++)
                {
                    _clearActions[i]();
                }
            }
        }
    }
}