using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateRoadTexture : EditorWindow
{
    private int textureSize = 256;      // width and height (square)
    private int lineHeightPixels = 5;    // height of the black center line in pixels (vertical thickness)

    [MenuItem("Tools/Create Road Texture")]
    static void ShowWindow()
    {
        GetWindow<CreateRoadTexture>("Road Texture Creator");
    }

    void OnGUI()
    {
        GUILayout.Label("Settings", EditorStyles.boldLabel);
        textureSize = EditorGUILayout.IntField("Texture Size (pixels)", textureSize);
        lineHeightPixels = EditorGUILayout.IntField("Line Height (pixels)", lineHeightPixels);

        if (GUILayout.Button("Generate and Save"))
        {
            GenerateTexture();
        }
    }

    void GenerateTexture()
    {
        // Create a new Texture2D
        Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);

        // Fill with white
        Color white = Color.white;
        Color black = Color.black;

        int centerY = textureSize / 2;
        int halfLine = lineHeightPixels / 2;
        int lowerBound = centerY - halfLine;
        int upperBound = centerY + halfLine;

        // If line height is odd, adjust to keep centered
        if (lineHeightPixels % 2 == 1)
        {
            upperBound = centerY + halfLine + 1;
        }

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                // Check if current row (y) is within the horizontal line band
                if (y >= lowerBound && y < upperBound)
                {
                    tex.SetPixel(x, y, black);
                }
                else
                {
                    tex.SetPixel(x, y, white);
                }
            }
        }

        tex.Apply();

        // Encode to PNG
        byte[] bytes = tex.EncodeToPNG();

        // Ensure the target folder exists
        string folderPath = "Assets/Textures";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Textures");
        }

        // Save the PNG
        string filePath = Path.Combine(folderPath, "RoadWithCenterLine.png");
        File.WriteAllBytes(filePath, bytes);

        // Refresh the asset database so Unity picks up the new file
        AssetDatabase.Refresh();

        // Log success
        Debug.Log($"Texture saved to {filePath} (Size: {textureSize}x{textureSize}, Line height: {lineHeightPixels}px)");

        // Optionally, select the new texture in the Project window
        Texture2D savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
        if (savedTex != null)
        {
            Selection.activeObject = savedTex;
            EditorGUIUtility.PingObject(savedTex);
        }
    }
}