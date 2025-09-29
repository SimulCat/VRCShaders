using UnityEditor;
using UnityEngine;
using System.IO;
using JetBrains.Annotations;
#if UNITY_EDITOR
public class TextureSaverEditor : EditorWindow
{
    [MenuItem("CONTEXT/Texture2D/Save Texture as PNG...")]
    public static void SaveTextureAsPNGItem(MenuCommand menuCommand)
    {
        Texture2D texture = menuCommand.context as Texture2D;
        SaveTextureAsPNG(texture);
    }
    public static void SaveTextureAsPNG(Texture2D selectedTexture)
    {
        // Get the currently selected object in the Project window

        // Check if a Texture2D is actually selected
        if (selectedTexture == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a Texture2D in the Project window.", "OK");
            return;
        }

        // Open a save file panel to choose the save location and filename
        string path = EditorUtility.SaveFilePanel(
            "Save Texture as PNG", // Dialog title
            "",                   // Default directory (empty means last used or project root)
            selectedTexture.name + ".png", // Default filename
            "png"                 // File extension filter
        );

        // If a path was selected (user didn't cancel)
        if (!string.IsNullOrEmpty(path))
        {
            // Encode the texture to PNG format
            byte[] bytes = selectedTexture.EncodeToPNG();

            // Write the bytes to the chosen file path
            File.WriteAllBytes(path, bytes);

            Debug.Log("Texture saved to: " + path);
        }
    }
}
#endif