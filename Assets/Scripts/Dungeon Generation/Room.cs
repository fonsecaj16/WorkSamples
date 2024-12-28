using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public class Room : MonoBehaviour
{ 
    public float width;
    public float height;
    public GameObject plane;
    public int X;
    public int Y;
    public int id;
    public int RealY;
    bool AlreadyRemovedRight=false;
    bool AlreadyRemovedLeft = false;
    bool AlreadyRemovedTop = false;
    bool AlreadyRemovedBottom = false;
    private bool updatedDoors = false;
    
    public GameObject Plane { get { return plane; } }
    public Room(int x, int y)
    {
        X = x;
        Y = y;
    }
    
    public Transform UpWall;
    public Transform DownWall;
    public Transform ReferencePoint;
    public Transform lowerDownWall;
    int topDoorId;
    int botDoorId;
    int leftDoorId;
    int rightDoorId;
    
    public string nameRoom;
    Vector2Int previousRoom;
    public GameObject UpperWall;
    public GameObject BottomWall;
    public GameObject LeftWall;
    public GameObject RightWall;
    [Space(10)]
    public List<Door> Doors = new List<Door>();
    public AudioClip soundOnEnter;

    
    private void Start()
    {
        if (RoomController.Instance == null)
        {
            Debug.Log("Wrong scene");
            return;
        }

        InitializeDoors();
        RoomController.Instance.RegisterRoom(this);
    }

    //Initialize Doors
    private void InitializeDoors()
    {
        Door[] doors = GetComponentsInChildren<Door>();

        foreach (Door door in doors)
        {
            Doors.Add(door);
        }
    }

    //Remove doors that lead nowhere
    public void RemoveUnconnectedDoors()
    {

        foreach (Door door in Doors)
        {
            Vector2Int offset = GetOffsetForDoorType(door.doorType);
            if (offset == Vector2Int.zero)
                continue;

            Vector2Int targetPosition = new Vector2Int(X, RealY) + offset;

            //Check if position is out of bounds for the door type offset.
            if (IsOutOfBounds(targetPosition))
            {
                //Deactivates the door and replaces it with a wall
                DeactivateDoor(door);
            }
            else
            {
                int roomKey = RoomController.Instance.Map[targetPosition.x, targetPosition.y].Key;
                int doorId = GetDoorId(door.doorType);

                if (roomKey == 0 || roomKey == doorId)
                {
                    DeactivateDoor(door);
                }
                else
                {
                    //Sets the door ID
                    SetDoorId(door.doorType, roomKey);
                }
            }
        }
    }

    private Vector2Int GetOffsetForDoorType(Door.DoorType doorType)
    {
        switch (doorType)
        {
            case Door.DoorType.TopRightRight: return new Vector2Int(2, 0);
            case Door.DoorType.BottomRightRight: return new Vector2Int(2, 1);
            case Door.DoorType.TopLeftRight: return new Vector2Int(1, 0);
            case Door.DoorType.TopLeftLeft: return new Vector2Int(-1, 0);
            case Door.DoorType.BottomLeftLeft: return new Vector2Int(-1, 1);
            case Door.DoorType.TopLeftUp: return new Vector2Int(0, -1);
            case Door.DoorType.TopRightUp: return new Vector2Int(1, -1);
            case Door.DoorType.BottomRightUp: return new Vector2Int(1, 0);
            case Door.DoorType.TopLeftDown: return new Vector2Int(0, 1);
            case Door.DoorType.TopRightDown: return new Vector2Int(1, 1);
            case Door.DoorType.BottomRightDown: return new Vector2Int(1, 2);
            case Door.DoorType.BottomLeftDown: return new Vector2Int(0, 2);
            default: return Vector2Int.zero;
        }
    }

    private bool IsOutOfBounds(Vector2Int position)
    {
        return position.x < 0 || position.x > 8 || position.y < 0 || position.y > 8;
    }

    //Deactivates the door
    private void DeactivateDoor(Door door)
    {
        door.gameObject.SetActive(false);
        door.doorCollider.SetActive(false);
        door.doorIfNoRoom.SetActive(true);
        door.doorActive = false;
    }

    //Returns the door ID depending on where they lead
    private int GetDoorId(Door.DoorType doorType)
    {
        switch (doorType)
        {
            case Door.DoorType.TopRightRight:
            case Door.DoorType.BottomRightRight:
            case Door.DoorType.TopLeftRight:
                return rightDoorId;
            case Door.DoorType.TopLeftLeft:
            case Door.DoorType.BottomLeftLeft:
                return leftDoorId;
            case Door.DoorType.TopLeftUp:
            case Door.DoorType.TopRightUp:
            case Door.DoorType.BottomRightUp:
                return topDoorId;
            case Door.DoorType.TopLeftDown:
            case Door.DoorType.TopRightDown:
            case Door.DoorType.BottomRightDown:
            case Door.DoorType.BottomLeftDown:
                return botDoorId;
            default:
                return -1;
        }
    }

    //Sets door ID to avoid multiple doors leading to the same room
    private void SetDoorId(Door.DoorType doorType, int newDoorId)
    {
        switch (doorType)
        {
            case Door.DoorType.TopRightRight:
            case Door.DoorType.BottomRightRight:
            case Door.DoorType.TopLeftRight:
                rightDoorId = newDoorId;
                break;
            case Door.DoorType.TopLeftLeft:
            case Door.DoorType.BottomLeftLeft:
                leftDoorId = newDoorId;
                break;
            case Door.DoorType.TopLeftUp:
            case Door.DoorType.TopRightUp:
            case Door.DoorType.BottomRightUp:
                topDoorId = newDoorId;
                break;
            case Door.DoorType.TopLeftDown:
            case Door.DoorType.TopRightDown:
            case Door.DoorType.BottomRightDown:
            case Door.DoorType.BottomLeftDown:
                botDoorId = newDoorId;
                break;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(width, height, 0));
    }

    //Returns the room centre
    public Vector3 GetRoomCentre()
    {
        return new Vector3(transform.position.x, transform.position.y,-4 );
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {       
        if (collision.tag=="Player")
        {
            RoomController.Instance.OnPlayerEnterRoom(this);
        }
    }
}
