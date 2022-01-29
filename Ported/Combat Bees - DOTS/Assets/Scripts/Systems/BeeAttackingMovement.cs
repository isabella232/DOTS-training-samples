﻿using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

public partial class BeeAttackingMovement : SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<SingletonMainScene>();
    }
    protected override void OnUpdate()
    {
        float dashMultiplier = 3f;
        BeeProperties beeProperties = GetBeeProperties();
        float deltaTime = World.Time.DeltaTime;
        float3 containerMinPos = GetSingleton<Container>().MinPosition;
        float3 containerMaxPos = GetSingleton<Container>().MaxPosition;

        Entities.WithAll<BeeTag>().ForEach((Entity entity, ref Translation translation, ref BeeTargets beeTargets, ref Velocity velocity, ref BeeStatus beeStatus, ref RandomState randomState) =>
        {
            //we are getting the BeeDead component here because we get compile error when we put it in lambda expression 
            var isBeeDead = GetComponent<BeeDead>(entity).Value;
            if (beeStatus.Value == Status.Attacking)
            {
                float3 delta = beeTargets.CurrentTargetPosition - translation.Value;
                float distanceFromTarget = math.sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);
                
                if (distanceFromTarget < beeProperties.KillingReach +2)
                {
                    // Set current bee to Idle
                    beeStatus.Value = Status.Idle;
                    
                    // Kill the bee
                    var enemyComponent = GetComponent<BeeDead>(beeTargets.EnemyTarget);
                    enemyComponent.Value = true;
                    
                    SetComponent(beeTargets.EnemyTarget, enemyComponent);
                    //the line below is not changing anything 
                    beeTargets.EnemyTarget = Entity.Null;
                    return;
                }
                if (distanceFromTarget < beeProperties.AttackDashReach) // Enemy reached
                {
                    // Kill and go home
                    velocity.Value += delta * (beeProperties.ChaseForce * dashMultiplier / distanceFromTarget);
                }
              
                // Add velocity towards the current target
                velocity.Value += delta * (beeProperties.ChaseForce / distanceFromTarget);
                
                // Add random jitter
                float3 randomJitter = randomState.Value.NextFloat3(-1f, 1f);
                velocity.Value += randomJitter * beeProperties.FlightJitter;
                
                // Apply damping (also limits velocity so that it does not keep increasing indefinitely)
                velocity.Value *= 1f - beeProperties.Damping;
                // Move bee closer to the target
                translation.Value += velocity.Value * deltaTime;

                // Clamp the position within the field container
                translation.Value = math.clamp(translation.Value, containerMinPos, containerMaxPos);
            }
        }).Run();

        bool beeIsdead = false;
        // get the position of the Dead bee
        Entities.WithAll<BeeTag>().ForEach((Entity entity, ref BeeStatus beeStatus, ref BeeDead beeDead, ref RandomState randomState,ref Falling falling ,in Translation translation) =>
        {
            if (beeDead.Value)
            {
                beeStatus.Value = Status.Dead;
                beeIsdead = true;
                falling.shouldFall = true;
                var spawningData = GetSingleton<BloodSpawningProperties>();
                
                // To make the bloodparticles only spawn once 
                if (!beeDead.AnimationStarted)
                {
                    beeDead.AnimationStarted = true;
                    for (int i = 0; i < spawningData.amountParticles; i++)
                    {
                        Entity e = EntityManager.Instantiate(spawningData.bloodEntity);
                        EntityManager.SetComponentData(e, new Translation
                        {
                            Value = translation.Value 
                        });
                        
                        EntityManager.AddComponentData(e, new Falling
                        {
                            timeToLive = randomState.Value.NextFloat(1, 5),
                            shouldFall = true
                        });
                        
                        float x = randomState.Value.NextFloat(-10, 10);
                        float y = randomState.Value.NextFloat(0, 10);
                        float z = randomState.Value.NextFloat(-10, 10);
                        EntityManager.AddComponentData(e, new Velocity()
                        {
                            Value = new float3(x, y, z),
                        });
                    }
                }
            }
        }).WithStructuralChanges().Run();
    }
    
    private BeeProperties GetBeeProperties()
    {
        BeeProperties beeProps = new BeeProperties();
        
        Entities.ForEach((in BeeProperties beeProperties) =>
        {
            beeProps = beeProperties;
        }).Run();

        return beeProps;
    }
    
}

