using Unity.Entities;
using Unity.Transforms;

public class TankFiringystem : SystemBase
{
    protected override void OnUpdate()
    {
        var time = Time.ElapsedTime;

        /*Entities
            .ForEach((ref Translation translation, TODO) =>
            {
                // TODO: tank firing logic
            }).ScheduleParallel();*/
    }
}