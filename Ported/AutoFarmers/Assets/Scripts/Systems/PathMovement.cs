﻿using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


public class PathMovement : SystemBase
{
    NativeArray<int> visitedTiles;
    NativeList<int> activeTiles;
    NativeList<int> nextTiles;
    NativeList<int> outputTiles;

    public NativeArray<ETileState> defaultNavigation;
	public NativeArray<ETileState> isRock;
	public NativeArray<ETileState> isTillable;

	NativeArray<int2> dirs;

    int mapWidth;
    int mapHeight;

	RectInt fullMapZone;
	const float k_walkSpeed = 3f;

	protected override void OnCreate()
    {
        activeTiles = new NativeList<int>(Allocator.Persistent);
        nextTiles = new NativeList<int>(Allocator.Persistent);
        outputTiles = new NativeList<int>(Allocator.Persistent);

        dirs = new NativeArray<int2>(4, Allocator.Persistent);
        dirs[0] = new int2(1, 0);
        dirs[1] = new int2(-1, 0);
        dirs[2] = new int2(0, 1);
        dirs[3] = new int2(0, -1);

        defaultNavigation = new NativeArray<ETileState>(5, Allocator.Persistent);
		defaultNavigation[0] = ETileState.Empty;
		defaultNavigation[1] = ETileState.Grown;
		defaultNavigation[2] = ETileState.Seeded;
		defaultNavigation[3] = ETileState.Store;
		defaultNavigation[4] = ETileState.Tilled;

        isRock = new NativeArray<ETileState>(1, Allocator.Persistent);
        isRock[0] = ETileState.Rock;

		isTillable = new NativeArray<ETileState>(1, Allocator.Persistent);
		isTillable[0] = ETileState.Empty;
	}


    protected override void OnUpdate()
    {
        var settings = GetSingleton<CommonSettings>();

		if (mapWidth != settings.GridSize.x || mapHeight != settings.GridSize.y)
		{
			mapWidth = settings.GridSize.x;
			mapHeight = settings.GridSize.y;
			fullMapZone = new RectInt(0, 0, mapWidth, mapHeight);
			if (visitedTiles.IsCreated)
				visitedTiles.Dispose();
			visitedTiles = new NativeArray<int>(mapWidth * mapHeight, Allocator.Persistent);
		}
        var data = GetSingletonEntity<CommonData>();
        
        var pathBuffers = GetBufferFromEntity<PathNode>();
        var tileBuffer = GetBufferFromEntity<TileState>()[data];
		var deltaTime = Time.DeltaTime;

		Entities
			.WithAll<Farmer>()
            .ForEach((Entity entity, ref Translation translation) =>
            {
                var pathNodes = pathBuffers[entity];

                var farmerPosition = new int2((int)math.floor(translation.Value.x), 
                                           (int)math.floor(translation.Value.z));

                if (pathNodes.Length > 0)
                {
                    for (int i = 0; i < pathNodes.Length - 1; i++)
                    {
                        Debug.DrawLine(new Vector3(pathNodes[i].Value.x + .5f, .5f, pathNodes[i].Value.y + .5f), new Vector3(pathNodes[i + 1].Value.x + .5f, .5f, pathNodes[i + 1].Value.y + .5f), Color.red);
                    }

                    var nextTile = pathNodes[pathNodes.Length - 1].Value;

                    if (farmerPosition.x == nextTile.x && farmerPosition.y == nextTile.y)
                    {
                        pathNodes.RemoveAt(pathNodes.Length - 1);
                    }
                    else
                    {
                        bool isBlocked = false;
                        if (nextTile.x < 0 || nextTile.y < 0 || nextTile.x >= settings.GridSize.x || nextTile.y >= settings.GridSize.y)
                        {
                            isBlocked |= true;
                        }
                        var nextTileState = tileBuffer[nextTile.x + nextTile.y * settings.GridSize.x].Value;
                        if (nextTileState == ETileState.Rock)
                        {
                            isBlocked |= true;
                        }

                        if (!isBlocked)
                        {
                            float offset = .5f;
                            if (nextTileState == ETileState.Grown)
                            {
                                offset = .01f;
							}
							float3 targetPos = new float3(nextTile.x + offset, 0.0f, nextTile.y + offset);
							translation.Value = DroneMovement.MoveTowards(translation.Value, targetPos, k_walkSpeed * deltaTime);
                        }
                    }
                }
            }).Run();
    }

    protected override void OnDestroy()
	{
		visitedTiles.Dispose();
		activeTiles.Dispose();
		nextTiles.Dispose();
		outputTiles.Dispose();

		defaultNavigation.Dispose();
		isRock.Dispose();
		isTillable.Dispose();

		dirs.Dispose();
	}

    public int Hash(int x, int y)
    {
        return y * mapWidth + x;
    }
    public void Unhash(int hash, out int x, out int y)
    {
        y = hash / mapWidth;
        x = hash % mapWidth;
    }

