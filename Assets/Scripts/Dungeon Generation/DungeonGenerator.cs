using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{   
    public DungeonGenerationData DungeonGenerationData;
    private List<Vector2Int> _dungeonRooms;
    
    private void Start()
    {
        _dungeonRooms = DungeonCrawlerController.GenerateDungeon(DungeonGenerationData);//Generates the dungeon positions
        RoomController.Instance.InitializeGrid(DungeonGenerationData.Size);//Initializes the grid 
        SpawnRooms(_dungeonRooms);
    }
    //Spawns the rooms based on the dungeon positions generated
    private void SpawnRooms(IEnumerable<Vector2Int> rooms)
    {
        RoomController.Instance.LoadStartRoom();
        Vector2Int previous = Vector2Int.zero;
        foreach(Vector2Int roomLocation in rooms)
        {
            RoomGenerationInfo roomGenerationInfo = GetRandomRoomGenerationData();
            RoomController.Instance.LoadRoom(roomGenerationInfo.Name,roomGenerationInfo.MaxHorizontalStep,roomGenerationInfo.MaxVerticalStep, roomLocation.x, roomLocation.y,previous);
            previous = roomLocation;
        }       
    }
    //Chooses one of the available rooms at random to be generated
    private RoomGenerationInfo GetRandomRoomGenerationData()
    {
        return DungeonGenerationData.AvailableRooms[Random.Range(0, DungeonGenerationData.AvailableRooms.Count)];
    }
}
