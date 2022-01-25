using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public partial class DestroyerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var inputReinitialize = Input.GetKeyDown(KeyCode.R);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        if (inputReinitialize)
        {
            Entities
                .WithAny<BeeTag, BloodTag, BeeBitsTag>()
                .WithAny<FoodTag>()
                .ForEach((Entity entity) =>
                {
                    ecb.DestroyEntity(entity);
                }).Run();
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}