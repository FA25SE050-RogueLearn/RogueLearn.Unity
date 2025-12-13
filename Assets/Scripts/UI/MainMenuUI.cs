using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using BossFight2D.Systems;
using BossFight2D.Core;

namespace BossFight2D.UI
{
  /// <summary>
  /// Simple Main Menu overlay shown while GameManager.State == Init.
  /// Provides a Start button to transition into gameplay and optional menu music.
  /// </summary>
  public class MainMenuUI : MonoBehaviour
  {
    [Header("Audio")]
    [Tooltip("Menu music. If not assigned, will try to auto-load from Resources/Audio (e.g., 'Audio/menu').")]
    public AudioClip menuMusic;

    [Header("Typography")]
    [Tooltip("UI font override. If not assigned, will try to auto-load Liberation Sans from Resources/Fonts.")]
    public Font uiFont;

    [Header("Appearance")]
    public string titleText = "Rogue Learn";
    public string startButtonText = "Start";
    public Color panelColor = new Color(0f, 0f, 0f, 0.6f);
    public bool autoCreateUI = true;
    [Header("Scenes")]
    [Tooltip("If set, Start will load this Gameplay scene instead of calling GameManager.StartGame in-place.")]
    public string gameplaySceneName;

    Canvas _canvas; GameObject _panel; Button _startBtn; Text _title;
    AudioSource _audio;

    void Awake()
    {
      // Ensure an AudioSource exists
      _audio = GetComponent<AudioSource>();
      if (_audio == null) { _audio = gameObject.AddComponent<AudioSource>(); }
      _audio.playOnAwake = false; _audio.loop = false; _audio.volume = 0.9f;
    }

    void OnEnable()
    {
      EventBus.GameStarted += OnGameStarted;
    }
    void OnDisable()
    {
      EventBus.GameStarted -= OnGameStarted;
    }

    void Start()
    {
      if (autoCreateUI) EnsureUIBuilt();
      ShowIfInitState();
    }

    void ShowIfInitState()
    {
      var gm = GameObjectFactory.FindOrCreate<GameManager>();
      if (gm != null && gm.State == GameState.Init) { ShowMenu(); }
      else { HideMenu(); }
    }

    void OnGameStarted() { HideMenu(); StopMusic(); }

    public void ShowMenu()
    {
      EnsureUIBuilt();
      if (_panel != null) { _panel.SetActive(true); }
      PlayMusic();
    }
    public void HideMenu() { if (_panel != null) _panel.SetActive(false); }

    void PlayMusic() { if (menuMusic == null) return; if (_audio != null) { _audio.clip = menuMusic; _audio.loop = true; if (!_audio.isPlaying) _audio.Play(); } }
    void StopMusic() { if (_audio != null && _audio.isPlaying) { _audio.Stop(); } }

    // Attempt to auto-load a default menu track from Resources/Audio when not set in Inspector
    AudioClip GetDefaultMenuMusic()
    {
      // Common filename guesses: menu, main_menu, menu_music
      var candidates = new string[]{
        "Audio/menu",
        "Audio/main_menu",
        "Audio/menu_music",
        "Audio/Menu"
      };
      foreach (var path in candidates)
      {
        var clip = Resources.Load<AudioClip>(path);
        if (clip != null) return clip;
      }
      return null;
    }

    void EnsureUIBuilt()
    {
      // If no menuMusic assigned, try to auto-load one from Resources/Audio
      if (menuMusic == null) { menuMusic = GetDefaultMenuMusic(); }
      // Ensure there is an EventSystem so UI can receive clicks
      EnsureEventSystem();
      // If no font assigned, try to auto-load Liberation Sans from Resources/Fonts
      if (uiFont == null) { uiFont = GetDefaultUIFont(); }
      if (_canvas == null) { _canvas = CreateOrGetGlobalCanvas(); }
      if (_panel == null)
      {
        _panel = new GameObject("MainMenuPanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(_canvas.transform, false);
        var img = _panel.GetComponent<Image>(); img.color = panelColor;
        var rt = _panel.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(_panel.transform, false);
        _title = titleGO.GetComponent<Text>(); _title.text = titleText; _title.color = Color.white; _title.alignment = TextAnchor.MiddleCenter; _title.fontSize = 48; if (uiFont != null) _title.font = uiFont;
        var trt = titleGO.GetComponent<RectTransform>(); trt.anchorMin = new Vector2(0.1f, 0.6f); trt.anchorMax = new Vector2(0.9f, 0.9f); trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        var btnGO = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(_panel.transform, false);
        var btnImg = btnGO.GetComponent<Image>(); btnImg.color = new Color(1f, 1f, 1f, 0.15f);
        _startBtn = btnGO.GetComponent<Button>(); _startBtn.onClick.AddListener(OnStartClicked);
        var brt = btnGO.GetComponent<RectTransform>(); brt.anchorMin = new Vector2(0.35f, 0.2f); brt.anchorMax = new Vector2(0.65f, 0.35f); brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

        var btnTextGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        btnTextGO.transform.SetParent(btnGO.transform, false);
        var t = btnTextGO.GetComponent<Text>(); t.text = startButtonText; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.fontSize = 28; if (uiFont != null) t.font = uiFont;
        var tr = btnTextGO.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;

      }
    }

    void EnsureEventSystem()
    {
      var es = GameObject.FindFirstObjectByType<EventSystem>();
      if (es == null)
      {
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
      }
    }

    // Attempt to auto-load Liberation Sans font from Resources/Fonts
    Font GetDefaultUIFont()
    {
      var candidates = new string[]{
        "Fonts/LiberationSans",
        "Fonts/Liberation Sans",
        "Fonts/liberation-sans",
        "Fonts/LiberationSans-Regular",
        "Fonts/LiberationSans-Bold"
      };
      foreach (var path in candidates)
      {
        var font = Resources.Load<Font>(path);
        if (font != null) return font;
      }
      return null;
    }

    void OnStartClicked()
    {
      // If a gameplay scene name is provided, load it (separate MainMenu/Gameplay scenes)
      if (!string.IsNullOrEmpty(gameplaySceneName))
      {
        StopMusic();
        SceneManager.LoadScene(gameplaySceneName);
        return;
      }
      // Otherwise, start game in-place
      var gm = GameObjectFactory.FindOrCreate<GameManager>();
      gm?.StartGame();
    }

    Canvas CreateOrGetGlobalCanvas()
    {
      var existing = GameObject.Find("GlobalUIRoot");
      if (existing == null)
      {
        existing = new GameObject("GlobalUIRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = existing.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 1000;
        var cs = existing.GetComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080);
      }
      return existing.GetComponent<Canvas>();
    }
  }
}
