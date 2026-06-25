using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PlaceShadyFigure
{
    [MenuItem("Tools/Place Shady Figure")]
    public static void Run()
    {
        var town = GameObject.Find("Town");
        if (town == null) { Debug.LogError("Town not found."); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/ShadyFigure.glb");
        if (prefab == null) { Debug.LogError("Assets/Art/ShadyFigure.glb not found."); return; }

        var existing = town.transform.Find("ShadyFigure");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Place Shady Figure");

        go.name = "ShadyFigure";
        go.transform.SetParent(town.transform, false);

        // Near ShadowDistrict (at local -5, 0, 1.5) — lurking in a doorway
        go.transform.localPosition = new Vector3(-4.2f, 0f, 0.8f);
        go.transform.localEulerAngles = new Vector3(0f, 45f, 0f);
        go.transform.localScale = Vector3.one * 0.38f;

        // ShadyFigure starts hidden — matches ShadowDistrict visibility rule
        go.SetActive(false);

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[PlaceShadyFigure] Shady Figure placed (hidden) near ShadowDistrict.");
    }
}
