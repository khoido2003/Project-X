using System;

[Serializable]
public struct EntityId : IEquatable<EntityId>
{
    public readonly int Id;

    public EntityId(int id) => Id = id;

    public bool Equals(EntityId other) => Id == other.Id;

    public override bool Equals(object obj) => obj is EntityId other && Equals(other);

    public override int GetHashCode() => Id;

    public override string ToString() => $"Entity[{Id}]";

    public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);

    public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);
}
