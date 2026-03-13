using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using GameCore.Level;

namespace GameCore.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public int CurrentScore { get; private set; }
        public int BestScore { get; private set; }

        [SerializeField] private View.UIScreen _winScreen;
        [SerializeField] private View.UIScreen _loseScreen;
        [SerializeField] private Misc.SceneManagment.SceneLoader _sceneLoader;
        [SerializeField] private TMP_Text _greenText;
        [SerializeField] private TMP_Text _yellowText;
        [SerializeField] private TMP_Text _purpleText;
        [SerializeField] private TMP_Text _pinkText;
        [SerializeField] private TMP_Text _orangeText;
        [SerializeField] private TMP_Text _blueText;
        [SerializeField] private BuildingGridManager _buildingGridManager;
        [SerializeField] private LevelLoader _levelLoader;

        private int[] _buildingGoals;
        private int[] _buildingCollected;
        private int _moves = 0;
        private bool _isGameFinished = false;

        private int CurrentLevelIndex => _levelLoader != null ? _levelLoader.CurrentLevel : 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadPerLevelBestForCurrentOrDefault();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            Time.timeScale = 1.0f;
            StartLevel(CurrentLevelIndex);
        }

        public void StartLevel(int levelIndex)
        {
            CurrentScore = 0;
            _moves = 0;
            _isGameFinished = false;

            if (_levelLoader != null)
                _levelLoader.LoadLevel(levelIndex);

            int enumColorCount = Enum.GetNames(typeof(Level.BuildingColor)).Length;
            int spriteColorCount = (_buildingGridManager != null && _buildingGridManager.BuildingSprites != null) ? _buildingGridManager.BuildingSprites.Length : enumColorCount;
            int colorCount = Mathf.Max(1, Mathf.Min(enumColorCount, spriteColorCount));

            _buildingGoals = new int[colorCount];
            _buildingCollected = new int[colorCount];

            int goalPerColor = Mathf.Clamp(levelIndex + 2, 1, 999);
            for (int i = 0; i < colorCount; i++)
                _buildingGoals[i] = goalPerColor;

            BestScore = PlayerPrefs.GetInt($"{GameConstants.LEVEL_BEST_SCORE_KEY}_{CurrentLevelIndex}", 0);

            if (_buildingGridManager != null)
            {
                _buildingGridManager.GenerateGrid();
            }

            UpdateScoreUI();
        }

        public void CollectBuildings(List<Level.Building> buildings)
        {
            if (_isGameFinished) return;
            if (buildings == null || buildings.Count < 2) return;

            _moves++;

            EnsureArraysInitialized();

            int[] collectPerColor = new int[_buildingCollected.Length];
            for (int i = 0; i < buildings.Count; i++)
            {
                int idx = (int)buildings[i].Color;
                if (idx < 0 || idx >= collectPerColor.Length) continue;
                collectPerColor[idx]++;
            }

            for (int i = 0; i < _buildingCollected.Length; i++)
            {
                _buildingCollected[i] += collectPerColor[i];
                if (_buildingCollected[i] > _buildingGoals[i])
                    _buildingCollected[i] = _buildingGoals[i];
            }

            int scoringBuildings = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                int idx = (int)buildings[i].Color;
                if (idx < 0 || idx >= _buildingCollected.Length) continue;
                if (_buildingCollected[idx] - collectPerColor[idx] < _buildingGoals[idx])
                    scoringBuildings++;
            }

            int comboBonus = scoringBuildings >= 5 ? Mathf.RoundToInt(10 * (scoringBuildings - 4) * 0.8f) : 0;
            int baseScore = Mathf.RoundToInt(scoringBuildings * 10 * 0.8f);
            int moveModifier = Mathf.Max(1, 20 - _moves);

            AddScore(baseScore * moveModifier + comboBonus);

            if (_buildingGridManager != null)
            {
                _buildingGridManager.RemoveBuildings(buildings);
                _buildingGridManager.CollapseAndRefill();
            }

            UpdateScoreUI();

            if (IsWin())
            {
                FinishGame();
            }
        }

        public void AddScore(int amount)
        {
            CurrentScore += amount;
            if (CurrentScore > BestScore)
                BestScore = CurrentScore;

            SaveBestForCurrentLevel();
            UpdateScoreUI();
        }

        public void RestartGame()
        {
            SaveBestForCurrentLevel();
            _sceneLoader.ChangeScene(Misc.Data.SceneConstants.GAME_SCENE);
        }

        public void FinishGame()
        {
            _isGameFinished = true;
            Time.timeScale = 0.0f;

            if (CurrentScore > BestScore)
                BestScore = CurrentScore;

            SaveBestForCurrentLevel();
            UnlockNextLevel(CurrentLevelIndex);

            _winScreen?.StartScreen();

            CurrentScore = 0;
        }

        public void LoseGame()
        {
            _isGameFinished = true;
            Time.timeScale = 0.0f;

            if (CurrentScore > BestScore)
                BestScore = CurrentScore;

            SaveBestForCurrentLevel();
            _loseScreen?.StartScreen();

            CurrentScore = 0;
        }

        public void UpdateScoreUI()
        {
            if (_buildingCollected == null || _buildingGoals == null) return;

            if (_greenText != null && _buildingCollected.Length > 0) _greenText.text = $"{_buildingCollected[0]}/{_buildingGoals[0]}";
            if (_yellowText != null && _buildingCollected.Length > 1) _yellowText.text = $"{_buildingCollected[1]}/{_buildingGoals[1]}";
            if (_purpleText != null && _buildingCollected.Length > 2) _purpleText.text = $"{_buildingCollected[2]}/{_buildingGoals[2]}";
            if (_pinkText != null && _buildingCollected.Length > 3) _pinkText.text = $"{_buildingCollected[3]}/{_buildingGoals[3]}";
            if (_orangeText != null && _buildingCollected.Length > 4) _orangeText.text = $"{_buildingCollected[4]}/{_buildingGoals[4]}";
            if (_blueText != null && _buildingCollected.Length > 5) _blueText.text = $"{_buildingCollected[5]}/{_buildingGoals[5]}";
        }

        // Возвращает ссылку на LevelLoader (для экранов UI)
        public LevelLoader GetLevelLoader() => _levelLoader;

        public void OnNextLevel()
        {
            int next = CurrentLevelIndex + 1;
            int total = _levelLoader != null ? _levelLoader.TotalLevels() : 9;
            if (next >= total)
            {
                FinishGame();
            }
            else
            {
                StartLevel(next);
            }
        }

        private void SaveBestForCurrentLevel()
        {
            string key = $"{GameConstants.LEVEL_BEST_SCORE_KEY}_{CurrentLevelIndex}";
            int saved = PlayerPrefs.GetInt(key, 0);
            if (BestScore > saved)
            {
                PlayerPrefs.SetInt(key, BestScore);
                PlayerPrefs.Save();
            }
        }

        private void UnlockNextLevel(int currentLevel)
        {
            int lastUnlocked = PlayerPrefs.GetInt(GameConstants.LAST_UNLOCKED_LEVEL_KEY, 0);
            if (currentLevel >= lastUnlocked)
            {
                int next = currentLevel + 1;
                int total = _levelLoader != null ? _levelLoader.TotalLevels() : 9;
                if (next < total)
                {
                    PlayerPrefs.SetInt(GameConstants.LAST_UNLOCKED_LEVEL_KEY, next);
                    PlayerPrefs.Save();
                }
            }
        }

        private void LoadPerLevelBestForCurrentOrDefault()
        {
            int selected = PlayerPrefs.GetInt(GameConstants.LAST_SELECTED_LEVEL_KEY, 0);
            BestScore = PlayerPrefs.GetInt($"{GameConstants.LEVEL_BEST_SCORE_KEY}_{selected}", 0);
        }

        private bool IsWin()
        {
            if (_buildingCollected == null || _buildingGoals == null) return false;
            int len = Mathf.Min(_buildingCollected.Length, _buildingGoals.Length);
            for (int i = 0; i < len; i++)
            {
                if (_buildingCollected[i] < _buildingGoals[i])
                    return false;
            }
            return true;
        }

        private void EnsureArraysInitialized()
        {
            if (_buildingGoals != null && _buildingCollected != null) return;

            int enumColorCount = Enum.GetNames(typeof(BuildingColor)).Length;
            int spriteColorCount = (_buildingGridManager != null && _buildingGridManager.BuildingSprites != null) ? _buildingGridManager.BuildingSprites.Length : enumColorCount;
            int colorCount = Mathf.Max(1, Mathf.Min(enumColorCount, spriteColorCount));

            _buildingGoals = new int[colorCount];
            _buildingCollected = new int[colorCount];
            int selectedLevel = _levelLoader != null ? _levelLoader.CurrentLevel : 0;
            int goalPerColor = Mathf.Clamp(selectedLevel + 2, 1, 999);
            for (int i = 0; i < colorCount; i++)
                _buildingGoals[i] = goalPerColor;
        }

        private string StringifyArray(int[] arr)
        {
            if (arr == null) return "null";
            return "[" + string.Join(", ", Array.ConvertAll(arr, x => x.ToString())) + "]";
        }
    }
}