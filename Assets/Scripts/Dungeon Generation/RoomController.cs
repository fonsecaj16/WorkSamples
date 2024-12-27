using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEngine.AI;
using Unity.AI.Navigation;

// Class to store information about each room
public class RoomInfo
{
    public string name; // Room name (e.g., "Start", "End", etc.)
    public int x; // X-coordinate in the map grid
    public int y; // Y-coordinate in the map grid
    public int yMatriz; // Adjusted Y-coordinate for matrix calculations
    public int id; // Unique identifier for the room
}

// Main controller class for managing rooms
public class RoomController : MonoBehaviour
{
    public static RoomController instance; // Singleton instance for global access
    public string currentWorldName = "PrimerPiso"; // Name of the current world/level
    Room currRoom; // Reference to the current active room
    bool once = false; // Flag for actions that should only happen once
    public KeyValuePair<int, string>[,] map; // Map grid storing room IDs and names
    public NavMeshSurface surface; // Reference to NavMesh for navigation
    public bool finishedLoading = false; // Flag indicating whether room loading is complete
    public bool finishedLoadingRooms = false; // Flag indicating whether room processing is complete
    RoomInfo currentLoadRoomData; // Data for the room currently being loaded
    Queue<RoomInfo> loadRoomQueue = new Queue<RoomInfo>(); // Queue for loading rooms in order
    public List<Room> loadedRooms = new List<Room>(); // List of all loaded rooms
    KeyValuePair<int, int> previousPosition; // Coordinates of the previous room
    bool isLoadingRoom = false; // Flag to prevent concurrent room loading
    bool spawnedBossRoom = false; // Flag indicating if the boss room has been spawned
    bool updatedRooms = false; // Flag indicating if rooms have been updated
    int ID = 1; // ID counter for rooms
    int size; // Map grid size
    int center; // Center of the map grid
    Vector2Int prevPos; // Coordinates of the previous room for updates

    public bool FinishedLoading { get { return finishedLoading; } } // Property for checking if loading is finished
    public bool FinishedLoadingRooms { get { return finishedLoadingRooms; } } // Property for checking if room processing is finished

    private void Awake()
    {
        instance = this; // Set the singleton instance
    }

    private void Update()
    {
        UpdateRoomQueue(); // Continuously process the room loading queue
    }
    void UpdateRoomQueue()
    {
        if (isLoadingRoom) return; // Skip if already loading a room

        if (loadRoomQueue.Count == 0)
        {
            // Handle post-loading actions
            if (!spawnedBossRoom)
            {
                StartCoroutine(SpawnBossRoom(0));
            }
            else if (!updatedRooms && spawnedBossRoom)
            {
                StartCoroutine(WaitForDoorRemoval());
            }
            return;
        }

        // Dequeue and start loading the next room
        currentLoadRoomData = loadRoomQueue.Dequeue();
        isLoadingRoom = true;
        StartCoroutine(LoadRoomRoutine(currentLoadRoomData));
    }

    // Waits before removing unconnected doors in rooms
    IEnumerator WaitForDoorRemoval()
    {
        updatedRooms = true;
        yield return new WaitForSeconds(1f);
        foreach (Room room in loadedRooms)
        {
            room.RemoveUnconnectedDoors(); // Removes unnecessary doors
        }
        finishedLoadingRooms = true;
        InstantiateRandomNFT.SummonNFT(); // Spawn NFTs (if applicable)
        StartCoroutine(RoomCoroutine());
    }

    // Deletes planes used for navigation after room loading
    void DeletePlanes()
    {
        foreach (Room room in loadedRooms)
        {
            if (room.plane != null)
            {
                room.plane?.SetActive(false);
            }
        }
    }

