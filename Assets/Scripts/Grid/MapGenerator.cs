using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour
{
    [Header("Tilemap Reference")]
    public Tilemap terrainTilemap;

    [Header("Tile Assets")]
    public TileBase grassTile;
    public TileBase waterTile;

    [Header("Map Settings")]
    public int mapWidth = 128;
    public int mapHeight = 128;

    [Header("River Settings")]
    public float riverCentreX = 64f;
    public int riverWidth = 4;
    public float waveAmplitude = 8f;
    public float waveFrequency = 0.05f;

    void Start()
    {
        GenerateMap();
    }

    void GenerateMap()
    {
        // Step 1: Fill everything with grass
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                terrainTilemap.SetTile(new Vector3Int(x, y, 0), grassTile);
            }
        }

        // Step 2: Carve a river that meanders through the map
        // We go row by row and calculate where the river centre is
        for (int y = 0; y < mapHeight; y++)
        {
            // Use a sine wave to make the river curve naturally
            float offset = Mathf.Sin(y * waveFrequency) * waveAmplitude;
            int centreX = Mathf.RoundToInt(riverCentreX + offset);

            // Paint water tiles for the width of the river
            int halfWidth = riverWidth / 2;
            for (int x = centreX - halfWidth; x <= centreX + halfWidth; x++)
            {
                // Make sure we're within map bounds
                if (x >= 0 && x < mapWidth)
                {
                    terrainTilemap.SetTile(new Vector3Int(x, y, 0), waterTile);
                }
            }
        }

        Debug.Log($"Map generated: {mapWidth}x{mapHeight} with river");
    }
}