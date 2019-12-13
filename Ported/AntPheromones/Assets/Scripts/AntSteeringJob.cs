
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;

[BurstCompile]
public struct AntSteeringJob : IJobForEach<Translation, AntComponent, AntSteeringComponent>
{
    [ReadOnly] public float3 ColonyPosition;
    [ReadOnly] public float3 ResourcePosition; 
    [ReadOnly] public int MapSize;
    [ReadOnly] [NativeDisableParallelForRestriction] [DeallocateOnJobCompletion] public NativeArray<float> RandomDirections;
    [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<float> PheromoneMap;
    [ReadOnly] public int2 ObstacleBucketDimensions;
    [ReadOnly] public NativeArray<int2> ObstacleBuckets;
    [ReadOnly] public NativeArray<MapObstacle> CachedObstacles;
    [ReadOnly] public float TargetSteeringStrength;

    private readonly static float lookAheadDistance = 3.0f;

    int PheromoneIndex(int x, int y) 
    {
		return x + y * MapSize;
	}

    private int2 GetObstacleBucket(float2 position)
	{
        int2 cell = math.clamp((int2)(position * ObstacleBucketDimensions), 0, ObstacleBucketDimensions - 1);
        int bucketIndex = cell.y * ObstacleBucketDimensions.x + cell.x;
		int2 range = ObstacleBuckets[bucketIndex];

		return range;
	}

    bool Linecast(float3 point1, float3 point2) 
    {
        float2 dp = point2.xy - point1.xy;
        float dist = math.length(dp);

		int stepCount = (int)math.ceil(dist*.5f);
		
        for (int s = 0; s < stepCount; s++) 
        {
			float t = (float)s / stepCount;

            float2 lookAheadPosition = point1.xy + dp * t;

            int2 range = GetObstacleBucket(lookAheadPosition);

            // if there are any obstacles in this bucket
            if (range.y > 0) 
            {
                return true;
            }
		}

		return false;
	}

    float PheromoneSteering(ref AntComponent ant, ref Translation translation) 
    {
		float output = 0;

        float angle = ant.facingAngle - math.PI * 0.25f;
		for (int i = -1; i <= 1; i += 2, angle += math.PI * 0.5f) 
        {
            float2 dp;
            math.sincos(angle, out dp.x, out dp.y);

            int2 test = (int2)(MapSize * (translation.Value.xy + dp * lookAheadDistance));

            if (math.any(test < 0) || math.any(test >= MapSize))
                continue;

            int index = PheromoneIndex((int)test.x, (int)test.y);
            float value = PheromoneMap[index];
            output += value * i;
		}

		return math.sign(output);
	}

    float TargetSteering(ref AntComponent ant, ref Translation translation)
    {
        var targetPos = ant.state == 0 ? ResourcePosition : ColonyPosition;

        // target is occluded
        if (Linecast(translation.Value, targetPos))
            return 0.0f;

        float targetAngle = math.atan2(targetPos.y - translation.Value.y, targetPos.x - translation.Value.x);
        float angleDelta = targetAngle - ant.facingAngle;

        if (angleDelta > math.PI) 
        {
            return math.PI * 2f;
        } 
        else if (angleDelta < -math.PI) 
        {
            return -math.PI * 2f;
        } 
        else 
        {
            if (math.abs(angleDelta) < math.PI * 0.5f)
                return (angleDelta) * TargetSteeringStrength;
        }

        return 0.0f;
    }

	public void Execute([ReadOnly] ref Translation translation, [ReadOnly] ref AntComponent ant, ref AntSteeringComponent antSteering)
	{
		antSteering.RandomDirection = RandomDirections[ant.index];
		antSteering.PheromoneSteering = PheromoneSteering(ref ant, ref translation);
        antSteering.TargetSteering = TargetSteering(ref ant, ref translation);
	}
}