    // Spawns the boss room
    public IEnumerator SpawnBossRoom(int i)
    {
        spawnedBossRoom = true;
        yield return new WaitForSeconds(0.5f);

        if (loadRoomQueue.Count == 0)
        {
            // Logic to replace a regular room with the boss room
            Room bossRoom = loadedRooms[loadedRooms.Count - (1 + i)];
            Room tempRoom = new Room(bossRoom.X, bossRoom.Y) { RealY = bossRoom.RealY };
            Destroy(bossRoom.gameObject);

            var roomToRemove = loadedRooms.Single(r => r.X == tempRoom.X && r.Y == tempRoom.Y);
            loadedRooms.Remove(roomToRemove);

            if (roomToRemove.nameRoom == "BigRoom")
            {
                ClearRoomFromMap(tempRoom);
                Vector2Int endRoomPosition = DetermineEndRoomPosition(tempRoom);
                LoadRoom("End", endRoomPosition.x, endRoomPosition.y, Vector2Int.zero);
            }
            else
            {
                LoadRoom("End", tempRoom.X, tempRoom.RealY, Vector2Int.zero);
            }

            StartCoroutine(RoomCoroutine());
        }

        StartCoroutine(WaitForNavMesh());
    }

    // Clears a room from the map grid
    private void ClearRoomFromMap(Room tempRoom)
    {
        map[tempRoom.X, tempRoom.RealY] = new KeyValuePair<int, string>(0, "");
        map[tempRoom.X + 1, tempRoom.RealY] = new KeyValuePair<int, string>(0, "");
        map[tempRoom.X + 1, tempRoom.RealY + 1] = new KeyValuePair<int, string>(0, "");
        map[tempRoom.X, tempRoom.RealY + 1] = new KeyValuePair<int, string>(0, "");
    }

    // Determines where to place the end room
    private Vector2Int DetermineEndRoomPosition(Room tempRoom)
    {
        bool canPlaceAbove = tempRoom.RealY - 1 >= 0 && map[tempRoom.X, tempRoom.RealY - 1].Key == 0;
        bool canPlaceBelow = tempRoom.RealY + 1 < map.GetLength(1) && map[tempRoom.X, tempRoom.RealY + 1].Key == 0;
        bool canPlaceLeft = tempRoom.X - 1 >= 0 && map[tempRoom.X - 1, tempRoom.RealY].Key == 0;
        bool canPlaceRight = tempRoom.X + 2 < map.GetLength(0) && map[tempRoom.X + 2, tempRoom.RealY].Key == 0;

        if (canPlaceAbove) return new Vector2Int(tempRoom.X, tempRoom.RealY - 1);
        if (canPlaceBelow) return new Vector2Int(tempRoom.X, tempRoom.RealY + 1);
        if (canPlaceLeft) return new Vector2Int(tempRoom.X - 1, tempRoom.RealY);
        if (canPlaceRight) return new Vector2Int(tempRoom.X + 2, tempRoom.RealY);

        return new Vector2Int(tempRoom.X, tempRoom.RealY); // Default fallback
    }

    // Waits for NavMesh updates
    IEnumerator WaitForNavMesh()
    {
        yield return new WaitForSeconds(0.5f);
        DeletePlanes();
        finishedLoading = true;
    }

    // Initializes the map grid and sets the center position
    public void SetSize(int size)
    {
        this.size = size; // Set the grid size
        center = (size / 2); // Calculate the center of the grid
        map = new KeyValuePair<int, string>[size, size]; // Initialize the map grid
    }

    // Loads a room into the map grid
    public void LoadRoom(string name, int x, int y, Vector2Int previousPos)
    {
        prevPos = previousPos; // Store the previous position for reference
        RoomInfo newRoomData = new RoomInfo(); // Create a new room data object

        // Handle specific room types
        if (name == "Start" || name == "End") // Fixed rooms like Start and End
        {
            LoadFixedRoom(name, x, y, ref newRoomData); // Load the fixed room
        }
        else if (name == "Enemy" || name == "Empty") // Dynamic rooms
        {
            LoadDynamicRoom(name, x, y, ref newRoomData, 2, 1); // Load with specific steps
        }
        else if (name == "BigRoom") // Large rooms requiring more space
        {
            LoadDynamicRoom(name, x, y, ref newRoomData, 2, 2); // Load with larger steps
        }
    }

