using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Pathfinder : MonoBehaviour
{
    public Tilemap buildingsTilemap;

    // The four cardinal directions an agent can move
    private static readonly Vector3Int[] directions = new Vector3Int[]
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right
    };

    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int end)
    {
        // A* algorithm
        var openSet = new SortedSet<(float fScore, Vector3Int pos)>(
            Comparer<(float fScore, Vector3Int pos)>.Create((a, b) =>
            {
                int compare = a.fScore.CompareTo(b.fScore);
                if (compare != 0) return compare;
                // Tiebreak on position to avoid duplicates being dropped
                compare = a.pos.x.CompareTo(b.pos.x);
                if (compare != 0) return compare;
                return a.pos.y.CompareTo(b.pos.y);
            })
        );

        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore = new Dictionary<Vector3Int, float>();

        gScore[start] = 0;
        openSet.Add((Heuristic(start, end), start));

        while (openSet.Count > 0)
        {
            // Get the node with lowest fScore
            var current = openSet.Min;
            openSet.Remove(current);
            Vector3Int currentPos = current.pos;

            // Found the goal — reconstruct the path
            if (currentPos == end)
            {
                return ReconstructPath(cameFrom, currentPos);
            }

            // Check each neighbour
            foreach (Vector3Int dir in directions)
            {
                Vector3Int neighbour = currentPos + dir;

                // Can we walk here? Must have a tile (road or building)
                if (!IsWalkable(neighbour, start, end))
                    continue;

                float tentativeG = gScore[currentPos] + 1f; // each step costs 1

                if (!gScore.ContainsKey(neighbour) || tentativeG < gScore[neighbour])
                {
                    cameFrom[neighbour] = currentPos;
                    gScore[neighbour] = tentativeG;
                    float fScore = tentativeG + Heuristic(neighbour, end);
                    openSet.Add((fScore, neighbour));
                }
            }
        }

        // No path found
        Debug.Log($"No path from {start} to {end}");
        return null;
    }

    bool IsWalkable(Vector3Int pos, Vector3Int start, Vector3Int end)
    {
        // The start and end tiles are always walkable
        // (so agents can leave/enter buildings)
        if (pos == start || pos == end)
            return true;

        // Otherwise, only roads are walkable
        TileBase tile = buildingsTilemap.GetTile(pos);
        return tile != null && tile.name == "Road";
    }

    float Heuristic(Vector3Int a, Vector3Int b)
    {
        // Manhattan distance — perfect for grid movement with no diagonals
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    List<Vector3Int> ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom,
                                     Vector3Int current)
    {
        var path = new List<Vector3Int> { current };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}