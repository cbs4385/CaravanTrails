using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Replaces each blockout primitive under Town/ with the matching Blender GLB.
// Run via menu: Tools > Wire Town Art
public static class WireTownArt
{
    // (name, glbPath, worldPos, worldRot, localScale)
    // Gate also needs the two GateTower siblings removed.
    struct DistrictEntry
    {
        public string name;
        public string glb;
        public Vector3 pos;
        public Vector3 rot;        // Euler Y rotation
        public Vector3 scale;
        public bool   removeSiblings; // true for Gate (removes GateTower x2)
    }

    [MenuItem("Tools/Wire Town Art")]
    public static void Run()
    {
        var town = GameObject.Find("Town");
        if (town == null) { Debug.LogError("Town not found in scene."); return; }

        // All models sit at Y=0 (ground); XZ from blockout centres.
        // Gate: Blender entrance faces +Y so needs 180° Y-rotation to face –Z (into town).
        // GuardPost: X/Z scale 0.4, Y driven by TownPresenter.ScaleGuard each tick.
        var entries = new DistrictEntry[]
        {
            new DistrictEntry { name="Market",        glb="Assets/Art/Market.glb",
                pos=new Vector3(  0f, 0f,  1.0f), rot=Vector3.zero,       scale=Vector3.one },
            new DistrictEntry { name="Palace",        glb="Assets/Art/Palace.glb",
                pos=new Vector3(  0f, 0f, -5.5f), rot=Vector3.zero,       scale=Vector3.one },
            new DistrictEntry { name="Gate",          glb="Assets/Art/Gate.glb",
                pos=new Vector3(  0f, 0f,  7.5f), rot=new Vector3(0,180,0), scale=Vector3.one,
                removeSiblings=true },
            new DistrictEntry { name="GuardPost",     glb="Assets/Art/GuardPost.glb",
                pos=new Vector3(  4f, 0f,  5.5f), rot=Vector3.zero,       scale=new Vector3(0.4f,0.4f,0.4f) },
            new DistrictEntry { name="ShadowDistrict",glb="Assets/Art/ShadowDistrict.glb",
                pos=new Vector3( -5f, 0f,  1.5f), rot=Vector3.zero,       scale=Vector3.one },
            new DistrictEntry { name="Fountain",      glb="Assets/Art/Fountain.glb",
                pos=new Vector3(-1.5f,0f,  2.5f), rot=Vector3.zero,       scale=Vector3.one },
        };

        foreach (var e in entries)
        {
            // ── Load the GLB prefab ────────────────────────────────────────
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.glb);
            if (prefab == null)
            {
                Debug.LogError($"[WireTownArt] Could not load {e.glb}. Run Assets > Refresh first.");
                continue;
            }

            // ── Remove old blockout child(ren) ─────────────────────────────
            var old = town.transform.Find(e.name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            if (e.removeSiblings)
            {
                // Gate blockout: also kill the two GateTower cubes
                var children = new System.Collections.Generic.List<Transform>();
                for (int i = 0; i < town.transform.childCount; i++)
                {
                    var c = town.transform.GetChild(i);
                    if (c.name == "GateTower") children.Add(c);
                }
                foreach (var c in children) Undo.DestroyObjectImmediate(c.gameObject);
            }

            // ── Instantiate GLB ────────────────────────────────────────────
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, $"Wire {e.name}");

            go.name = e.name;
            go.transform.SetParent(town.transform, false);
            go.transform.localPosition = e.pos;
            go.transform.localEulerAngles = e.rot;
            go.transform.localScale = e.scale;

            // ShadowDistrict starts hidden (RefreshShadow hides it on first tick anyway,
            // but match the original blockout state)
            if (e.name == "ShadowDistrict") go.SetActive(false);

            Debug.Log($"[WireTownArt] Placed {e.name} from {e.glb}");
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        Debug.Log("[WireTownArt] Done. Review the scene, then save.");
    }
}