    // Loads fixed rooms like Start and End into specific positions
    private void LoadFixedRoom(string name, int x, int y, ref RoomInfo newRoomData)
    {
        int startX = name == "Start" ? center : x; // Center the Start room
        int startY = name == "Start" ? center : y;

        map[startX, startY] = new KeyValuePair<int, string>(ID, name); // Assign ID and name
        map[startX + 1, startY] = new KeyValuePair<int, string>(ID, name); // Handle multi-tile rooms
        previousPosition = new KeyValuePair<int, int>(startX, startY);

        FillRoomData(ref newRoomData, name, startX, startY, name == "Start"); // Populate room data
        loadRoomQueue.Enqueue(newRoomData); // Add the room to the loading queue
    }

    // Handles dynamic room placement with collision checks
    private void LoadDynamicRoom(string name, int x, int y, ref RoomInfo newRoomData, int stepX, int stepY)
    {
        int i = 0;
        // Check if the room placement is blocked
        while (IsRoomBlocked(x, y, i, stepX, stepY))
        {
            i++;
            if (i > 8) return; // Prevent infinite loops
        }

        if (CanPlaceRoom(x, y, i, stepX, stepY)) // Check if the room can be placed
        {
            PlaceRoom(name, x, y, i, stepX, stepY, ref newRoomData); // Place the room
        }
    }

    // Checks if the room placement is blocked by existing rooms
    private bool IsRoomBlocked(int x, int y, int i, int stepX, int stepY)
    {
        // Check boundaries and existing rooms
        if (previousPosition.Key + ((stepX + i) * x) >= 9 || previousPosition.Key + ((stepX + i) * x) < 0 ||
            previousPosition.Value + ((stepY + i) * y) >= 9 || previousPosition.Value + ((stepY + i) * y) < 0)
            return true;

        // Additional checks for overlapping rooms
        if (map[previousPosition.Key + ((1 + i) * x), previousPosition.Value].Key != 0 ||
            map[previousPosition.Key, previousPosition.Value + ((1 + i) * y)].Key != 0)
            return true;

        return false; // No blockage
    }

    // Checks if the room can be placed in the specified position
    private bool CanPlaceRoom(int x, int y, int i, int stepX, int stepY)
    {
        return previousPosition.Key + ((stepX + i) * x) < 9 &&
               previousPosition.Value + ((stepY + i) * y) < 9;
    }

    // Places the room into the map grid and updates its data
    private void PlaceRoom(string name, int x, int y, int i, int stepX, int stepY, ref RoomInfo newRoomData)
    {
        map[previousPosition.Key + ((1 + i) * x), previousPosition.Value] = new KeyValuePair<int, string>(ID, name);
        map[previousPosition.Key, previousPosition.Value + ((1 + i) * y)] = new KeyValuePair<int, string>(ID, name);

        FillRoomData(ref newRoomData, name, previousPosition.Key, previousPosition.Value, false);
        loadRoomQueue.Enqueue(newRoomData);
    }

    // Fills room data for initialization
    private void FillRoomData(ref RoomInfo roomData, string name, int x, int y, bool isStart)
    {
        roomData.name = name;
        roomData.x = x;
        roomData.y = isStart ? y : size - 1 - y;
        roomData.yMatriz = y;
        roomData.id = ID++;
    }

