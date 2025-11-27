using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using BossFight2D.Core;

namespace BossFight2D.UI
{
  /// <summary>
  /// PauseMenu: toggled with Escape during gameplay. Provides Resume, Settings (Master Volume), and Main Menu.
  /// </summary>
  public class PauseMenu : MonoBehaviour
  {
    [Header("Input")]
    public KeyCode toggleKey = KeyCode.Escape;

    [Header("Font")]
    public Font font;

    [Header("Settings")]
    [Tooltip("Scene name to load when clicking Main Menu. Leave empty to reload current scene.")]
    public string mainMenuSceneName = "ClientUI";

    GameObject _panel;
    Slider _volumeSlider;
    Text _title;
    Button _resumeBtn, _menuBtn;
    Canvas _canvas;

    const string PrefVolume = "MasterVolume";

    void Awake() { EnsureUIBuilt(); ApplySavedVolume(); Hide(); }

    void Update()
    {
      var gm = GameObjectFactory.FindOrCreate<GameManager>();
      if (gm == null) return;
      // Allow toggling in Init and Playing, block only in Win/Lose
      if (Input.GetKeyDown(toggleKey))
      {
        if (gm.State == GameState.Paused) { gm.ResumeGame(); Hide(); }
        else if (gm.State != GameState.Win && gm.State != GameState.Lose)
        {
          Show();
          // Pause the game when opening from Init or Playing
          gm.PauseGame();
        }
      }
    }

    void EnsureUIBuilt()
    {
      _canvas = CreateOrGetGlobalCanvas();
      _panel = GameObject.Find("PauseMenuPanel");
      if (_panel == null) { _panel = BuildPanel(_canvas.transform); }
      // If no font assigned, try to auto-load a default UI font
      if (font == null) { font = GetDefaultUIFont(); }
      // Wire up buttons
      _resumeBtn.onClick.RemoveAllListeners();
      _resumeBtn.onClick.AddListener(OnResumeClicked);
      _menuBtn.onClick.RemoveAllListeners();
      _menuBtn.onClick.AddListener(OnMainMenuClicked);
      _volumeSlider.onValueChanged.RemoveAllListeners();
      _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
      EnsureEventSystem();
    }

    void Show() { if (_panel != null) _panel.SetActive(true); }
    void Hide() { if (_panel != null) _panel.SetActive(false); }

    void OnResumeClicked() { var gm = GameObjectFactory.FindOrCreate<GameManager>(); if (gm != null) { gm.ResumeGame(); } Hide(); }
    void OnMainMenuClicked()
    {
      var gm = GameObjectFactory.FindOrCreate<GameManager>();
      if (gm != null && gm.State == GameState.Paused) { gm.ResumeGame(); }
      // Try to load the configured menu scene; if it isn't in the build, fall back to reloading current
      if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        SceneManager.LoadScene(mainMenuSceneName);
      else
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnVolumeChanged(float v) { AudioListener.volume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(PrefVolume, AudioListener.volume); PlayerPrefs.Save(); }
    void ApplySavedVolume() { if (PlayerPrefs.HasKey(PrefVolume)) { AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefVolume)); } else { AudioListener.volume = 1f; } if (_volumeSlider != null) _volumeSlider.value = AudioListener.volume; }

