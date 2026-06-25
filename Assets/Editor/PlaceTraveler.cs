using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PlaceTraveler
{
    [MenuItem("Tools/Place Traveler")]
    public static void Run()
    {
        var town = GameObject.Find("Town");
        if (town == null) { Debug.LogError("Town not found."); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Traveler.glb");
        if (prefab == null) { Debug.LogError("Assets/Art/Traveler.glb not found."); return; }

        var existing = town.transform.Find("Traveler");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Place Traveler");

        go.name = "Traveler";
        go.transform.SetParent(town.transform, false);

        // Just inside the Gate (Gate at local 0, 0, 7.5) — passing through
        go.transform.localPosition = new Vector3(-1.2f, 0f, 6.2f);
        go.transform.localEulerAngles = new Vector3(0f, 210f, 0f); // walking into town
        go.transform.localScale = Vector3.one * 0.38f;

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[PlaceTraveler] Traveler placed near Gate.");
    }
}