    // Coroutine for loading a room asynchronously
    IEnumerator LoadRoomRoutine(RoomInfo info)
    {
        string roomName = currentWorldName + info.name;
        AsyncOperation loadRoom = SceneManager.LoadSceneAsync(roomName, LoadSceneMode.Additive);
        while (!loadRoom.isDone)
        {
            yield return null;
        }
    }
    public void RegisterRoom(Room room)
    {
        // Check if the room does not already exist at the specified coordinates
        if (!DoesRoomExist(currentLoadRoomData.x, currentLoadRoomData.y))
        {
            // Handle "End" room special case (currently does nothing)
            if (currentLoadRoomData.name == "End")
            {
            }

            // Set position for "Start" room
            if (currentLoadRoomData.name == "Start")
            {
                room.transform.position = new Vector3(currentLoadRoomData.x * 15.9f, currentLoadRoomData.y * 20f, 0);
            }
            // Set position for normal rooms ("Empty", "Enemy", "End")
            else if (currentLoadRoomData.name == "Empty" || currentLoadRoomData.name == "Enemy" || currentLoadRoomData.name == "End")
            {
                room.transform.position = new Vector3(
                    currentLoadRoomData.x * 15.9f,
                    (currentLoadRoomData.y * 20f) + (8 - (2 * currentLoadRoomData.y)),
                    0);
            }
            // Set position for "BigRoom" rooms
            else if (currentLoadRoomData.name == "BigRoom")
            {
                room.transform.position = new Vector3(
                    currentLoadRoomData.x * 15.9f,
                    (currentLoadRoomData.y * 18f) - 2f,
                    0);
            }
            // Default position for other rooms
            else
            {
                room.transform.position = new Vector3(currentLoadRoomData.x * 15.9f, currentLoadRoomData.y * 18f, 0);
            }

            // Set room properties
            room.X = currentLoadRoomData.x;
            room.Y = currentLoadRoomData.y;
            room.id = currentLoadRoomData.id;
            room.RealY = currentLoadRoomData.yMatriz;
            room.name = $"{currentWorldName} {room.id}-{currentLoadRoomData.name} {room.X},{room.Y}";
            room.transform.parent = transform; // Set this room as a child of the RoomController

            isLoadingRoom = false; // Indicate that loading for this room is finished

            // Set the initial room for the camera if this is the first room
            if (loadedRooms.Count == 0)
            {
                CameraController.instance.currRoom = room;
            }

            loadedRooms.Add(room); // Add the room to the list of loaded rooms
        }
        else
        {
            // If the room is "End", mark it as not loading anymore
            if (currentLoadRoomData.name == "End")
            {
                isLoadingRoom = false;
            }
        }
    }
    public string GetRandomRoomName()
    {
        string[] possibleRooms = new string[] { "Empty", "Enemy", "BigRoom" };
        return possibleRooms[Random.Range(0, possibleRooms.Length)];
    }

    // Checks if a room exists at the specified coordinates
    public bool DoesRoomExist(int x, int y)
    {
        return loadedRooms.Find(item => item.X == x && item.Y == y) != null;
    }

    // Finds and returns a room object at the specified coordinates
    public Room FindRoom(int x, int y)
    {
        return loadedRooms.Find(item => item.X == x && item.Y == y);
    }

    // Handles player entering a new room
    public void OnPlayerEnterRoom(Room room)
    {
        // Deactivate walls of the previous room
        if (once)
        {
            ChangeWalls(currRoom, false);
        }
        once = true;

        // Enable or disable camera follow based on room type
        if (room.nameRoom == "BigRoom" || room.nameRoom == "LRoom")
        {
            CameraController.instance.followPlayer = true;
        }
        else
        {
            CameraController.instance.followPlayer = false;
        }

        CameraController.instance.currRoom = room; // Update the current room for the camera
        currRoom = room; // Update the reference to the current room
        ChangeWalls(room, true); // Activate walls for the new room
        StartCoroutine(RoomCoroutine()); // Start room update coroutine
        AudioManager.instance.ReproduceRoomAudio(currRoom.soundOnEnter); // Play room entry sound
    }
    public IEnumerator RoomCoroutine()
    {
        yield return new WaitForSeconds(0.2f);
        UpdateRooms(); // Update all rooms
    }

