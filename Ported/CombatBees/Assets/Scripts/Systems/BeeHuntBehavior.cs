using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class BeeHuntBehavior : SystemBase
{
    EndSimulationEntityCommandBufferSystem ecbs;

    protected override void OnCreate()
    {
        ecbs = World
            .GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var globalDataEntity = GetSingletonEntity<GlobalData>();
        var globalData = GetComponent<GlobalData>(globalDataEntity);
        var beeDefinitions = GetBuffer<TeamDefinition>(globalDataEntity);

        var frameCount = UnityEngine.Time.frameCount +1;
        
        var dt = Time.DeltaTime;
        
        var ecb = ecbs.CreateCommandBuffer();

        Entities
            .WithAll<BeeHuntMode>()
            .WithNone<Ballistic, Decay>()
            .WithReadOnly(beeDefinitions)
            .WithNativeDisableContainerSafetyRestriction(beeDefinitions)
            .ForEach((Entity entity, ref Bee myself, ref BeeHuntMode huntMode, in Translation position, in TeamID team) =>
                {
                    var teamDef = beeDefinitions[team.Value];
                    huntMode.timeHunting += dt;

                    var timedOut = huntMode.timeHunting > teamDef.huntTimeout;
                    var cancelHunt = timedOut;

                    if (!timedOut)
                    {
                        var targeted = GetComponent<TargetedBy>(myself.TargetEntity);
                        cancelHunt = targeted.Value != entity;
                    }

                    if (cancelHunt)
                    {
                        ecb.RemoveComponent<BeeHuntMode>(entity);
                        ecb.AddComponent(entity, new BeeIdleMode());
                        if (timedOut)
                            ecb.SetComponent(myself.TargetEntity, new TargetedBy { Value = Entity.Null });
                        myself.TargetEntity = Entity.Null;
                    }
                    else
                    {
                        var otherpos = GetComponent<Translation>(myself.TargetEntity);
                        if (math.distancesq(otherpos.Value, position.Value) < teamDef.attackRange)
                        {
                            ecb.RemoveComponent<BeeHuntMode>(entity);
                            ecb.AddComponent(entity, new BeeAttackMode());
                        }
                    }
                }
            ).Schedule();
        
        ecbs.AddJobHandleForProducer(Dependency);
    }
}