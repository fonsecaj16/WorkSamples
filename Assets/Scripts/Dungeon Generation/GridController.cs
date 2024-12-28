using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridController : MonoBehaviour
{
    public Room room;
    public List<GameObject> props;
    [System.Serializable]
    public struct Grid
    {
        public int colums, rows;
        public float verticalOffset, horizontalOffset;
    }
    
    public Grid grid;
    public GameObject gridTile;
    public List<Vector2> availablePoints= new List<Vector2>();

    private void Awake()
    {
        room = GetComponentInParent<Room>();
        grid.colums = (int)room.width-3;
        grid.rows = (int)room.height - 5;
        GenerateGrid();

    }

    //Spawn available positions to spawn enemies and/or objects
    public void GenerateGrid()
    {
        
        grid.verticalOffset += room.transform.localPosition.z;
        grid.horizontalOffset+= room.transform.localPosition.x;

        for(int y =0; y<grid.rows;y++)
        {
            for(int x=0;x<grid.colums;x++)
            {
                GameObject go = Instantiate(gridTile, transform);
                go.transform.position = new Vector3(x - (grid.colums - grid.horizontalOffset),0, y - (grid.rows - grid.verticalOffset));
                go.name = "X: " + x + ", Y: " + y;
                availablePoints.Add(go.transform.position);
                go.SetActive(false);
                
            }
        }
        
        GetComponentInParent<ObjectRoomSpawner>()?.InitializeObjectSpawning();
    }
}
