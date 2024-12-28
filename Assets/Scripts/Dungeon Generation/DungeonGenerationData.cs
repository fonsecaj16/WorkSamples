
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DungeonGenerationData.asset", menuName ="DungeonGenerationData/Dungeon Data" )]
public class DungeonGenerationData : ScriptableObject
{
    public int NumberOfCrawlers;
    public int IterationMin;
    public int IterationMax;
    public int Size;
    public List<RoomGenerationInfo> AvailableRooms;
}

[Serializable]
public class RoomGenerationInfo
{
    public string Name;
    public int MaxHorizontalStep;
    public int MaxVerticalStep;
}
