﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public class MoveToTarget : SystemBase
{
    protected override void OnUpdate()
    {
        var deltaTime = Time.DeltaTime;

        Entities.ForEach((ref Velocity velocity, in Translation pos, in Target target) =>
        {
            Translation targetPos;
            if (target.EnemyTarget != Entity.Null)
            {
                targetPos = GetComponent<Translation>(target.EnemyTarget);
            }
            else if (target.ResourceTarget != Entity.Null)
            {
                targetPos = GetComponent<Translation>(target.ResourceTarget);
            }
            else
            {
                return;
            }
            var delta = targetPos.Value - pos.Value;
            var sqrDist = math.distancesq(targetPos.Value, pos.Value);
            if (sqrDist > math.pow(BeeManager.Instance.attackDistance, 2))
            {
                velocity.Value += delta * (BeeManager.Instance.chaseForce * deltaTime / math.sqrt(sqrDist));
            }
            else
            {
                // bee.isAttacking = true;
                velocity.Value += delta * (BeeManager.Instance.attackForce * deltaTime / math.sqrt(sqrDist));

                if (sqrDist < math.pow(BeeManager.Instance.hitDistance, 2))
                {
                    // Hit on enemy
                    // ParticleManager.SpawnParticle(bee.enemyTarget.position,ParticleType.Blood,bee.velocity * .35f,2f,6);
                    // bee.enemyTarget.dead = true;
                    // bee.enemyTarget.velocity *= .5f;
                    // bee.enemyTarget = null;
                }
            }
        }).Schedule();
    }
}
