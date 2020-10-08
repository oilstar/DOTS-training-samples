﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;


public class TileCheckSystem : SystemBase
{
    private const float positionThreshold = 0.01f;

    private EntityCommandBufferSystem m_ECBSystem;
    private EntityQuery m_tilesWithWallsQuery;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_ECBSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();

        EntityQueryDesc desc = new EntityQueryDesc
        {
            // Query only matches chunks with both Red and Green components.
            All = new ComponentType[] { typeof(Wall), typeof(Translation) }
        };
        m_tilesWithWallsQuery = EntityManager.CreateEntityQuery(desc);

    }

    protected override void OnUpdate()
    {
        var ecb = m_ECBSystem.CreateCommandBuffer().AsParallelWriter();

        //var walls = m_tilesWithWallsQuery.ToComponentDataArray<Wall>(Allocator.TempJob);
        //var tileTranslations = m_tilesWithWallsQuery.ToComponentDataArray<Translation>(Allocator.TempJob);

        var tileWallsEntity = GetSingletonEntity<TileWall>();
        var tileWalls = EntityManager.GetBuffer<TileWall>(tileWallsEntity);
        // can probably make position readonly.
        Entities
            .WithAll<TileCheckTag>()
            .WithoutBurst()//remove later when this works.
            .WithReadOnly(tileWalls)
            .ForEach((
                Entity entity,
                int entityInQueryIndex,
                ref Direction direction,
                ref Rotation rotation,
                ref Position position) =>
            {
                UnityEngine.Debug.Log("Running Tile Check");
                var tileX = RoundNumberToNearestInt(position.Value.x);
                var tileY = RoundNumberToNearestInt(position.Value.y);
                //UnityEngine.Debug.Log("X: " + tileX +
                //                      " X_orig: " + position.Value.x +
                //                      " Y: " + tileY +
                //                      " Y_orig: " + position.Value.y);
                //UnityEngine.Debug.Log("BufferSize: " + tileWalls.Length);


                //Bug in here somewhere.  the mice can get to a certain point and then continuously spin on the tile.
                byte newDirection = FindNewDirectionIfNeeded(tileX, tileY, direction, tileWalls);
                if (direction.Value == newDirection)
                {
                    //We did not collide with a wall on current tile, check next tile for a wall.
                    newDirection = FindNewDirectionIfNeededFromNextTile(tileX, tileY, direction, tileWalls);
                }

                if (newDirection != direction.Value)
                {
                    direction.Value = newDirection;
                    var temp = quaternion.RotateY(math.radians(90f));
                    rotation.Value = math.normalize(math.mul(rotation.Value, temp));
                }



                //if we dont hit a wall, we still want to remove the tag regardless.
                ecb.RemoveComponent<TileCheckTag>(entityInQueryIndex, entity);
            }).ScheduleParallel();

        m_ECBSystem.AddJobHandleForProducer(Dependency);

    }

    static byte FindNewDirectionIfNeeded(
        int tileX,
        int tileY,
        Direction direction,
        DynamicBuffer<TileWall> tileWalls)
    {
        int bufferIndex = tileY * 10 + tileX;
        TileWall wall = tileWalls[bufferIndex];

        byte directionOut = direction.Value;
        switch (direction.Value)
        {
            case DirectionDefines.North:
                if ((wall.Value & DirectionDefines.North) != 0)
                {
                    directionOut = DirectionDefines.East;
                }
                break;
            case DirectionDefines.South:
                if ((wall.Value & DirectionDefines.South) != 0)
                {
                    directionOut = DirectionDefines.West;
                }
                break;
            case DirectionDefines.West:
                if ((wall.Value & DirectionDefines.West) != 0)
                {
                    directionOut = DirectionDefines.North;
                }
                break;
            case DirectionDefines.East:
                if ((wall.Value & DirectionDefines.East) != 0)
                {
                    directionOut = DirectionDefines.South;
                }
                break;
            default:
                break;
        }

        UnityEngine.Debug.Log("X: " + tileX + " Y: " + tileY +
            "\ncurrent Direction: " + direction.Value + " NewDir: " + directionOut);

        return directionOut;
    }

    /*
     *
     * When we are colliding with a wall on the next tile, we need to check for the opposite wall direction than normal.
     * IE. if we are heading north and there is not a wall on the north portion of the current tile, the next possible wall
     * would be the south wall of the next tile in the same direction.
     *
     */
    static byte FindNewDirectionIfNeededFromNextTile(
        int tileX,
        int tileY,
        Direction direction,
        DynamicBuffer<TileWall> tileWalls)
    {

        float2 forwardDir = ConvertDirectionToForward(direction);
        tileX += (int)forwardDir.x;
        tileY += (int)forwardDir.y;

        if (tileX < 0 || tileY < 0)
        {
            //we cant go negative.
            return direction.Value;
        }

        int bufferIndex = tileY * 10 + tileX;
        TileWall wall = tileWalls[bufferIndex];

        byte directionOut = direction.Value;
        switch (direction.Value)
        {
            case DirectionDefines.North:
                if ((wall.Value & DirectionDefines.South) != 0)
                {
                    directionOut = DirectionDefines.East;
                }
                break;
            case DirectionDefines.South:
                if ((wall.Value & DirectionDefines.North) != 0)
                {
                    directionOut = DirectionDefines.West;
                }
                break;
            case DirectionDefines.West:
                if ((wall.Value & DirectionDefines.East) != 0)
                {
                    directionOut = DirectionDefines.North;
                }
                break;
            case DirectionDefines.East:
                if ((wall.Value & DirectionDefines.West) != 0)
                {
                    directionOut = DirectionDefines.South;
                }
                break;
            default:
                break;
        }

        return directionOut;
    }

    static int RoundNumberToNearestInt(float number)
    {
        return (int)(number + 0.5f);
    }

    static byte GetNewDirection(Direction direction)
    {
        byte newDirection = 0;
        switch (direction.Value)
        {
            case DirectionDefines.North:
                newDirection = DirectionDefines.East;
                break;
            case DirectionDefines.South:
                newDirection = DirectionDefines.West;
                break;
            case DirectionDefines.East:
                newDirection = DirectionDefines.South;
                break;
            case DirectionDefines.West:
                newDirection = DirectionDefines.North;
                break;
            default:
                break;
        }

        return newDirection;
    }

    //stolen from movementsystem, we will want to make this shared.
    static float2 ConvertDirectionToForward(Direction direction)
    {
        var forward = float2.zero;
        //Convert direction to forward
        if ((direction.Value & DirectionDefines.North) == 1)
        {
            forward = new float2(0, 1);
        }
        else if ((direction.Value & DirectionDefines.South) == 2)
        {
            forward = new float2(0, -1);
        }
        else if ((direction.Value & DirectionDefines.East) == 4)
        {
            forward = new float2(1, 0);
        }
        else if ((direction.Value & DirectionDefines.West) == 8)
        {
            forward = new float2(-1, 0);
        }

        return forward;
    }

    static bool IsPositionCloseToTileCenter(float2 position, Translation tileTranslation, float threshold)
    {
        return (math.abs(position.x - tileTranslation.Value.x) <= threshold) &&
               (math.abs(position.y - tileTranslation.Value.z) <= threshold);
    }
}