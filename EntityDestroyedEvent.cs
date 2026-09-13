namespace RTSProje
{
    // Bir müşteri otelden ayrıldığında resepsiyonun megafondan
    // geçtiği anons: "101 numaralı oda boşaldı!" Bu anonsu duyan
    // her sistem (LogisticsConfidence, BuildingStateMonitor vb.)
    // kendi defterinden o kaydı hemen siler - aylarca birikmiş
    // ölü kayıt taramaya (Prune) hiç gerek kalmaz.
    public struct EntityDestroyedEvent
    {
        public EntityHandle Handle;
    }
}