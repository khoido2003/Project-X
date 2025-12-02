using System;
using Unity.Netcode;
using UnityEngine;

public struct NetworkTransformState : INetworkSerializable
{
    public Vector3 Position;
    public Quaternion Rotation;
    public uint Tick;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Rotation);
        serializer.SerializeValue(ref Tick);
    }
}

public struct NetworkHealthState : INetworkSerializable
{
    public float Current;
    public float Max;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Current);
        serializer.SerializeValue(ref Max);
    }
}

public struct NetworkMovementState : INetworkSerializable
{
    public Vector3 MoveDirection;
    public bool IsMoving;
    public bool IsGrounded;
    public bool IsStunned;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref MoveDirection);
        serializer.SerializeValue(ref IsMoving);
        serializer.SerializeValue(ref IsGrounded);
        serializer.SerializeValue(ref IsStunned);
    }
}

public struct ClientInputState : INetworkSerializable
{
    public uint Tick;
    public Vector2 MoveInput;
    public bool AttackPressed;
    public int SkillIndex;
    public bool SkillPressed;
    public Vector3 MouseWorldPos;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref MoveInput);
        serializer.SerializeValue(ref AttackPressed);
        serializer.SerializeValue(ref SkillIndex);
        serializer.SerializeValue(ref SkillPressed);
        serializer.SerializeValue(ref MouseWorldPos);
    }
}
