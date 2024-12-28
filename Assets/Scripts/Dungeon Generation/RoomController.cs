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
    public bool FinishedLoadingRooms { get { return _finishedLoadingRooms; } } // Property for checking if room processing is finished

    public static RoomController Instance; // Singleton instance for global access
    public string CurrentWorldName = "PrimerPiso"; // Name of the current world/level
    public KeyValuePair<int, string>[,] Map; // Map grid storing room IDs and names
    public List<Room> LoadedRooms = new List<Room>(); // List of all loaded rooms

    private bool _finishedLoadingRooms = false; // Flag indicating whether room processing is complete
    private Room _currRoom; // Reference to the current active room
    private bool _once = false; // Flag for actions that should only happen once
    private RoomInfo _currentLoadRoomData; // Data for the room currently being loaded
    private Queue<RoomInfo> _loadRoomQueue = new Queue<RoomInfo>(); // Queue for loading rooms in order
    private KeyValuePair<int, int> _previousPosition; // Coordinates of the previous room
    private bool _isLoadingRoom = false; // Flag to prevent concurrent room loading
    private bool _spawnedBossRoom = false; // Flag indicating if the boss room has been spawned
    private bool _updatedRooms = false; // Flag indicating if rooms have been updated
    private int _id = 1; // ID counter for rooms
    private int _size; // Map grid size
    private int _center; // Center of the map grid
    private Vector2Int _prevPos; // Coordinates of the previous room for updates
    private static string START_ROOM = "Start";
    private static string END_ROOM = "End";


    private void Awake()
    {
        Instance = this; // Set the singleton instance
    }

    private void Update()
    {
        UpdateRoomQueue(); // Continuously process the room loading queue
    }
    void UpdateRoomQueue()
    {
        if (_isLoadingRoom) return; // Skip if already loading a room

        if (_loadRoomQueue.Count == 0)
        {
            // Handle post-loading actions
            if (!_spawnedBossRoom)
            {
                StartCoroutine(SpawnBossRoom(0));
            }
            else if (!_updatedRooms && _spawnedBossRoom)
            {
                StartCoroutine(WaitForDoorRemoval());
            }
            return;
        }

        // Dequeue and start loading the next room
        _currentLoadRoomData = _loadRoomQueue.Dequeue();
        _isLoadingRoom = true;
        StartCoroutine(LoadRoomRoutine(_currentLoadRoomData));
    }

    // Waits before removing unconnected doors in rooms
    IEnumerator WaitForDoorRemoval()
    {
        _updatedRooms = true;
        yield return new WaitForSeconds(1f);
        foreach (Room room in LoadedRooms)
        {
            room.RemoveUnconnectedDoors(); // Removes unnecessary doors
        }
        _finishedLoadingRooms = true;
        StartCoroutine(RoomCoroutine());
    }

    // Deletes planes used for navigation after room loading
    void DeletePlanes()
    {
        foreach (Room room in LoadedRooms)
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
        _spawnedBossRoom = true;
        yield return new WaitForSeconds(0.5f);

        if (_loadRoomQueue.Count == 0)
        {
            // Logic to replace a regular room with the boss room
            Room bossRoom = LoadedRooms[LoadedRooms.Count - (1 + i)];
            Room tempRoom = new Room(bossRoom.X, bossRoom.Y) { RealY = bossRoom.RealY };
            Destroy(bossRoom.gameObject);

            var roomToRemove = LoadedRooms.Single(r => r.X == tempRoom.X && r.Y == tempRoom.Y);
            LoadedRooms.Remove(roomToRemove);

            if (roomToRemove.nameRoom == "BigRoom")
            {
                ClearRoomFromMap(tempRoom);
                Vector2Int endRoomPosition = DetermineEndRoomPosition(tempRoom);
                LoadEndRoom(endRoomPosition.x, endRoomPosition.y);
            }
            else
            {
                LoadEndRoom(tempRoom.X, tempRoom.RealY);
            }

            StartCoroutine(RoomCoroutine());
        }

        StartCoroutine(WaitForNavMesh());
    }

    // Clears a room from the map grid
    private void ClearRoomFromMap(Room tempRoom)
    {
        Map[tempRoom.X, tempRoom.RealY] = new KeyValuePair<int, string>(0, "");
        Map[tempRoom.X + 1, tempRoom.RealY] = new KeyValuePair<int, string>(0, "");
        Map[tempRoom.X + 1, tempRoom.RealY + 1] = new KeyValuePair<int, string>(0, "");
        Map[tempRoom.X, tempRoom.RealY + 1] = new KeyValuePair<int, string>(0, "");
    }

    // Determines where to place the end room
    private Vector2Int DetermineEndRoomPosition(Room tempRoom)
    {
        bool canPlaceAbove = tempRoom.RealY - 1 >= 0 && Map[tempRoom.X, tempRoom.RealY - 1].Key == 0;
        bool canPlaceBelow = tempRoom.RealY + 1 < Map.GetLength(1) && Map[tempRoom.X, tempRoom.RealY + 1].Key == 0;
        bool canPlaceLeft = tempRoom.X - 1 >= 0 && Map[tempRoom.X - 1, tempRoom.RealY].Key == 0;
        bool canPlaceRight = tempRoom.X + 2 < Map.GetLength(0) && Map[tempRoom.X + 2, tempRoom.RealY].Key == 0;

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
    }

    // Initializes the map grid and sets the center position
    public void InitializeGrid(int size)
    {
        _size = size; // Set the grid size
        _center = (size / 2); // Calculate the center of the grid
        Map = new KeyValuePair<int, string>[size, size]; // Initialize the map grid
    }

    // Loads a room into the map grid
    public void LoadRoom(string name, int maxHorizontalStep, int maxVerticalStep, int horizontalMovement, int verticalMovement, Vector2Int previousPos)
    {
        _prevPos = previousPos; // Store the previous position for reference
        RoomInfo newRoomData = new RoomInfo(); // Create a new room data object
        LoadDynamicRoom(name, horizontalMovement, verticalMovement, ref newRoomData, maxHorizontalStep, maxVerticalStep);
    }

    //Loads the starting room
    public void LoadStartRoom()
    {
        RoomInfo newRoomData = new RoomInfo();
        Map[_center, _center] = new KeyValuePair<int, string>(_id, START_ROOM);
        Map[_center + 1, _center] = new KeyValuePair<int, string>(_id, START_ROOM);
        _previousPosition = new KeyValuePair<int, int>(_center, _center);

        FillRoomData(ref newRoomData, START_ROOM, _center, _center, true);
        _loadRoomQueue.Enqueue(newRoomData);
    }

    private void LoadEndRoom(int x, int y)
    {
        RoomInfo newRoomData = new RoomInfo();
        Map[x, y] = new KeyValuePair<int, string>(_id, END_ROOM);
        Map[x + 1, y] = new KeyValuePair<int, string>(_id, END_ROOM);
        _previousPosition = new KeyValuePair<int, int>(x, y);

        FillRoomData(ref newRoomData, END_ROOM, x, y, false);
        _loadRoomQueue.Enqueue(newRoomData);
    }

    private void LoadDynamicRoom(string name, int horizontalMovement, int verticalMovement, ref RoomInfo newRoomData, int maxHorizontalStep, int maxVerticalStep)
    {
        int i = 0;
        // Check if the room placement is blocked
        while (IsRoomBlocked(horizontalMovement, verticalMovement, i, maxHorizontalStep, maxVerticalStep))
        {
            i++;
            if (i > 8) return; // Prevent infinite loops
        }

        PlaceRoom(name, horizontalMovement, verticalMovement, i, maxHorizontalStep, maxVerticalStep, ref newRoomData);//Place the room
    }

    private bool IsRoomBlocked(int horizontalMovement, int verticalMovement, int i, int maxHorizontalStep, int maxVerticalStep)
    {
        //Check if the maximum length of the room exceeds the size of the map.
        if (_previousPosition.Key + ((maxHorizontalStep + i) * horizontalMovement) >= 9 || _previousPosition.Key + ((maxHorizontalStep + i) * horizontalMovement) < 0 ||
                _previousPosition.Value + ((maxVerticalStep + i) * verticalMovement) >= 9 || _previousPosition.Value + ((maxVerticalStep + i) * verticalMovement) < 0)
            return true;
        else
        {
            //Determine if the step starts at 0 or 1 based on the type of movement.
            for (int verticalStep = 1 * Mathf.Abs(verticalMovement); verticalStep <= (verticalMovement != 0 ? maxVerticalStep : maxVerticalStep-1); verticalStep++)
            {
                for (int horizontalStep = 1 * Mathf.Abs(horizontalMovement); horizontalStep <= (horizontalMovement != 0 ? maxHorizontalStep : maxHorizontalStep-1); horizontalStep++)
                {
                    //Create new key and Value
                    int newKey = _previousPosition.Key + ((horizontalStep+i) * horizontalMovement);
                    int newValue = _previousPosition.Value + ((verticalStep+i) * verticalMovement);

                    // Check for horizontal/vertical collisions
                    if (Map[newKey, newValue].Key != 0)
                        return true;

                    // Check diagonal positions if horizontalMovement is 0 (vertical room movement)
                    if (horizontalMovement == 0)
                    {
                        // Check adjacent horizontal positions at the current vertical step
                        if (Map[_previousPosition.Key + horizontalStep, newValue].Key != 0)
                            return true;
                    }
                    // Check diagonal positions if verticalMovement is 0 (horizontal room movement)
                    else
                    {
                        if (Map[newKey, _previousPosition.Value+verticalStep].Key != 0)
                            return true;
                    }
                }
            }

        }
        return false; // No blockage
    }

    //Place room
    private void PlaceRoom(string name, int horizontalMovement, int verticalMovement, int i, int maxHorizontalStep, int maxVerticalStep, ref RoomInfo newRoomData)
    {
        for (int verticalStep = 1 * Mathf.Abs(verticalMovement); verticalStep <= (verticalMovement != 0 ? maxVerticalStep : maxVerticalStep - 1); verticalStep++)
        {
            for (int horizontalStep = 1 * Mathf.Abs(horizontalMovement); horizontalStep <= (horizontalMovement != 0 ? maxHorizontalStep : maxHorizontalStep - 1); horizontalStep++)
            {
                //Creates new key and value as in the check but assigns them to the matrix
                int newKey = _previousPosition.Key + ((horizontalStep + i) * horizontalMovement);
                int newValue = _previousPosition.Value + ((verticalStep + i) * verticalMovement);
                Map[newKey, newValue] = new KeyValuePair<int, string>(_id, name);

                //Assigns diagonal IDs in the matrix
                if (horizontalMovement == 0)
                {
                    Map[_previousPosition.Key + horizontalStep, newValue] = new KeyValuePair<int, string>(_id, name);
                }
                else
                {
                    Map[newKey, _previousPosition.Value + verticalStep] = new KeyValuePair<int, string>(_id, name);
                }
            }
        }
        //If movement positive in the matrix assign previous position to the position to the right or directly below, reference position is the leftmost upper corner of the room.
        if (verticalMovement > 0 || horizontalMovement > 0)
        {
            _previousPosition = new KeyValuePair<int, int>(_previousPosition.Key + ((1 + i) * horizontalMovement), _previousPosition.Value + (1 + i) * verticalMovement);
        }
        //If movement is negative in the matrix assign previous position two positions to the left or two positions above, making sure reference point persists in the leftmost upper corner.
        else
        {
            _previousPosition = new KeyValuePair<int, int>(_previousPosition.Key + ((maxHorizontalStep + i) * horizontalMovement), _previousPosition.Value + (maxVerticalStep + i) * verticalMovement);
        }

        FillRoomData(ref newRoomData, name, _previousPosition.Key, _previousPosition.Value,false);
        _loadRoomQueue.Enqueue(newRoomData);
    }

    //Fill new room data
    private void FillRoomData(ref RoomInfo roomData, string name, int x, int y, bool isStart)
    {
        roomData.name = name;        
        roomData.x = x;
        if (isStart)
            roomData.y = y;
        else
            roomData.y = _size - 1 - y;
        roomData.yMatriz = y;
        roomData.id = _id;
        _id++;
    }

    // Coroutine for loading a room asynchronously
    IEnumerator LoadRoomRoutine(RoomInfo info)
    {
        string roomName = CurrentWorldName + info.name;
        AsyncOperation loadRoom = SceneManager.LoadSceneAsync(roomName, LoadSceneMode.Additive);
        while (loadRoom.isDone == false)
        {
            yield return null;
        }
    }
    public void RegisterRoom(Room room)
    {
        // Check if the room does not already exist at the specified coordinates
        if (!DoesRoomExist(_currentLoadRoomData.x, _currentLoadRoomData.y))
        {
            //calculations to properly place the rooms visually
            if (_currentLoadRoomData.name == "End")
            {

            }
            if (_currentLoadRoomData.name == "Start")
            {
                room.transform.position = new Vector3(_currentLoadRoomData.x * 15.9f, _currentLoadRoomData.y * (20f), 0);
            }
            else if (_currentLoadRoomData.name == "Empty" || _currentLoadRoomData.name == "Enemy" || _currentLoadRoomData.name == "End")
            {
                room.transform.position = new Vector3
                (_currentLoadRoomData.x * 15.9f, (_currentLoadRoomData.y * (20f)) + (8 - (2 * _currentLoadRoomData.y)), 0);
            }
            else if (_currentLoadRoomData.name == "BigRoom")
            {
                room.transform.position = new Vector3(_currentLoadRoomData.x * 15.9f, ((_currentLoadRoomData.y * 18f) - 2f), 0);
            }
            else
            {
                room.transform.position = new Vector3(_currentLoadRoomData.x * 15.9f, _currentLoadRoomData.y * 18f, 0);
            }
            room.X = _currentLoadRoomData.x;
            room.Y = _currentLoadRoomData.y;
            room.id = _currentLoadRoomData.id;
            room.RealY = _currentLoadRoomData.yMatriz;
            room.name = CurrentWorldName + " " + room.id + "-" + _currentLoadRoomData.name + " " + room.X + "," + room.Y;
            room.transform.parent = transform;
            _isLoadingRoom = false;
            if (LoadedRooms.Count == 0)
            {
                CameraController.instance.currRoom = room;
            }
            LoadedRooms.Add(room);
        }
        else
        {
            if (_currentLoadRoomData.name == "End")
            {
                _isLoadingRoom = false;
            }
        }
    }
    public bool DoesRoomExist(int x, int y)
    {
        return LoadedRooms.Find(item => item.X == x && item.Y == y) != null;
    }
    public Room FindRoom(int x, int y)
    {
        return LoadedRooms.Find(item => item.X == x && item.Y == y);
    }

    //Handle behaviour when player enters a room
    public void OnPlayerEnterRoom(Room room)
    {
        //Have camera follow player around depending on the size of the room
        if (room.nameRoom == "BigRoom")
        {
            CameraController.instance.followPlayer = true;
        }
        else
        {
            CameraController.instance.followPlayer = false;
        }
        CameraController.instance.currRoom = room;
        _currRoom = room;
        StartCoroutine(RoomCoroutine());
        AudioManager.instance.ReproduceRoomAudio(_currRoom.soundOnEnter);
    }
    public IEnumerator RoomCoroutine()
    {
        yield return new WaitForSeconds(0.2f);
        UpdateRooms();
    }

    //Open and close doors, colliders and rendering based on the position of the player respective to the rooms
    public void UpdateRooms()
    {
        foreach (Room room in LoadedRooms)
        {
            Transform[] enemies = room.FindComponentsInChildrenWithTag<Transform>("Enemy");
            if (_currRoom != room)
            {
                if (enemies.Length != 0)
                {
                    foreach (Door door in room.GetComponentsInChildren<Door>())
                    {
                        door.colliderDoor.isTrigger = true;
                        if (door.doorActive)
                        {
                            door.doorCollider.SetActive(false);
                        }
                        Physics2D.IgnoreCollision(door.colliderDoor, PlayerController.instance.GetComponent<Collider2D>(), false);
                    }
                }
                else
                {
                    foreach (Door door in room.GetComponentsInChildren<Door>())
                    {
                        door.colliderDoor.isTrigger = true;
                        if (door.doorActive)
                        {
                            door.doorCollider.SetActive(false);
                        }
                        Physics2D.IgnoreCollision(door.colliderDoor, PlayerController.instance.GetComponent<Collider2D>(), false);
                    }
                }
            }
            else
            {
                if (enemies.Length > 0)
                {
                    foreach (Door door in room.GetComponentsInChildren<Door>())
                    {
                        if (door.doorActive)
                        {
                            door.doorCollider.SetActive(true);
                            door.colliderDoor.isTrigger = false;
                        }
                        Physics2D.IgnoreCollision(door.colliderDoor, PlayerController.instance.GetComponent<Collider2D>(), true);
                    }
                }
                else
                {
                    foreach (Door door in room.GetComponentsInChildren<Door>())
                    {
                        door.colliderDoor.isTrigger = true;
                        if (door.doorActive)
                        {
                            door.doorCollider.SetActive(false);
                        }
                        Physics2D.IgnoreCollision(door.colliderDoor, PlayerController.instance.GetComponent<Collider2D>(), false);
                    }
                }
            }
        }
    }
}