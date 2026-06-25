using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverPanel : MonoBehaviour
{
    public TextMeshProUGUI subtitleLabel;
    public Button          restartButton;

    void Awake()
    {
        EventBus.OnGameOver += Show;
        restartButton.onClick.AddListener(Restart);
    }

    void OnDestroy() => EventBus.OnGameOver -= Show;

    void Show(int day)
    {
        gameObject.SetActive(true);
        float balance = PersonalAccount.Instance.Balance;
        subtitleLabel.text =
            $"Your trading empire collapsed on Day {day}.\n" +
            $"You had {balance:F0}g — not enough to cover living costs.\n" +
            $"Raise your tax rate and invest in trade to survive.";
    }

    static void Restart() =>
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
}
