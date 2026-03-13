using UnityEngine;
using Cysharp.Threading.Tasks;

namespace View.UI
{
    public class EntryPoint : MonoBehaviour
    {
        public enum AppMode { None, WebView, Game }

        [SerializeField] private UIScreen _gameMenu;
        [SerializeField] private UIScreen _internet;
        [SerializeField] private GameObject _loading;

        private bool _allowOrientationChange = true;
        private AppMode _mode;

        private void Awake()
        {
            _mode = (AppMode)PlayerPrefs.GetInt(Misc.Data.ServicesConstants.WEB_VIEW_MODE_KEY, (int)AppMode.None);
        }

        private void Start()
        {
            ProceedAppFlow().Forget();
        }

        private void Update()
        {
            if (_allowOrientationChange)
            {
                HandleOrientationChange();
            }
        }

        public async UniTask ProceedAppFlow()
        {
            _loading.SetActive(true);

            if (_mode == AppMode.None)
            {
                await FirstLaunch();
            }
            else
            {
                _loading.SetActive(false);
                StopOrientationChange();
                _gameMenu.StartScreen();
            }
        }

        private async UniTask FirstLaunch()
        {
            if (!await IsInternetAvailable())
            {
                _loading.SetActive(false);
                _internet.StartScreen();
                return;
            }

            _loading.SetActive(false);
            StopOrientationChange();
            _gameMenu.StartScreen();
        }

        private void HandleOrientationChange()
        {
            var currentOrientation = Input.deviceOrientation;

            if (currentOrientation == DeviceOrientation.LandscapeLeft && Screen.orientation != ScreenOrientation.LandscapeLeft)
                Screen.orientation = ScreenOrientation.LandscapeLeft;
            else if (currentOrientation == DeviceOrientation.LandscapeRight && Screen.orientation != ScreenOrientation.LandscapeRight)
                Screen.orientation = ScreenOrientation.LandscapeRight;
            else if (currentOrientation == DeviceOrientation.Portrait && Screen.orientation != ScreenOrientation.Portrait)
                Screen.orientation = ScreenOrientation.Portrait;
            else if (currentOrientation == DeviceOrientation.PortraitUpsideDown && Screen.orientation != ScreenOrientation.PortraitUpsideDown)
                Screen.orientation = ScreenOrientation.PortraitUpsideDown;
        }

        private void StopOrientationChange()
        {
            _allowOrientationChange = false;

            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToPortrait = true;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        private async UniTask<bool> IsInternetAvailable() =>
            await new Misc.Services.InternetChecker().IsAvailableAsync();
    }
}