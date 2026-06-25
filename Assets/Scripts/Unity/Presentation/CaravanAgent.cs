using System;
using UnityEngine;

// §6.5: Visual-only caravan walker. Moves a wrapper GameObject along a waypoint path.
// The character mesh lives as a child, so CharacterIdleAnim bobs independently of travel.
[DisallowMultipleComponent]
public class CaravanAgent : MonoBehaviour
{
    public float Speed = 2.2f;

    Vector3[] _waypoints;
    int       _wpIdx;
    bool      _running;
    Action    _onComplete;

    public void Launch(Vector3[] waypoints, Action onComplete)
    {
        _waypoints        = waypoints;
        _wpIdx            = 1;
        _onComplete       = onComplete;
        _running          = true;
        transform.position = waypoints[0];
        transform.rotation = Quaternion.LookRotation(
            (waypoints[1] - waypoints[0]).normalized, Vector3.up);
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (!_running || _waypoints == null || _wpIdx >= _waypoints.Length) return;

        var target = _waypoints[_wpIdx];
        var dir    = target - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.02f)
        {
            _wpIdx++;
            if (_wpIdx >= _waypoints.Length) { Complete(); return; }
        }
        else
        {
            transform.position += dir.normalized * Speed * Time.deltaTime;
            transform.rotation  = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir.normalized, Vector3.up),
                10f * Time.deltaTime);
        }
    }

    void Complete()
    {
        _running = false;
        gameObject.SetActive(false);
        _onComplete?.Invoke();
    }
}
