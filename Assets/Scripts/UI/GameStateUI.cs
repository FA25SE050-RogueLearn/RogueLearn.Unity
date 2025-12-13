using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using BossFight2D.Systems;
using BossFight2D.Core;

namespace BossFight2D.UI
{
  /// <summary>
  /// Manages Win and Lose overlays and music, driven by EventBus GameWon/GameLost/GameStarted.
  /// Provides choice panel on Lose (Retry, Main Menu) and similar on Win.
  /// </summary>
  public class GameStateUI : MonoBehaviour
  {
    [Header("Audio")]
    [Tooltip("Win music. If not assigned, will try to auto-load from Resources/Audio (e.g., 'Audio/win').")]
    public AudioClip winMusic;
    [Tooltip("Lose music. If not assigned, will try to auto-load from Resources/Audio (e.g., 'Audio/lose').")]
    public AudioClip loseMusic;
    [Tooltip("Gameplay theme music. If not assigned, will try to auto-load from Resources/Audio (e.g., 'Audio/gameplay' or 'Audio/theme').")]
    public AudioClip gameplayMusic;

    [Header("Typography")]
    [Tooltip("UI font override for Win/Lose panels. If not assigned, will try to auto-load Liberation Sans from Resources/Fonts.")]
    public Font uiFont;

    [Header("Appearance")]
    public Color panelColor = new Color(0f, 0f, 0f, 0.6f);
    public bool autoCreateUI = true;
    [Header("Scenes")]
    [Tooltip("If set, Main Menu button will load this scene.")]
    public string mainMenuSceneName = "ClientUI";

    Canvas _canvas; GameObject _winPanel; GameObject _losePanel; UnityEngine.UI.Text _winSummary; UnityEngine.UI.Text _loseSummary;
    AudioSource _audio;

    void Awake()
    {
      _audio = GetComponent<AudioSource>();
      if (_audio == null) { _audio = gameObject.AddComponent<AudioSource>(); }
      _audio.playOnAwake = false; _audio.loop = false; _audio.volume = 0.95f;
    }

    void OnEnable()
    {
      EventBus.GameWon += OnGameWon;
      EventBus.GameLost += OnGameLost;
      EventBus.GameStarted += OnGameStarted;
    }
    void OnDisable()
    {
      EventBus.GameWon -= OnGameWon;
      EventBus.GameLost -= OnGameLost;
      EventBus.GameStarted -= OnGameStarted;
    }

    void Start() { if (autoCreateUI) EnsureUIBuilt(); HideAll(); TryAutoPlayGameplayThemeOnSceneLoad(); }

    void OnGameStarted()
    {
      HideAll();
      // Play gameplay theme if available; attempt auto-load when null
      if (gameplayMusic == null) gameplayMusic = GetDefaultGameplayMusic();
      // Avoid restarting if the same clip is already playing (e.g., from scene auto-play)
      if (gameplayMusic != null)
      {
        if (_audio != null)
        {
          if (!_audio.isPlaying || _audio.clip != gameplayMusic) { _audio.clip = gameplayMusic; _audio.loop = true; _audio.Play(); }
        }
      }
    }
    void OnGameWon() { EnsureUIBuilt(); HideAll(); if (_winPanel != null) { _winPanel.SetActive(true); PopulateSummary(true); } if (winMusic == null) winMusic = GetDefaultWinMusic(); PlayClip(winMusic); }
    void OnGameLost() { EnsureUIBuilt(); HideAll(); if (_losePanel != null) { _losePanel.SetActive(true); PopulateSummary(false); } if (loseMusic == null) loseMusic = GetDefaultLoseMusic(); PlayClip(loseMusic); }

    void PlayClip(AudioClip clip) { if (clip == null) return; if (_audio != null) { _audio.clip = clip; _audio.loop = false; _audio.Play(); } }
    void StopMusic() { if (_audio != null && _audio.isPlaying) _audio.Stop(); }
    void HideAll() { if (_winPanel != null) _winPanel.SetActive(false); if (_losePanel != null) _losePanel.SetActive(false); }