    GameObject BuildPanel(Transform parent)
    {
      var panel = new GameObject("PauseMenuPanel", typeof(RectTransform), typeof(Image));
      panel.transform.SetParent(parent, false);
      var img = panel.GetComponent<Image>(); img.color = new Color(0f, 0f, 0f, 0.6f);
      var rt = panel.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

      var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
      titleGO.transform.SetParent(panel.transform, false);
      _title = titleGO.GetComponent<Text>(); _title.text = "Paused"; _title.color = Color.white; _title.alignment = TextAnchor.MiddleCenter; _title.fontSize = 40; if (font != null) _title.font = font;
      var trt = titleGO.GetComponent<RectTransform>(); trt.anchorMin = new Vector2(0.1f, 0.72f); trt.anchorMax = new Vector2(0.9f, 0.9f); trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

      // Resume Button
      _resumeBtn = BuildButton(panel.transform, "Resume", new Vector2(0.35f, 0.55f), new Vector2(0.65f, 0.65f));
      // Volume Slider
      var volGO = new GameObject("VolumeRow", typeof(RectTransform)); volGO.transform.SetParent(panel.transform, false);
      var vrt = volGO.GetComponent<RectTransform>(); vrt.anchorMin = new Vector2(0.1f, 0.42f); vrt.anchorMax = new Vector2(0.9f, 0.52f); vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
      var volLabelGO = new GameObject("Label", typeof(RectTransform), typeof(Text)); volLabelGO.transform.SetParent(volGO.transform, false);
      var vlt = volLabelGO.GetComponent<Text>(); vlt.text = "Master Volume"; vlt.color = Color.white; vlt.alignment = TextAnchor.MiddleLeft; vlt.fontSize = 24; if (font != null) vlt.font = font;
      var vlrt = volLabelGO.GetComponent<RectTransform>(); vlrt.anchorMin = new Vector2(0f, 0f); vlrt.anchorMax = new Vector2(0.35f, 1f); vlrt.offsetMin = Vector2.zero; vlrt.offsetMax = Vector2.zero;
      var sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider)); sliderGO.transform.SetParent(volGO.transform, false);
      _volumeSlider = sliderGO.GetComponent<Slider>(); _volumeSlider.minValue = 0f; _volumeSlider.maxValue = 1f; _volumeSlider.value = AudioListener.volume; _volumeSlider.wholeNumbers = false;
      var srt = sliderGO.GetComponent<RectTransform>(); srt.anchorMin = new Vector2(0.4f, 0f); srt.anchorMax = new Vector2(1f, 1f); srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
      // Slider visuals
      var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image)); bgGO.transform.SetParent(sliderGO.transform, false);
      var bgRT = bgGO.GetComponent<RectTransform>(); bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero; var bgImg = bgGO.GetComponent<Image>(); bgImg.color = new Color(0f, 0f, 0f, 0.35f);
      var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform)); fillAreaGO.transform.SetParent(sliderGO.transform, false);
      var faRT = fillAreaGO.GetComponent<RectTransform>(); faRT.anchorMin = new Vector2(0f, 0f); faRT.anchorMax = new Vector2(1f, 1f); faRT.offsetMin = new Vector2(4f, 4f); faRT.offsetMax = new Vector2(-4f, -4f);
      var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fillGO.transform.SetParent(fillAreaGO.transform, false);
      var fillRT = fillGO.GetComponent<RectTransform>(); fillRT.anchorMin = new Vector2(0f, 0f); fillRT.anchorMax = new Vector2(1f, 1f); fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero; var fillImg = fillGO.GetComponent<Image>(); fillImg.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);
      var slider = sliderGO.GetComponent<Slider>(); slider.targetGraphic = bgImg; slider.fillRect = fillRT; slider.handleRect = null; slider.direction = Slider.Direction.LeftToRight;

      // Main Menu Button
      _menuBtn = BuildButton(panel.transform, "Main Menu", new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.35f));

      return panel;
    }

    Button BuildButton(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax)
    {
      var btnGO = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
      btnGO.transform.SetParent(parent, false);
      var btnImg = btnGO.GetComponent<Image>(); btnImg.color = new Color(1f, 1f, 1f, 0.15f);
      var brt = btnGO.GetComponent<RectTransform>(); brt.anchorMin = anchorMin; brt.anchorMax = anchorMax; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
      var btn = btnGO.GetComponent<Button>();
      var tGO = new GameObject("Text", typeof(RectTransform), typeof(Text)); tGO.transform.SetParent(btnGO.transform, false);
      var t = tGO.GetComponent<Text>(); t.text = text; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.fontSize = 28; if (font != null) t.font = font;
      var tr = tGO.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
      return btn;
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
    void EnsureEventSystem() { var es = GameObject.FindFirstObjectByType<EventSystem>(); if (es == null) { var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); } }

    // Attempt to auto-load a default UI font from Resources/Fonts similar to GameStateUI
    Font GetDefaultUIFont()
    {
      var candidates = new string[] {
        "Fonts/LiberationSans",
        "Fonts/Liberation Sans",
        "Fonts/liberation-sans",
        "Fonts/LiberationSans-Regular",
        "Fonts/LiberationSans-Bold"
      };
      foreach (var path in candidates)
      {
        var f = Resources.Load<Font>(path);
        if (f != null) return f;
      }
      return null;
    }
  }
}