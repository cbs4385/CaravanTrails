using UnityEngine;

/// Procedural idle animation — float, sway, head nod, arm swing.
/// Attach to a chibi character root; tune per character in the Inspector.
public class CharacterIdleAnim : MonoBehaviour
{
    [Header("Float")]
    public float bobAmplitude = 0.030f;
    public float bobFreq      = 0.90f;   // cycles/sec

    [Header("Sway")]
    public float swayDegrees  = 2.5f;
    public float swayFreq     = 0.55f;

    [Header("Head")]
    public string headName    = "Head";
    public float  headNodDeg  = 3.0f;
    public float  headNodFreq = 0.35f;

    [Header("Arms")]
    public string armLName    = "UpperArmL";
    public string armRName    = "UpperArmR";
    public float  armSwingDeg = 5.0f;
    public float  armSwingFreq= 0.90f;

    [Header("Phase")]
    public float phase = 0f;   // stagger so characters don't sync

    Vector3    _basePos;
    Quaternion _baseRot;
    Transform  _head;
    Quaternion _headBase;
    Transform  _armL, _armR;
    Quaternion _armLBase, _armRBase;

    void Start()
    {
        _basePos = transform.localPosition;
        _baseRot = transform.localRotation;

        _head = transform.Find(headName);
        if (_head != null) _headBase = _head.localRotation;

        _armL = transform.Find(armLName);
        _armR = transform.Find(armRName);
        if (_armL != null) _armLBase = _armL.localRotation;
        if (_armR != null) _armRBase = _armR.localRotation;
    }

    void Update()
    {
        float t = Time.time + phase;

        // Root float
        float bob = Mathf.Sin(t * bobFreq * Mathf.PI * 2f) * bobAmplitude;
        transform.localPosition = _basePos + new Vector3(0f, bob, 0f);

        // Root sway
        float sway = Mathf.Sin(t * swayFreq * Mathf.PI * 2f) * swayDegrees;
        transform.localRotation = _baseRot * Quaternion.Euler(0f, sway, 0f);

        // Head nod
        if (_head != null)
        {
            float nod = Mathf.Sin(t * headNodFreq * Mathf.PI * 2f) * headNodDeg;
            _head.localRotation = _headBase * Quaternion.Euler(nod, 0f, 0f);
        }

        // Arm breathing swing (opposite phase on each side)
        if (_armL != null)
        {
            float swing = Mathf.Sin(t * armSwingFreq * Mathf.PI * 2f) * armSwingDeg;
            _armL.localRotation = _armLBase * Quaternion.Euler( swing, 0f, 0f);
        }
        if (_armR != null)
        {
            float swing = Mathf.Sin(t * armSwingFreq * Mathf.PI * 2f) * armSwingDeg;
            _armR.localRotation = _armRBase * Quaternion.Euler(-swing, 0f, 0f);
        }
    }
}
