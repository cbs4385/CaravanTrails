using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    public Town       SelectedTown  { get; private set; }
    public TradeRoute SelectedRoute { get; private set; }

    void Awake() => Instance = this;

    void Update()
    {
        if (!ClickedThisFrame(out Vector2 screenPos)) return;

        // Ignore clicks that land on UI elements
        // Pass -1 (primary pointer) — works for both legacy and new InputSystem
        if (UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(-1))
            return;

        var ray = Camera.main.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0));

        if (Physics.Raycast(ray, out var hit, 200f))
        {
            var tv = hit.collider.GetComponent<TownView>();
            if (tv != null)
            {
                if (PlacementManager.Instance.IsPlacing)
                    PlacementManager.Instance.TryPlaceOnTown(tv.town);
                else
                    SelectTown(tv.town);
                return;
            }

            // Route colliders use a proxy component on a child GO
            var proxy = hit.collider.GetComponent<RouteViewProxy>();
            if (proxy?.Route != null)
            {
                if (PlacementManager.Instance.IsPlacing)
                    PlacementManager.Instance.TryPlaceOnRoute(proxy.Route);
                else
                    SelectRoute(proxy.Route);
                return;
            }
        }

        if (PlacementManager.Instance.IsPlacing)
            PlacementManager.Instance.Cancel();
        else
            ClearSelection();
    }

    static bool ClickedThisFrame(out Vector2 pos)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            pos = mouse.position.ReadValue();
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            pos = Input.mousePosition;
            return true;
        }
#endif
        pos = default;
        return false;
    }

    public void SelectTown(Town town)
    {
        SelectedTown  = town;
        SelectedRoute = null;
        EventBus.TriggerTownSelected(town);
    }

    public void SelectRoute(TradeRoute route)
    {
        SelectedRoute = route;
        SelectedTown  = null;
        EventBus.TriggerRouteSelected(route);
    }

    public void ClearSelection()
    {
        SelectedTown  = null;
        SelectedRoute = null;
        EventBus.TriggerSelectionCleared();
    }
}
