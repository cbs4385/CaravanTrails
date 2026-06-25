using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameConfig config;

    void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
    }
}
