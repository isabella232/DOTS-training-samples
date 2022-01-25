using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public partial class CitySpawnerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var random = new Random(1337);

        Entities
            .ForEach((Entity entity, in CitySpawner spawner) =>
            {
                ecb.DestroyEntity(entity);

                for (int i = 0; i < spawner.NumberOfClusters; ++i)
                {
                    var cluster = ecb.CreateEntity();

                    // TODO: create a grid based on number of clusters -> assign grid mid position + radius to cluster so we don't have overlapping buildings
                    var position = new Vector3(
                        random.NextFloat(-spawner.CityWidth, spawner.CityWidth),
                        0f,
                        random.NextFloat(-spawner.CityLength, spawner.CityLength));
                    ecb.AddComponent(cluster, new Cluster
                    {
                        Position = position,
                    });
                    ecb.AddComponent(cluster, new ClusterGeneration
                    {
                        NumberOfSubClusters = random.NextInt(1, 5),
                        BarPrefab = spawner.BarPrefab,
                        MinTowerHeight = spawner.MinTowerHeight,
                        MaxTowerHeight = spawner.MaxTowerHeight,
                    });
                    
                    ecb.AddBuffer<Joint>(cluster);
                    ecb.AddBuffer<Connection>(cluster);
                    ecb.AddBuffer<Bar>(cluster);
                }
            }).Run();

        ecb.Playback(EntityManager);
        ecb.Dispose();
        ecb = new EntityCommandBuffer(Allocator.Temp);
        // TODO: use parallel writer
        //var parallelWriter = ecbOriginal.AsParallelWriter();
        
        Entities
            .WithAll<ClusterGeneration>()
            .ForEach((Entity entity, 
                //int entityInQueryIndex,
                DynamicBuffer<Joint> joints,
                DynamicBuffer<Connection> connections, 
                DynamicBuffer<Bar> bars, 
                in ClusterGeneration clusterData,
                in Cluster cluster) =>
            {
                var clusterPosition = cluster.Position;
                float spacing = 2f;
                float clusterSize = clusterData.NumberOfSubClusters * spacing;
                int structureStartIndex = 0;
                for (int c = 0; c < clusterData.NumberOfSubClusters; ++c)
                {
                    int height = random.NextInt(clusterData.MinTowerHeight, clusterData.MaxTowerHeight);
                    var pos = clusterPosition + new float3(
                        random.NextFloat(-clusterSize, clusterSize), 
                        0f, 
                        random.NextFloat(-clusterSize, clusterSize));
                    for (int h = 0; h < height; ++h)
                    {
                        var pos1 = new float3(pos.x + spacing, h * spacing, pos.z - spacing);
                        joints.Add(new Joint
                        {
                            Value = pos1,
                            OldPos = pos1,
                            IsAnchored = h == 0
                        });
                        var pos2 = new float3(pos.x - spacing, h * spacing, pos.z - spacing);
                        joints.Add(new Joint
                        {
                            Value = pos2,
                            OldPos = pos2,
                            IsAnchored = h == 0
                        });
                        
                        var pos3 = new float3(pos.x, h * spacing, pos.z + spacing);
                        joints.Add(new Joint
                        {
                            Value = pos3,
                            OldPos = pos3,
                            IsAnchored = h == 0
                        });
                    }
                
                    // horizontal bars
                    for (int j = structureStartIndex; j < joints.Length; j += 3)
                    {
                        CreateConnection(j, j + 1, joints, connections, ecb, clusterData.BarPrefab, bars, entity);
                        CreateConnection(j + 1, j + 2, joints, connections, ecb, clusterData.BarPrefab, bars, entity);
                        CreateConnection(j + 2, j, joints, connections, ecb, clusterData.BarPrefab, bars, entity);
                    }
                    // vertical bars
                    for (int j = structureStartIndex; j < joints.Length - 3; ++j)
                    {
                        CreateConnection(j, j + 3, joints, connections, ecb, clusterData.BarPrefab, bars, entity);
                    }
                    // cross bars
                    for (int j = structureStartIndex + 3; j < joints.Length; j += 3)
                    {
                        CreateConnection(j, j - 2, joints, connections, ecb, clusterData.BarPrefab, bars, entity);
                        CreateConnection(j, j - 1, joints, connections, ecb, clusterData.BarPrefab, bars, entity);
                        CreateConnection(j + 1, j - 3, joints, connections, ecb, clusterData.BarPrefab, bars, entity);
                        CreateConnection(j + 1, j - 1, joints, connections, ecb, clusterData.BarPrefab, bars, entity);
                        CreateConnection(j + 2, j - 3, joints, connections, ecb, clusterData.BarPrefab, bars, entity);
                        CreateConnection(j + 2, j - 2, joints, connections, ecb, clusterData.BarPrefab, bars, entity);
                    }

                    structureStartIndex = joints.Length;
                }
                    
                ecb.RemoveComponent<ClusterGeneration>(entity);
            }).Run();
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    static void CreateConnection(
        int j1, int j2, DynamicBuffer<Joint> joints, DynamicBuffer<Connection> connections, 
        EntityCommandBuffer ecb, Entity barPrefab, DynamicBuffer<Bar> bars, Entity me)
    {
        connections.Add(new Connection
        {
            J1 = j1, J2 = j2, OriginalLength = math.length(joints[j1].Value - joints[j2].Value),
        });
        
        var bar = ecb.Instantiate(barPrefab);
        ecb.AddComponent<BarVisualizer>(bar);
        ecb.AppendToBuffer(me, new Bar {Value=bar});
    }
}