    // Updates doors and interactions for all rooms
    public void UpdateRooms()
    {
        foreach (Room room in loadedRooms)
        {
            Transform[] enemies = room.FindComponentsInChildrenWithTag<Transform>("Enemy"); // Find enemies in the room

            if (currRoom != room) // For rooms other than the current room
            {
                foreach (Door door in room.GetComponentsInChildren<Door>())
                {
                    door.colliderDoor.isTrigger = true; // Set doors to triggers
                    if (door.doorActive)
                    {
                        door.doorCollider.SetActive(false); // Deactivate door colliders
                    }
                    Physics2D.IgnoreCollision(door.colliderDoor, PlayerController.instance.GetComponent<Collider2D>(), false);
                }
            }
            else // For the current room
            {
                if (enemies.Length > 0) // If enemies exist
                {
                    foreach (Door door in room.GetComponentsInChildren<Door>())
                    {
                        if (door.doorActive)
                        {
                            door.doorCollider.SetActive(true); // Activate door colliders
                            door.colliderDoor.isTrigger = false; // Remove trigger
                        }
                        Physics2D.IgnoreCollision(door.colliderDoor, PlayerController.instance.GetComponent<Collider2D>(), true);
                    }
                }
                else // If no enemies exist
                {
                    foreach (Door door in room.GetComponentsInChildren<Door>())
                    {
                        door.colliderDoor.isTrigger = true;
                        if (door.doorActive)
                        {
                            door.doorCollider.SetActive(false); // Deactivate door colliders
                        }
                        Physics2D.IgnoreCollision(door.colliderDoor, PlayerController.instance.GetComponent<Collider2D>(), false);
                    }
                }
            }
        }
    }

    public void ChangeWalls(Room room, bool on)
    {
        // Define sorting layers based on the "on" parameter
        string wallLayer = on ? "InRoomWall" : "Walls";
        string horizontalDoorLayer = on ? "InRoomHorizontalDoor" : "HorizontalDoors";
        string doorLayer = on ? "InRoomDoor" : "Doors";
        string closeFullDoorLayer = on ? "InRoomCloseFullDoor" : "CloseFullDoor";

        // Update wall layers
        var walls = new[] { room.UpperWall, room.BottomWall, room.LeftWall, room.RightWall };
        SetSortingLayerForObjects(walls, wallLayer);

        // Update doors with predefined sorting layers
        var horizontalDoors = new[]
        {
        room.TopLeftLeftDoorObject,
        room.TopLeftRightDoorObject,
        room.TopRightRightDoorObject,
        room.BottomLeftLeftDoorObject,
        room.BottomRightRightDoorObject
    };
        SetSortingLayerForObjects(horizontalDoors, horizontalDoorLayer);

        var verticalDoors = new[]
        {
        room.TopLeftUpDoorObject,
        room.TopLeftDownDoorObject,
        room.TopRightDownDoorObject,
        room.TopRightUpDoorObject,
        room.BottomLeftDownDoorObject,
        room.BottomRightDownDoorObject,
        room.BottomRightUpDoorObject
    };
        SetSortingLayerForObjects(verticalDoors, doorLayer);

        // Update "NoDoor" objects with the close full door layer
        var noDoors = new[]
        {
        room.TopLeftLeftDoorObjectNoDoor,
        room.TopLeftRightDoorObjectNoDoor,
        room.TopLeftUpDoorObjectNoDoor,
        room.TopLeftDownDoorObjectNoDoor,
        room.TopRightDownDoorObjectNoDoor,
        room.TopRightRightDoorObjectNoDoor,
        room.TopRightUpDoorObjectNoDoor,
        room.BottomLeftLeftDoorObjectNoDoor,
        room.BottomLeftDownDoorObjectNoDoor,
        room.BottomRightRightDoorObjectNoDoor,
        room.BottomRightDownDoorObjectNoDoor,
        room.BottomRightUpDoorObjectNoDoor
    };
        SetSortingLayerForObjects(noDoors, closeFullDoorLayer);
    }

    // Helper method to update sorting layers for multiple objects
    private void SetSortingLayerForObjects(GameObject[] objects, string sortingLayer)
    {
        foreach (var obj in objects)
        {
            if (obj != null)
            {
                obj.GetComponent<Renderer>().sortingLayerID = SortingLayer.NameToID(sortingLayer);
            }
        }
    }
}