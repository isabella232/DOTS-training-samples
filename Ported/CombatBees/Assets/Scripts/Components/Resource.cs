using Unity.Entities;

public struct Resource : IComponentData
{
    public float Speed;
    public Bee CarryingBee;
}