using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class DynamicSpawnerSystem : SystemBase
{
    EntityQuery m_CatQuery;
    private EntityCommandBufferSystem CommandBufferSystem;

    protected override void OnCreate()
    {
        CommandBufferSystem
            = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireSingletonForUpdate<GameStartedTag>();
        // Query to grab the cats (need max number of cats)
        m_CatQuery = GetEntityQuery(typeof(CatTag), typeof(GridPosition), typeof(CellOffset), typeof(Direction));
    }

    protected override void OnUpdate()
    {
        var ecb = CommandBufferSystem.CreateCommandBuffer();

        // Figure out number of cats
        var numberOfCats = m_CatQuery.CalculateEntityCount();

        // Need to access to cat and mouse prefabs
        var spawnerDefinition = GetSingleton<DynamicSpawnerDefinition>();

        var dt = Time.DeltaTime;

        Entities
            .ForEach((Entity entity, ref SpawnerData spawnerData) =>
            {
                spawnerData.Timer -= dt;

                if (spawnerData.Timer < 0 && !(spawnerData.Type == SpawnerType.CatSpawner && numberOfCats >= spawnerDefinition.MaxCats))
                {
                    spawnerData.Timer = spawnerData.Frequency;

                    // Choose the prefab
                    var prefab = spawnerDefinition.MousePrefab;
                    if (spawnerData.Type == SpawnerType.CatSpawner)
                    {
                        prefab = spawnerDefinition.CatPrefab;
                    }

                    // Create the prefab
                    var spawnedEntity = ecb.Instantiate(prefab);

                    // Set its components
                    var gridPosition = new GridPosition() { X = spawnerData.X, Y = spawnerData.Y };
                    var direction = new Direction() { Value = spawnerData.Direction };
                    var cellOffset = new CellOffset() { Value = 0.5f };
                    ecb.AddComponent(spawnedEntity, gridPosition);
                    ecb.AddComponent(spawnedEntity, direction);
                    ecb.AddComponent(spawnedEntity, cellOffset);
                }

            }).Schedule();
    }
}