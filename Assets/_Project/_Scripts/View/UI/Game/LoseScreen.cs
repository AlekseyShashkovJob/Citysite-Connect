using UnityEngine;
using TMPro;
using View.Button;

namespace View.UI.Game
{
    public class LoseScreen : UIScreen
    {
        [SerializeField] private CustomButton _back;
        [SerializeField] private CustomButton _restart;

        [SerializeField] private Misc.SceneManagment.SceneLoader _sceneLoader;

        [SerializeField] private TMP_Text _currentScoreText;
        [SerializeField] private TMP_Text _totalScoreText;

        private void OnEnable()
        {
            _back.AddListener(BackToMenu);
            _restart.AddListener(Restart);
        }

        private void OnDisable()
        {
            _back.RemoveListener(BackToMenu);
            _restart.RemoveListener(Restart);
        }

        public override void StartScreen()
        {
            base.StartScreen();

            Time.timeScale = 0.0f;

            _currentScoreText.text = $"SCORE {GameCore.Core.GameManager.Instance.CurrentScore}";
            _totalScoreText.text = $"BEST {GameCore.Core.GameManager.Instance.BestScore}";
        }

        private void BackToMenu()
        {
            Time.timeScale = 1.0f;
            _sceneLoader.ChangeScene(Misc.Data.SceneConstants.MENU_SCENE);
            CloseScreen();
        }

        private void Restart()
        {
            Time.timeScale = 1.0f;
            GameCore.Core.GameManager.Instance.RestartGame();
            CloseScreen();
        }
    }
}