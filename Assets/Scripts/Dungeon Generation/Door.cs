using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MonoBehaviour
{
    public enum DoorType
    {
        TopLeftLeft, TopLeftRight, TopLeftUp, TopLeftDown,
        TopRightDown, TopRightRight, TopRightUp,
        BottomLeftLeft, BottomLeftDown,
        BottomRightRight, BottomRightDown, BottomRightUp
    }

    public DoorType doorType;
    public BoxCollider2D colliderDoor;
    public GameObject doorCollider;
    public GameObject doorIfNoRoom;
    public bool doorActive = true;

    private GameObject player;
    private float widthOffset = 3f;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        colliderDoor = GetComponent<BoxCollider2D>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Vector3 offset = GetOffsetByDoorType(doorType);
            player.transform.position += offset;
        }
    }

    private Vector3 GetOffsetByDoorType(DoorType doorType)
    {
        switch (doorType)
        {
            case DoorType.BottomLeftDown:
            case DoorType.BottomRightDown:
            case DoorType.TopRightDown:
            case DoorType.TopLeftDown:
                return new Vector3(0, -widthOffset, 0);

            case DoorType.TopRightUp:
            case DoorType.TopLeftUp:
            case DoorType.BottomRightUp:
                return new Vector3(0, widthOffset, 0);

            case DoorType.TopLeftLeft:
            case DoorType.BottomLeftLeft:
                return new Vector3(-widthOffset, 0, 0);

            case DoorType.TopLeftRight:
            case DoorType.TopRightRight:
            case DoorType.BottomRightRight:
                return new Vector3(widthOffset, 0, 0);

            default:
                return Vector3.zero; // No movement if the door type is invalid
        }
    }
}
