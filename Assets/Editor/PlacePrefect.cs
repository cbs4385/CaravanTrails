using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PlacePrefect
{
    [MenuItem("Tools/Place Prefect")]
    public static void Run()
    {
        var town = GameObject.Find("Town");
        if (town == null) { Debug.LogError("Town not found."); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefect.glb");
        if (prefab == null) { Debug.LogError("Assets/Art/Prefect.glb not found."); return; }

        var existing = town.transform.Find("Prefect");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Place Prefect");

        go.name = "Prefect";
        go.transform.SetParent(town.transform, false);

        // In front of Palace (Palace at local 0, 0, -5.5) — surveying his domain
        go.transform.localPosition = new Vector3(0.6f, 0f, -4.0f);
        go.transform.localEulerAngles = new Vector3(0f, 175f, 0f);
        go.transform.localScale = Vector3.one * 0.42f;  // slightly larger — he's important

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[PlacePrefect] Prefect placed in front of Palace.");
    }
}