    void EnsureUIBuilt()
    {
      if (_canvas == null) { _canvas = CreateOrGetGlobalCanvas(); }
      // If no font assigned, try to auto-load Liberation Sans from Resources/Fonts
      if (uiFont == null) { uiFont = GetDefaultUIFont(); }
      if (_winPanel == null) { _winPanel = BuildPanel("WinPanel", "You Win!", true); }
      if (_losePanel == null) { _losePanel = BuildPanel("LosePanel", "You Lose", false); }
      // Ensure there is an EventSystem so Win/Lose buttons can be clicked
      EnsureEventSystem();
    }

    // Attempt to auto-load default tracks from Resources/Audio when not set in Inspector
    AudioClip GetDefaultWinMusic()
    {
      var candidates = new string[] { "Audio/win", "Audio/victory", "Audio/Win" };
      foreach (var path in candidates) { var clip = Resources.Load<AudioClip>(path); if (clip != null) return clip; }
      return null;
    }
    AudioClip GetDefaultLoseMusic()
    {
      var candidates = new string[] { "Audio/lose", "Audio/defeat", "Audio/Lose" };
      foreach (var path in candidates) { var clip = Resources.Load<AudioClip>(path); if (clip != null) return clip; }
      return null;
    }
    AudioClip GetDefaultGameplayMusic()
    {
      var candidates = new string[] { "Audio/gameplay", "Audio/theme", "Audio/battle", "Audio/Gameplay", "Audio/Theme" };
      foreach (var path in candidates) { var clip = Resources.Load<AudioClip>(path); if (clip != null) return clip; }
      return null;
    }

    // Auto-play gameplay theme when entering a Gameplay scene, even before GameStarted
    void TryAutoPlayGameplayThemeOnSceneLoad()
    {
      var scene = SceneManager.GetActiveScene();
      // Heuristic: if scene name contains "Gameplay", start theme immediately
      if (scene.name.ToLower().Contains("gameplay"))
      {
        if (gameplayMusic == null) gameplayMusic = GetDefaultGameplayMusic();
        if (gameplayMusic != null && _audio != null && !_audio.isPlaying)
        {
          _audio.clip = gameplayMusic; _audio.loop = true; _audio.Play();
        }
      }
    }

    GameObject BuildPanel(string name, string title, bool isWin)
    {
      var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
      panel.transform.SetParent(_canvas.transform, false);
      var img = panel.GetComponent<Image>(); img.color = panelColor;
      var rt = panel.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

      var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
      titleGO.transform.SetParent(panel.transform, false);
      var titleText = titleGO.GetComponent<Text>(); titleText.text = title; titleText.color = Color.white; titleText.alignment = TextAnchor.MiddleCenter; titleText.fontSize = 44; if (uiFont != null) titleText.font = uiFont;
      var trt = titleGO.GetComponent<RectTransform>(); trt.anchorMin = new Vector2(0.1f, 0.6f); trt.anchorMax = new Vector2(0.9f, 0.85f); trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

      var sumGO = new GameObject("Summary", typeof(RectTransform), typeof(Text));
      sumGO.transform.SetParent(panel.transform, false);
      var sumText = sumGO.GetComponent<Text>(); sumText.text = ""; sumText.color = Color.white; sumText.alignment = TextAnchor.UpperLeft; sumText.fontSize = 24; if (uiFont != null) sumText.font = uiFont;
      var srt = sumGO.GetComponent<RectTransform>(); srt.anchorMin = new Vector2(0.1f, 0.35f); srt.anchorMax = new Vector2(0.9f, 0.6f); srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
      if (isWin) _winSummary = sumText; else _loseSummary = sumText;

      // Buttons row
      var retryBtn = BuildButton(panel.transform, "Retry", new Vector2(0.15f, 0.2f), new Vector2(0.45f, 0.35f));
      retryBtn.onClick.AddListener(OnRetryClicked);

      var menuBtn = BuildButton(panel.transform, "Main Menu", new Vector2(0.55f, 0.2f), new Vector2(0.85f, 0.35f));
      menuBtn.onClick.AddListener(OnMainMenuClicked);

      if (isWin)
      {
        // Optional extra button for win state (e.g., Continue). For now, we provide Retry/Main Menu only.
      }
      return panel;
    }

