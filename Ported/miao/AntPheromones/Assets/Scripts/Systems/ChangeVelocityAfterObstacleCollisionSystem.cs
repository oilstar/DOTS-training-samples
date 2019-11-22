﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace AntPheromones_ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CollideWithObstacleSystem))]
    public class ChangeVelocityAfterObstacleCollisionSystem : JobComponentSystem
    {
        private EntityQuery _mapQuery;
        private EntityQuery _steeringStrengthQuery;
        private (bool AreRetrieved, float Inward, float Outward) _steeringStrengths;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            this._mapQuery = GetEntityQuery(ComponentType.ReadOnly<MapComponent>());
            this._steeringStrengthQuery = 
                GetEntityQuery(ComponentType.ReadOnly<SteeringStrengthComponent>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDependencies)
        {
            if (!this._steeringStrengths.AreRetrieved)
            {
                var steeringStrength = this._steeringStrengthQuery.GetSingleton<SteeringStrengthComponent>();
                this._steeringStrengths =
                    (AreRetrieved: true,
                    Inward: steeringStrength.Inward,
                    Outward: steeringStrength.Outward);
            }
            
            var map = this._mapQuery.GetSingleton<MapComponent>();
            
            return new Job
            {
                ColonyPosition = map.ColonyPosition,
                
                InwardStrength = this._steeringStrengths.Inward,
                InwardPushRadius = map.Width,
                
                OutwardStrength = this._steeringStrengths.Outward,
                OutwardPushRadius = map.Width * 0.4f
            }.Schedule(this, inputDependencies);
        }

        [BurstCompile]
        private struct Job : IJobForEach<PositionComponent, ResourceCarrierComponent, VelocityComponent>
        {
            public float2 ColonyPosition;

            public float InwardStrength;
            public float InwardPushRadius;
            
            public float OutwardStrength;
            public float OutwardPushRadius;
            
            public void Execute(
                [ReadOnly]ref PositionComponent position, 
                [ReadOnly] ref ResourceCarrierComponent resourceCarrier,
                [WriteOnly] ref VelocityComponent velocity)
            {
                float pushRadius = resourceCarrier.IsCarrying ? this.InwardPushRadius : this.OutwardPushRadius;
                float strength = resourceCarrier.IsCarrying ? this.InwardStrength : this.OutwardStrength;

                float2 offset = this.ColonyPosition - position.Value;
                float distance = math.length(offset);
                
                velocity.Value += offset / distance * strength * (1f - math.clamp(distance / pushRadius, 0f, 1f));
            }
        }
    }
}