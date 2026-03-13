using UnityEngine;
using UnityEngine.UI;
using View.UI;
using View.Button;
using Misc.SceneManagment;

namespace View.UI.Menu
{
    public class LevelsScreen : UIScreen
    {
        [SerializeField] private SceneLoader _sceneLoader;
        [SerializeField] private LevelButton[] _levelButtons;

        [SerializeField] private CustomButton _back;

        private void OnEnable()
        {
            _back.AddListener(BackToMenu);
        }

        private void OnDisable()
        {
            _back.RemoveListener(BackToMenu);
        }

        private void Start()
        {
            InitLevels();
        }

        private void InitLevels()
        {
            int unlockedLevel = PlayerPrefs.GetInt(GameCore.Core.GameConstants.LAST_UNLOCKED_LEVEL_KEY, 0);

            for (int i = 0; i < _levelButtons.Length; i++)
            {
                bool isUnlocked = i <= unlockedLevel;
                _levelButtons[i].Init(i, isUnlocked, OnLevelSelected);
            }
        }

        private void OnLevelSelected(int levelIndex)
        {
            PlayerPrefs.SetInt(GameCore.Core.GameConstants.LAST_SELECTED_LEVEL_KEY, levelIndex);
            PlayerPrefs.Save();

            _sceneLoader.ChangeScene(Misc.Data.SceneConstants.GAME_SCENE);
            CloseScreen();
        }

        private void BackToMenu()
        {
            CloseScreen();
        }
    }

    [System.Serializable]
    public class LevelButton
    {
        [SerializeField] private CustomButton _button;
        [SerializeField] private Image _image;
        [SerializeField] private Sprite _onSprite;
        [SerializeField] private Sprite _offSprite;

        private int _index;
        private bool _isUnlocked;
        private System.Action<int> _onClick;

        public void Init(int index, bool isUnlocked, System.Action<int> onClick)
        {
            _index = index;
            _isUnlocked = isUnlocked;
            _onClick = onClick;

            _image.sprite = isUnlocked ? _onSprite : _offSprite;

            _button.RemoveListener(HandleClick);
            if (isUnlocked)
                _button.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            _onClick?.Invoke(_index);
        }
    }
}