using UnityEngine;
using TMPro;
using View.Button;

namespace View.UI.Game
{
    public class VictoryScreen : UIScreen
    {
        [SerializeField] private CustomButton _back;
        [SerializeField] private CustomButton _restart;

        [SerializeField] private Misc.SceneManagment.SceneLoader _sceneLoader;

        [SerializeField] private TMP_Text _currentScoreText;
        [SerializeField] private TMP_Text _totalScoreText;
        [SerializeField] private TMP_Text _buttonText;

        private void OnEnable()
        {
            _back.AddListener(OnBackClicked);
            _restart.AddListener(Restart);

            UpdateNextButtonText();
        }

        private void OnDisable()
        {
            _back.RemoveListener(OnBackClicked);
            _restart.RemoveListener(Restart);
        }

        public override void StartScreen()
        {
            base.StartScreen();

            var gameManager = GameCore.Core.GameManager.Instance;
            if (gameManager != null)
            {
                _currentScoreText.text = $"SCORE {gameManager.CurrentScore}";
                _totalScoreText.text = $"BEST {gameManager.BestScore}";
            }

            UpdateNextButtonText();
        }

        private void UpdateNextButtonText()
        {
            var gameManager = GameCore.Core.GameManager.Instance;
            var levelLoader = gameManager != null ? gameManager.GetLevelLoader() : null;

            if (_buttonText == null)
                return;

            if (levelLoader != null && levelLoader.HasNextLevel())
                _buttonText.text = "LOAD NEXT";
            else
                _buttonText.text = "BACK TO MENU";
        }

        private void OnBackClicked()
        {
            var gameManager = GameCore.Core.GameManager.Instance;
            var levelLoader = gameManager != null ? gameManager.GetLevelLoader() : null;

            if (levelLoader != null && levelLoader.HasNextLevel())
            {
                gameManager.OnNextLevel();
            }
            else
            {
                _sceneLoader.ChangeScene(Misc.Data.SceneConstants.MENU_SCENE);
            }

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