	public int FindNearbyRock(int x, int y, int range, DynamicBuffer<TileState> tiles, DynamicBuffer<PathNode> outputPath)
	{
		int rockPosHash = SearchForOne(x, y, range, tiles, defaultNavigation, isRock, fullMapZone);
		if (rockPosHash == -1)
		{
			return -1; //ETileState.Empty;
		}
		else
		{
			int rockX, rockY;
			Unhash(rockPosHash, out rockX, out rockY);
			if (outputPath.IsCreated)
			{
				AssignLatestPath(outputPath, rockX, rockY);
			}

			return Hash(rockX, rockY); //tiles[Hash(rockX, rockY)].Value;
		}
	}

	public void WalkTo(int x, int y, int range, DynamicBuffer<TileState> tiles, NativeArray<ETileState> match, DynamicBuffer<PathNode> outputPath)
	{
		int storePosHash = SearchForOne(x, y, range, tiles, defaultNavigation, match, fullMapZone);
		if (storePosHash != -1)
		{
			int storeX, storeY;
			Unhash(storePosHash, out storeX, out storeY);
			if (outputPath.IsCreated)
			{
				AssignLatestPath(outputPath, storeX, storeY);
			}
		}
	}

	public int SearchForOne(int startX, int startY, int range, DynamicBuffer<TileState> tiles, NativeArray<ETileState> navigable, NativeArray<ETileState> match, RectInt requiredZone)
	{
		outputTiles = Search(startX, startY, range, tiles, navigable, match, requiredZone, 1);
		if (outputTiles.Length == 0)
		{
			return -1;
		}
		else
		{
			return outputTiles[0];
		}
	}

	public NativeList<int> Search(int startX, int startY, int range, DynamicBuffer<TileState> tiles, NativeArray<ETileState> navigable, NativeArray<ETileState> match, RectInt requiredZone, int maxResultCount = 0)
	{
		for (int x = 0; x < mapWidth; x++)
		{
			for (int y = 0; y < mapHeight; y++)
			{
				visitedTiles[Hash(x, y)] = -1;
			}
		}

		outputTiles.Clear();
		visitedTiles[Hash(startX, startY)] = 0;
		activeTiles.Clear();
		nextTiles.Clear();
		nextTiles.Add(Hash(startX, startY));

		int steps = 0;

		while (nextTiles.Length > 0 && (steps < range || range == 0))
		{
			NativeList<int> temp = activeTiles;
			activeTiles = nextTiles;
			nextTiles = temp;
			nextTiles.Clear();

			steps++;;

			for (int i = 0; i < activeTiles.Length; i++)
			{
				int x, y;
				Unhash(activeTiles[i], out x, out y);

				for (int j = 0; j < dirs.Length; j++)
				{
					int x2 = x + dirs[j].x;
					int y2 = y + dirs[j].y;

					if (x2 < 0 || y2 < 0 || x2 >= mapWidth || y2 >= mapHeight)
					{
						continue;
					}
					var hash2 = Hash(x2, y2);
					if (visitedTiles[hash2] == -1 || visitedTiles[hash2] > steps)
					{
						for (int k = 0; k < navigable.Length; k++)
						{
							if (navigable[k] == tiles[hash2].Value)
							{
								visitedTiles[hash2] = steps;
								nextTiles.Add(hash2);
							}
						}
						if (x2 >= requiredZone.xMin && x2 <= requiredZone.xMax)
						{
							if (y2 >= requiredZone.yMin && y2 <= requiredZone.yMax)
							{

								for (int k = 0; k < match.Length; k++)
								{
									if (match[k] == tiles[hash2].Value)
									{
										outputTiles.Add(hash2);
										if (maxResultCount != 0 && outputTiles.Length >= maxResultCount)
										{
											return outputTiles;
										}
									}
								}
							}
						}
					}
				}
			}
		}

		return outputTiles;
	}

	public void AssignLatestPath(DynamicBuffer<PathNode> target, int endX, int endY)
	{
		target.Clear();

		int x = endX;
		int y = endY;

		target.Add(new PathNode { Value = new int2(x, y) });

		int dist = int.MaxValue;
		while (dist > 0)
		{
			int minNeighborDist = int.MaxValue;
			int bestNewX = x;
			int bestNewY = y;
			for (int i = 0; i < dirs.Length; i++)
			{
				int x2 = x + dirs[i].x;
				int y2 = y + dirs[i].y;
				if (x2 < 0 || y2 < 0 || x2 >= mapWidth || y2 >= mapHeight)
				{
					continue;
				}

				int newDist = visitedTiles[Hash(x2, y2)];
				if (newDist != -1 && newDist < minNeighborDist)
				{
					minNeighborDist = newDist;
					bestNewX = x2;
					bestNewY = y2;
				}
			}
			x = bestNewX;
			y = bestNewY;
			dist = minNeighborDist;
			target.Add(new PathNode { Value = new int2(x, y) });
		}
	}
}