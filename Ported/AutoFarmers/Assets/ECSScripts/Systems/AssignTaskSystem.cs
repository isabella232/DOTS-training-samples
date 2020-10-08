﻿using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditorInternal;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class AssignTaskSystem : SystemBase
{
    Random m_Random;

    EntityQuery cropsQuery;

    protected override void OnCreate()
    {
        m_Random = new Random(666);

        cropsQuery = GetEntityQuery(typeof(Crop), ComponentType.Exclude<Assigned>());
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var ecbWriter = ecb.AsParallelWriter();
        NativeArray<Entity> crops = cropsQuery.ToEntityArray(Allocator.TempJob);

        // Loop over all idle farmers, assigning a pickup crop task
        Entities.
            WithName("assign_pickup_crop_task").
            WithAll<Farmer>().
            WithNone<DropOffCropTask>().
            WithNone<PickUpCropTask>().
            WithReadOnly(crops).
            WithDisposeOnCompletion(crops).
            ForEach(
            (Entity farmerEntity, int entityInQueryIndex, in Position farmerPos) =>
            {
                // Find nearest crop
                float minDistSq = float.MaxValue;
                Entity nearestCropEntity = Entity.Null;
                float2 nearestCropPos = float2.zero;
                for (int i = 0; i < crops.Length; i++)
                {
                    Translation cropTranslation = GetComponent<Translation>(crops[i]);
                    float2 cropPos = new float2(cropTranslation.Value.x, cropTranslation.Value.z);
                    float distSq = math.distancesq(cropPos, farmerPos.Value);
                    if (minDistSq > distSq)
                    {
                        minDistSq = distSq;
                        nearestCropEntity = crops[i];
                        nearestCropPos = cropPos;
                    }
                }

                if (nearestCropEntity != Entity.Null)
                {
                    ecbWriter.AddComponent<PickUpCropTask>(entityInQueryIndex, farmerEntity);
                    ecbWriter.AddComponent<TargetEntity>(entityInQueryIndex, farmerEntity);
                    ecbWriter.SetComponent(entityInQueryIndex, farmerEntity, new TargetEntity { target = nearestCropEntity, targetPosition = nearestCropPos });
                    ecbWriter.AddComponent<NeedsDeduplication>(entityInQueryIndex, farmerEntity);
                }

            }).ScheduleParallel();

        Dependency.Complete();
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

}