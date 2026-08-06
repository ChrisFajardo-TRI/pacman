using UnityEditor;
using UnityEngine;

// One-shot generator for the FPV cube/floor prefabs in Resources/. Placing them
// there guarantees the URP Unlit shader and primitive meshes survive build
// stripping (Shader.Find returns null in WebGL builds otherwise).
public static class FPVAssetCreator
{
    [MenuItem("Tools/Create FPV Assets")]
    public static void Create()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources");
        var shader = Shader.Find("Universal Render Pipeline/Unlit");

        var wallMat = new Material(shader);
        AssetDatabase.CreateAsset(wallMat, "Assets/Resources/FPVWall.mat");
        var floorMat = new Material(shader);
        floorMat.SetColor("_BaseColor", new Color(0.06f, 0.06f, 0.12f));
        AssetDatabase.CreateAsset(floorMat, "Assets/Resources/FPVFloor.mat");

        SavePrimitive(PrimitiveType.Cube, wallMat, "Assets/Resources/FPVWall.prefab");
        SavePrimitive(PrimitiveType.Quad, floorMat, "Assets/Resources/FPVFloor.prefab");
        AssetDatabase.SaveAssets();
    }

    static void SavePrimitive(PrimitiveType type, Material mat, string path)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }
}
