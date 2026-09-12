using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace RTSProje
{
    // ============================================================
    // CoreEngine.cs
    // ------------------------------------------------------------
    // Burası fabrikanın ana şalteri ve duvar saati.
    // Herkesin bilgisayarı canavar gibi değil; biri saniyede 60 kare
    // çizerken öbürü 240 kare çizer. Eğer oyunu saatin akışına
    // bağlamazsan, parayı basıp güçlü ekran kartı alan adamın askeri
    // 4 kat hızlı koşar, oyunun adaleti şaşar!
    //
    // CoreEngine ne yapar?
    // 1. İki göz kırpması arasında geçen saliseyi (DeltaTime) ölçer.
    // 2. Herkese "sen bu kadar sürede ne yapacaksan yap" der.
    // 3. Oyun 60 FPS'i geçip ekran kartının fanlarını bağırtmasın
    //    diye işlemciyi milisaniyelik dinlendirir.
    // ============================================================


    // ------------------------------------------------------------
    // ISYSTEM (Usta Sözleşmesi)
    // Şantiyeye girecek her ustanın (Hareket sistemi, Savaş sistemi vb.)
    // cebinde bu sözleşme olmak zorunda:
    // - İşe başlarken ne yapacaksın? (Initialize)
    // - Her saniye ne yapacaksın? (Update)
    // - Paydos olunca ortalığı nasıl toplayacaksın? (Shutdown)
    // ------------------------------------------------------------
    public interface ISystem
    {
        void Initialize();
        void Update(float deltaTime);
        void Shutdown();
    }


    public class CoreEngine
    {
        public bool IsRunning { get; private set; } // Çark dönüyor mu?
        public bool IsPaused { get; set; }          // Maçı dondurduk mu?
        public float TimeScale { get; set; } = 1.0f; // 1x normal hız, 2x hızlı çekim (test için birebir)
        public int TargetFPS { get; set; } = 60;     // Hedeflediğimiz hız (saniyede 60 kare)

        // Şantiyede çalışan ustaların sırası
        private readonly List<ISystem> _systems = new List<ISystem>();

        // Kronometremiz: Mikrosaniye kaçırmayan en hassas saatimiz
        private readonly Stopwatch _frameStopwatch = new Stopwatch();


        // ------------------------------------------------------------
        // REGISTER SYSTEM (Ustayı İşe Al)
        // Sıraya kim önce girerse işini önce o yapar.
        // ------------------------------------------------------------
        public void RegisterSystem(ISystem system)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system), "Boş ustayı işe alamazsın!");
            }

            if (!_systems.Contains(system))
            {
                _systems.Add(system);
            }
        }


        // ------------------------------------------------------------
        // UNREGISTER SYSTEM (Ustayı Gönder)
        // Artık bu sisteme ihtiyaç yoksa toparlanıp gitsin.
        // ------------------------------------------------------------
        public void UnregisterSystem(ISystem system)
        {
            if (system != null && _systems.Contains(system))
            {
                system.Shutdown();
                _systems.Remove(system);
            }
        }


        // ------------------------------------------------------------
        // START (Şalteri Kaldır)
        // Önce herkes alet çantasını açar (Initialize), sonra çark dönmeye başlar.
        // ------------------------------------------------------------
        public void Start()
        {
            if (IsRunning) return;

            IsRunning = true;

            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Initialize();
            }

            RunGameLoop();
        }


        // ------------------------------------------------------------
        // STOP (Paydos)
        // Şalteri indirir ve herkesin dükkanı kilitlemesini sağlar.
        // ------------------------------------------------------------
        public void Stop()
        {
            IsRunning = false;

            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Shutdown();
            }
        }


        // ------------------------------------------------------------
        // RUN GAME LOOP (Kalbin Attığı Yer)
        // ------------------------------------------------------------
        private void RunGameLoop()
        {
            _frameStopwatch.Start();
            long previousTicks = _frameStopwatch.ElapsedTicks;

            while (IsRunning)
            {
                long currentTicks = _frameStopwatch.ElapsedTicks;
                
                // İki kare arasında ne kadar göz kırptık (saniye cinsinden)
                float rawDeltaTime = (float)(currentTicks - previousTicks) / Stopwatch.Frequency;
                previousTicks = currentTicks;

                // Bilgisayar anlık takılırsa (lag spike) askerler duvardan geçip
                // ışınlanmasın diye süreyi en fazla 0.1 saniye ile dizginliyoruz.
                if (rawDeltaTime > 0.1f)
                {
                    rawDeltaTime = 0.1f;
                }

                // Oyun duraklatıldıysa zaman akmaz (0 olur), durmadıysa çarpanla yürür
                float finalDeltaTime = IsPaused ? 0f : (rawDeltaTime * TimeScale);

                // Ustaları sırayla dürtüyoruz: "Hadi işini yap"
                for (int i = 0; i < _systems.Count; i++)
                {
                    _systems[i].Update(finalDeltaTime);
                }

                // Windows'un uyku sersemliğini (15ms sapma) alt eden akıllı fren:
                // Hedeflenen süreden çok gerideysek işlemciyi dinlendir,
                // son saliselerde ise tetikte bekle ki 60 FPS milimi milimine tutsun.
                if (TargetFPS > 0)
                {
                    long targetFrameTicks = Stopwatch.Frequency / TargetFPS;
                    long frameEndTarget = currentTicks + targetFrameTicks;

                    while (_frameStopwatch.ElapsedTicks < frameEndTarget)
                    {
                        long remainingTicks = frameEndTarget - _frameStopwatch.ElapsedTicks;
                        double remainingMs = (double)remainingTicks / Stopwatch.Frequency * 1000.0;

                        if (remainingMs > 2.0)
                        {
                            Thread.Sleep(1);
                        }
                        else
                        {
                            Thread.Yield(); // İnce ayar, titremeyi sıfırlar
                        }
                    }
                }
            }

            _frameStopwatch.Stop();
        }
    }
}
