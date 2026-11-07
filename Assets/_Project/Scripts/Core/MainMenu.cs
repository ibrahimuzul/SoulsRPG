using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SoulsRPG.Save;

namespace SoulsRPG.Core
{
    /// <summary>
    /// Açılış ekranı (MainMenu sahnesi). Sahnede sadece bu bileşen var; kamera, EventSystem ve arayüz koddan kurulur.
    ///  - Kayıt varsa: "Continue" (altında seviye, son ateş ve kayıt tarihi)
    ///  - Kayıt yoksa: "New Game"
    ///  - "Çıkış"
    /// Seçim yapılınca yükleme ekranı açılır ve dünya (Core + oyuncunun bölgesi) yüklenir.
    /// Kontroller: fare, klavye (ok tuşları + Enter) ve kol (sol analog + ✕ / Kare).
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private string gameTitle = "SOULS RPG";

        private Button mainButton;
        private Button quitButton;
        private bool starting;

        private static readonly Color TitleColor = new Color(0.86f, 0.78f, 0.6f);
        private static readonly Color ButtonNormal = new Color(0f, 0f, 0f, 0f);
        private static readonly Color ButtonHighlight = new Color(0.95f, 0.6f, 0.2f, 0.18f);
        private static readonly Color ButtonText = new Color(0.9f, 0.85f, 0.72f);

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            EnsureCamera();
            EnsureEventSystem();
            BuildUI();
        }

        private void Update()
        {
            if (starting) return;

            // ✕ (JoystickButton1) da onaylasın; Kare (Button0) ve Enter zaten EventSystem'in "Submit"i
            if (Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                var button = selected != null ? selected.GetComponent<Button>() : null;
                if (button != null) button.onClick.Invoke();
            }

            // Fare boşa tıklayınca seçim kaybolmasın (kol/klavye ile devam edilebilsin)
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && mainButton != null)
            {
                EventSystem.current.SetSelectedGameObject(mainButton.gameObject);
            }
        }

        // ------------------------------------------------------------------ eylemler

        private void OnMainButton()
        {
            if (starting) return;
            starting = true;
            mainButton.interactable = false;
            quitButton.interactable = false;
            LoadingScreen.LoadWorld();
        }

        private void OnQuit()
        {
            if (starting) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ kurulum

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var camGo = new GameObject("Menu Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0; // menüde sadece arayüz var
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private void BuildUI()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("MenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Arka plan: koyu, altta hafif kor rengi
            var bg = Rect("Background", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bg.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.035f, 1f);
            var glow = Rect("EmberGlow", canvasGo.transform, new Vector2(0f, 0f), new Vector2(1f, 0.35f), Vector2.zero, Vector2.zero);
            var glowImg = glow.gameObject.AddComponent<Image>();
            glowImg.sprite = CreateVerticalGradient(new Color(0.55f, 0.22f, 0.06f, 0.35f), new Color(0f, 0f, 0f, 0f));

            // Başlık
            var title = Label(canvasGo.transform, "Title", gameTitle, font, 96, TitleColor, FontStyle.Normal,
                new Vector2(0.5f, 0.68f), new Vector2(1400f, 140f));
            var titleOutline = title.gameObject.AddComponent<Shadow>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            titleOutline.effectDistance = new Vector2(3f, -3f);

            // İnce ayraç çizgisi
            var line = Rect("Divider", canvasGo.transform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), new Vector2(-260f, -1f), new Vector2(260f, 1f));
            line.gameObject.AddComponent<Image>().color = new Color(0.86f, 0.78f, 0.6f, 0.35f);

            // Ana buton: kayıt varsa Continue, yoksa New Game
            var save = SaveManager.HasSaveFile() ? SaveManager.LoadPlayer() : null;
            bool hasSave = save != null;
            mainButton = CreateButton(canvasGo.transform, hasSave ? "Continue" : "New Game", font, new Vector2(0.5f, 0.42f));
            mainButton.onClick.AddListener(OnMainButton);

            if (hasSave)
            {
                string bonfire = save.hasSavedBonfire && !string.IsNullOrEmpty(save.lastBonfireName) ? save.lastBonfireName : "—";
                string info = $"Seviye {save.soulLevel}   ·   {bonfire}";
                if (!string.IsNullOrEmpty(save.saveTimestamp)) info += $"   ·   {save.saveTimestamp}";
                Label(canvasGo.transform, "SaveInfo", info, font, 22, new Color(0.7f, 0.66f, 0.58f, 0.85f), FontStyle.Italic,
                    new Vector2(0.5f, 0.375f), new Vector2(900f, 34f));
            }

            quitButton = CreateButton(canvasGo.transform, "Çıkış", font, new Vector2(0.5f, 0.3f));
            quitButton.onClick.AddListener(OnQuit);

            // Kol / klavye gezinmesi
            var navMain = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = quitButton, selectOnUp = quitButton };
            var navQuit = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = mainButton, selectOnDown = mainButton };
            mainButton.navigation = navMain;
            quitButton.navigation = navQuit;

            // Alt ipucu
            Label(canvasGo.transform, "Hint", "X / Enter: Seç", font, 18, new Color(0.6f, 0.56f, 0.5f, 0.6f), FontStyle.Normal,
                new Vector2(0.5f, 0.06f), new Vector2(600f, 30f));

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(mainButton.gameObject);
        }

        private static Button CreateButton(Transform parent, string text, Font font, Vector2 anchor)
        {
            var rt = Rect(text + "Button", parent, anchor, anchor, new Vector2(-220f, -32f), new Vector2(220f, 32f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.white;

            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHighlight;
            colors.selectedColor = ButtonHighlight;
            colors.pressedColor = new Color(0.95f, 0.6f, 0.2f, 0.35f);
            colors.disabledColor = ButtonNormal;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.targetGraphic = img;

            var label = Label(rt, "Text", text, font, 38, ButtonText, FontStyle.Normal, new Vector2(0.5f, 0.5f), new Vector2(440f, 64f));
            label.raycastTarget = false;
            return button;
        }

        private static Text Label(Transform parent, string name, string content, Font font, int size, Color color, FontStyle style, Vector2 anchor, Vector2 sizeDelta)
        {
            var rt = Rect(name, parent, anchor, anchor, -sizeDelta * 0.5f, sizeDelta * 0.5f);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        private static Sprite CreateVerticalGradient(Color bottom, Color top)
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
        }
    }
}