    Button BuildButton(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax)
    {
      var btnGO = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
      btnGO.transform.SetParent(parent, false);
      var btnImg = btnGO.GetComponent<Image>(); btnImg.color = new Color(1f, 1f, 1f, 0.15f);
      var brt = btnGO.GetComponent<RectTransform>(); brt.anchorMin = anchorMin; brt.anchorMax = anchorMax; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
      var btn = btnGO.GetComponent<Button>();

      var tGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
      tGO.transform.SetParent(btnGO.transform, false);
      var t = tGO.GetComponent<Text>(); t.text = text; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.fontSize = 28; if (uiFont != null) t.font = uiFont;
      var tr = tGO.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
      return btn;
    }

    void OnRetryClicked() { SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    void OnMainMenuClicked()
    {
      // If a scene name is provided, load it (separate Gameplay/MainMenu scenes)
      if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
      {
        StopMusic();
        SceneManager.LoadScene(mainMenuSceneName);
        return;
      }
      // Fallback: If a MainMenuUI exists in the same scene, show it; otherwise reload current scene
      var mm = GameObject.FindFirstObjectByType<MainMenuUI>();
      var gm = GameObjectFactory.FindOrCreate<GameManager>();
      if (mm != null) { HideAll(); mm.ShowMenu(); StopMusic(); }
      else { SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
      if (mm != null && gm != null)
      {
        // Since GameManager does not expose an Init method, the menu is shown and gameplay is idle until Start is pressed.
      }
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
      foreach (var path in candidates) { var font = Resources.Load<Font>(path); if (font != null) return font; }
      return null;
    }

    void PopulateSummary(bool isWin)
    {
      var gsc = BossFight2D.Systems.GameSessionClient.Instance;
      string res = string.Empty; string ts = string.Empty; BossFight2D.Systems.GameSessionClient.SummaryData sd = null;
      if (gsc != null && gsc.TryGetLatestCompletion(out res, out ts, out sd))
      {
        var sb = new System.Text.StringBuilder();
        sb.Append("Result: "); sb.Append(string.IsNullOrEmpty(res) ? (isWin ? "win" : "lose") : res); sb.Append("\n");
        if (!string.IsNullOrEmpty(ts)) { sb.Append("Time: "); sb.Append(ts); sb.Append("\n"); }
        if (sd != null && sd.topics != null && sd.topics.Count > 0)
        {
          foreach (var t in sd.topics) { sb.Append(t.topic); sb.Append(": "); sb.Append(t.correct); sb.Append("/"); sb.Append(t.total); sb.Append("\n"); }
        }
        var label = isWin ? _winSummary : _loseSummary; if (label != null) label.text = sb.ToString();
        return;
      }
      if (gsc != null)
      {
        gsc.StartCoroutine(gsc.FetchResultFromBackend((r, tt, s) =>
        {
          var sb = new System.Text.StringBuilder();
          sb.Append("Result: "); sb.Append(string.IsNullOrEmpty(r) ? (isWin ? "win" : "lose") : r); sb.Append("\n");
          if (!string.IsNullOrEmpty(tt)) { sb.Append("Time: "); sb.Append(tt); sb.Append("\n"); }
          if (s != null && s.topics != null && s.topics.Count > 0)
          {
            foreach (var t in s.topics) { sb.Append(t.topic); sb.Append(": "); sb.Append(t.correct); sb.Append("/"); sb.Append(t.total); sb.Append("\n"); }
          }
          var label = isWin ? _winSummary : _loseSummary; if (label != null) label.text = sb.ToString();
        }));
      }
    }
  }
}