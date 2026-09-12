using System;

namespace RTSProje
{
    // ============================================================
    // EntityHandle — artık "kimlik" tek bir int değil, bir struct.
    // Index: dizideki yer. Version: bu yer kaçıncı kez kullanıldı.
    // ============================================================
    public readonly struct EntityHandle : IEquatable<EntityHandle>
    {
        public readonly int Index;
        public readonly int Version;

        public EntityHandle(int index, int version)
        {
            Index = index;
            Version = version;
        }

        public static readonly EntityHandle Invalid = new EntityHandle(-1, -1);

        public bool IsValid => Index >= 0;

        public bool Equals(EntityHandle other) => Index == other.Index && Version == other.Version;
        public override bool Equals(object? obj) => obj is EntityHandle other && Equals(other);
        public override int GetHashCode() => (Index, Version).GetHashCode();
    }
}