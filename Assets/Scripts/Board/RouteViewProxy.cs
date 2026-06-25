using UnityEngine;

// Attached to the collider child of each RouteView so SelectionManager can identify it
public class RouteViewProxy : MonoBehaviour
{
    public RouteView owner;
    public TradeRoute Route => owner != null ? owner.route : null;
}
