using UnityEngine;
using UnityEditor;

public class TileAssetGenerator : MonoBehaviour
{
    [MenuItem("Tools/Generate Placeholder Tiles")]
    public static void GenerateTiles()
    {
        // Define our tile types: name and colour
        var tileDefinitions = new (string name, Color colour)[]
        {
            ("Grass",       new Color(0.36f, 0.68f, 0.36f)),  // soft green
            ("Water",       new Color(0.25f, 0.52f, 0.85f)),  // calm blue
            ("Road",        new Color(0.40f, 0.40f, 0.40f)),  // grey
            ("House",       new Color(0.85f, 0.65f, 0.30f)),  // warm orange
            ("BurgerStore",  new Color(0.85f, 0.25f, 0.25f)),  // red
            ("Supermarket",  new Color(0.25f, 0.65f, 0.85f)),  // cyan-blue
            ("Office",       new Color(0.55f, 0.35f, 0.75f)),  // purple
        };

        // Make sure the output folders exist
        if (!AssetDatabase.IsValidFolder("Assets/Tiles/Sprites"))
            AssetDatabase.CreateFolder("Assets/Tiles", "Sprites");
        if (!AssetDatabase.IsValidFolder("Assets/Tiles/TileAssets"))
            AssetDatabase.CreateFolder("Assets/Tiles", "TileAssets");

        foreach (var (tileName, colour) in tileDefinitions)
        {
            // Step 1: Create a 1x1 pixel texture with our colour
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            texture.filterMode = FilterMode.Point; // No blurring - crisp pixels

            // Step 2: Save it as a PNG file
            byte[] pngData = texture.EncodeToPNG();
            string spritePath = $"Assets/Tiles/Sprites/{tileName}.png";
            System.IO.File.WriteAllBytes(spritePath, pngData);
            AssetDatabase.ImportAsset(spritePath);

            // Step 3: Configure the imported texture as a sprite
            TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 1; // 1 pixel = 1 Unity unit = 1 grid cell
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();

            // Step 4: Create or update the Tile asset.
            // If it already exists we update it in-place to preserve its GUID
            // (and therefore keep all existing Inspector references intact).
            string tilePath = $"Assets/Tiles/TileAssets/{tileName}.asset";
            var existingTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(tilePath);
            if (existingTile != null)
            {
                existingTile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                existingTile.color = Color.white;
                EditorUtility.SetDirty(existingTile);
                Debug.Log($"Updated tile: {tileName}");
            }
            else
            {
                var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                tile.color = Color.white;
                AssetDatabase.CreateAsset(tile, tilePath);
                Debug.Log($"Created tile: {tileName}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("All placeholder tiles generated!");
    }
}