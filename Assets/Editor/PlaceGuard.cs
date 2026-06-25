using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PlaceGuard
{
    [MenuItem("Tools/Place Guard")]
    public static void Run()
    {
        var town = GameObject.Find("Town");
        if (town == null) { Debug.LogError("Town not found."); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Guard.glb");
        if (prefab == null) { Debug.LogError("Assets/Art/Guard.glb not found."); return; }

        var existing = town.transform.Find("Guard");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Place Guard");

        go.name = "Guard";
        go.transform.SetParent(town.transform, false);

        // Near the GuardPost (at local 4, 0, 5.5) — slightly in front, facing town
        go.transform.localPosition = new Vector3(3.2f, 0f, 4.8f);
        go.transform.localEulerAngles = new Vector3(0f, 160f, 0f);
        go.transform.localScale = Vector3.one * 0.38f;

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[PlaceGuard] Guard placed near GuardPost.");
    }
}
