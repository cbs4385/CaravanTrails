using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PlaceMerchant
{
    [MenuItem("Tools/Place Merchant")]
    public static void Run()
    {
        var town = GameObject.Find("Town");
        if (town == null) { Debug.LogError("Town not found."); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Merchant.glb");
        if (prefab == null) { Debug.LogError("Assets/Art/Merchant.glb not found — run Assets > Refresh first."); return; }

        // Remove existing if re-running
        var existing = town.transform.Find("Merchant");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Place Merchant");

        go.name = "Merchant";
        go.transform.SetParent(town.transform, false);

        // Near the Market (Market is at local 0,0,1.0) — offset to the right
        go.transform.localPosition = new Vector3(1.8f, 0f, 1.5f);
        go.transform.localEulerAngles = new Vector3(0f, 200f, 0f); // facing slightly toward market
        go.transform.localScale = Vector3.one * 0.38f;

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[PlaceMerchant] Merchant placed near Market.");
    }
}
