using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SoulsRPG.Save;

namespace SoulsRPG.Core
{
    /// <summary>
    /// Açılış ekranı (MainMenu sahnesi). Sahnede sadece bu bileşen var; kamera, EventSystem ve arayüz koddan kurulur.
    ///
    ///  Ana menü:   Continue (son oynanan karakter) · New Game · Load Game · Çıkış
    ///  New Game:   karakter adı sorulur → yeni kayıt yuvası açılır
    ///  Load Game:  tüm karakterler (ad, seviye, son ateş, tarih) listelenir → seçilen yüklenir
    ///
    /// Kontroller: fare · klavye (ok tuşları, Enter, Esc) · kol (sol analog, ✕ / Kare = seç, ◯ = geri)
    /// Seçimden sonra yükleme ekranı açılır ve dünya yüklenir.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private string gameTitle = "SOULS RPG";

        private Font font;
        private Transform canvasRoot;

        private GameObject mainPanel, newGamePanel, loadPanel;
        private Button continueButton, newGameButton, loadGameButton, quitButton;
        private Button startButton, newBackButton, loadBackButton;
        private InputField nameField;
        private RectTransform loadListContent;
        private ScrollRect loadScroll;
        private readonly List<Button> loadSlotButtons = new List<Button>();

        private GameObject currentPanel;
        private Selectable defaultSelection;
        private bool starting;

        private static readonly Color TitleColor = new Color(0.86f, 0.78f, 0.6f);
        private static readonly Color SubtleText = new Color(0.7f, 0.66f, 0.58f, 0.85f);
        private static readonly Color ButtonNormal = new Color(0f, 0f, 0f, 0f);
        private static readonly Color ButtonHighlight = new Color(0.95f, 0.6f, 0.2f, 0.18f);
        private static readonly Color ButtonTextColor = new Color(0.9f, 0.85f, 0.72f);
        private static readonly Color ButtonTextDisabled = new Color(0.45f, 0.42f, 0.38f);

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            EnsureCamera();
            EnsureEventSystem();
            BuildCanvas();
            BuildMainPanel();
            BuildNewGamePanel();
            BuildLoadPanel();
            ShowPanel(mainPanel);
        }

        private void Update()
        {
            if (starting) return;
            var es = EventSystem.current;

            // ✕ (JoystickButton1) da onaylasın; Kare (Button0) ve Enter zaten EventSystem'in "Submit"i
            if (Input.GetKeyDown(KeyCode.JoystickButton1) && es != null && es.currentSelectedGameObject != null)
            {
                var button = es.currentSelectedGameObject.GetComponent<Button>();
                if (button != null && button.interactable) button.onClick.Invoke();
            }

            // ◯ / Esc: alt ekrandan geri
            bool back = Input.GetKeyDown(KeyCode.JoystickButton2) || Input.GetKeyDown(KeyCode.Escape);
            if (back && currentPanel != mainPanel)
            {
                ShowPanel(mainPanel);
                return;
            }

            // Fare boşa tıklayınca seçim kaybolmasın (kol/klavye ile devam edilebilsin)
            if (es != null && es.currentSelectedGameObject == null && defaultSelection != null)
            {
                es.SetSelectedGameObject(defaultSelection.gameObject);
            }

            // Kol ile listede gezinirken seçili karakter görünür kalsın
            if (currentPanel == loadPanel && es != null && es.currentSelectedGameObject != null)
            {
                KeepVisible(es.currentSelectedGameObject.transform as RectTransform);
            }
        }

        // ================================================================== EYLEMLER

        private void OnContinue()
        {
            var last = SaveManager.LastPlayedSlotId;
            if (last == null) return;
            SaveManager.SetActiveSlot(last);
            StartGame();
        }

        private void OnNewGame()
        {
            nameField.text = SuggestName();
            ShowPanel(newGamePanel);
            nameField.ActivateInputField();
        }

        private void OnStartNewGame()
        {
            string name = nameField.text != null ? nameField.text.Trim() : "";
            if (name.Length == 0)
            {
                nameField.ActivateInputField();
                return;
            }
            SaveManager.CreateNewSlot(name);
            StartGame();
        }

        private void OnLoadGame()
        {
            RebuildSlotList();
            ShowPanel(loadPanel);
        }

        private void OnLoadSlot(string slotId)
        {
            SaveManager.SetActiveSlot(slotId);
            StartGame();
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void StartGame()
        {
            if (starting) return;
            starting = true;
            foreach (var s in canvasRoot.GetComponentsInChildren<Selectable>()) s.interactable = false;
            LoadingScreen.LoadWorld();
        }

        private void ShowPanel(GameObject panel)
        {
            mainPanel.SetActive(panel == mainPanel);
            newGamePanel.SetActive(panel == newGamePanel);
            loadPanel.SetActive(panel == loadPanel);
            currentPanel = panel;

            if (panel == mainPanel) defaultSelection = continueButton.interactable ? (Selectable)continueButton : newGameButton;
            else if (panel == newGamePanel) defaultSelection = nameField;
            else defaultSelection = loadSlotButtons.Count > 0 ? (Selectable)loadSlotButtons[0] : loadBackButton;

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(defaultSelection.gameObject);
        }

        private static string SuggestName()
        {
            int n = SaveManager.ListSlots().Count + 1;
            return "Şövalye " + n;
        }

        // ================================================================== PANELLER

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("MenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasRoot = canvasGo.transform;

            // Arka plan: koyu, altta hafif kor rengi
            var bg = Rect("Background", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bg.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.035f, 1f);
            var glow = Rect("EmberGlow", canvasRoot, Vector2.zero, new Vector2(1f, 0.35f), Vector2.zero, Vector2.zero);
            glow.gameObject.AddComponent<Image>().sprite = CreateVerticalGradient(new Color(0.55f, 0.22f, 0.06f, 0.35f), new Color(0f, 0f, 0f, 0f));

            // Başlık ve ayraç (tüm panellerde görünür)
            var title = Label(canvasRoot, "Title", gameTitle, 96, TitleColor, FontStyle.Normal, new Vector2(0.5f, 0.74f), new Vector2(1400f, 140f));
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(3f, -3f);
            var line = Rect("Divider", canvasRoot, new Vector2(0.5f, 0.665f), new Vector2(0.5f, 0.665f), new Vector2(-260f, -1f), new Vector2(260f, 1f));
            line.gameObject.AddComponent<Image>().color = new Color(0.86f, 0.78f, 0.6f, 0.35f);

            Label(canvasRoot, "Hint", "X / Enter: Seç     O / Esc: Geri", 18, new Color(0.6f, 0.56f, 0.5f, 0.6f), FontStyle.Normal,
                new Vector2(0.5f, 0.05f), new Vector2(800f, 30f));
        }

        private void BuildMainPanel()
        {
            mainPanel = Panel("MainPanel");
            var t = mainPanel.transform;

            bool hasSave = SaveManager.HasAnySave;
            continueButton = CreateButton(t, "Continue", new Vector2(0.5f, 0.56f));
            continueButton.onClick.AddListener(OnContinue);
            SetButtonEnabled(continueButton, hasSave);

            if (hasSave)
            {
                var last = SaveManager.ListSlots().Find(s => s.slotId == SaveManager.LastPlayedSlotId);
                if (last != null)
                {
                    Label(t, "ContinueInfo", SlotSummary(last.data), 22, SubtleText, FontStyle.Italic,
                        new Vector2(0.5f, 0.515f), new Vector2(1000f, 34f));
                }
            }

            newGameButton = CreateButton(t, "New Game", new Vector2(0.5f, 0.44f));
            newGameButton.onClick.AddListener(OnNewGame);

            loadGameButton = CreateButton(t, "Load Game", new Vector2(0.5f, 0.36f));
            loadGameButton.onClick.AddListener(OnLoadGame);
            SetButtonEnabled(loadGameButton, hasSave);

            quitButton = CreateButton(t, "Çıkış", new Vector2(0.5f, 0.28f));
            quitButton.onClick.AddListener(OnQuit);

            // Dikey gezinme (pasif butonlar Unity tarafından atlanır)
            ChainVertical(continueButton, newGameButton, loadGameButton, quitButton);
        }

        private void BuildNewGamePanel()
        {
            newGamePanel = Panel("NewGamePanel");
            var t = newGamePanel.transform;

            Label(t, "Header", "Yeni Karakter", 40, TitleColor, FontStyle.Normal, new Vector2(0.5f, 0.57f), new Vector2(800f, 60f));
            Label(t, "NameLabel", "Karakter adı", 22, SubtleText, FontStyle.Normal, new Vector2(0.5f, 0.505f), new Vector2(600f, 30f));

            // İsim alanı
            var fieldRt = Rect("NameField", t, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), new Vector2(-260f, -30f), new Vector2(260f, 30f));
            var fieldImg = fieldRt.gameObject.AddComponent<Image>();
            fieldImg.color = new Color(0.1f, 0.09f, 0.08f, 0.95f);
            var outline = fieldRt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.86f, 0.78f, 0.6f, 0.4f);

            var textRt = Rect("Text", fieldRt, Vector2.zero, Vector2.one, new Vector2(14f, 4f), new Vector2(-14f, -4f));
            var text = textRt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 30;
            text.color = ButtonTextColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            var phRt = Rect("Placeholder", fieldRt, Vector2.zero, Vector2.one, new Vector2(14f, 4f), new Vector2(-14f, -4f));
            var placeholder = phRt.gameObject.AddComponent<Text>();
            placeholder.font = font;
            placeholder.fontSize = 30;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color(0.5f, 0.47f, 0.42f, 0.7f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.text = "Bir ad yaz…";

            nameField = fieldRt.gameObject.AddComponent<InputField>();
            nameField.textComponent = text;
            nameField.placeholder = placeholder;
            nameField.characterLimit = 20;
            nameField.lineType = InputField.LineType.SingleLine;
            nameField.targetGraphic = fieldImg;
            nameField.onEndEdit.AddListener(_ =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) OnStartNewGame();
            });

            startButton = CreateButton(t, "Başla", new Vector2(0.5f, 0.35f));
            startButton.onClick.AddListener(OnStartNewGame);
            newBackButton = CreateButton(t, "Geri", new Vector2(0.5f, 0.27f));
            newBackButton.onClick.AddListener(() => ShowPanel(mainPanel));

            ChainVertical(nameField, startButton, newBackButton);
        }

        private void BuildLoadPanel()
        {
            loadPanel = Panel("LoadPanel");
            var t = loadPanel.transform;

            Label(t, "Header", "Karakter Seç", 40, TitleColor, FontStyle.Normal, new Vector2(0.5f, 0.615f), new Vector2(800f, 60f));

            // Kaydırılabilir liste
            var scrollRt = Rect("SlotList", t, new Vector2(0.5f, 0.17f), new Vector2(0.5f, 0.58f), new Vector2(-420f, 0f), new Vector2(420f, 0f));
            loadScroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            loadScroll.horizontal = false;
            loadScroll.movementType = ScrollRect.MovementType.Clamped;
            loadScroll.scrollSensitivity = 30f;

            var viewport = Rect("Viewport", scrollRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // fare tekerleği için

            loadListContent = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            loadListContent.pivot = new Vector2(0.5f, 1f);
            var layout = loadListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = loadListContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            loadScroll.viewport = viewport;
            loadScroll.content = loadListContent;

            loadBackButton = CreateButton(t, "Geri", new Vector2(0.5f, 0.11f));
            loadBackButton.onClick.AddListener(() => ShowPanel(mainPanel));
        }

        private void RebuildSlotList()
        {
            foreach (Transform child in loadListContent) Destroy(child.gameObject);
            loadSlotButtons.Clear();

            foreach (var slot in SaveManager.ListSlots())
            {
                string id = slot.slotId;
                var rt = new GameObject("Slot_" + id, typeof(RectTransform)).GetComponent<RectTransform>();
                rt.SetParent(loadListContent, false);
                var le = rt.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 78f;

                var img = rt.gameObject.AddComponent<Image>();
                img.color = Color.white;
                var button = rt.gameObject.AddComponent<Button>();
                StyleButton(button, img, new Color(1f, 1f, 1f, 0.03f));
                button.onClick.AddListener(() => OnLoadSlot(id));

                string name = string.IsNullOrEmpty(slot.data.characterName) ? "İsimsiz" : slot.data.characterName;
                var nameText = Label(rt, "Name", name, 32, ButtonTextColor, FontStyle.Normal, Vector2.zero, Vector2.zero);
                Stretch(nameText.rectTransform, new Vector2(24f, 36f), new Vector2(-24f, -6f));
                nameText.alignment = TextAnchor.UpperLeft;

                var infoText = Label(rt, "Info", SlotSummary(slot.data, includeName: false), 20, SubtleText, FontStyle.Italic, Vector2.zero, Vector2.zero);
                Stretch(infoText.rectTransform, new Vector2(24f, 8f), new Vector2(-24f, -44f));
                infoText.alignment = TextAnchor.LowerLeft;

                loadSlotButtons.Add(button);
            }

            // Gezinme: liste aşağı → Geri, Geri yukarı → son karakter
            var all = new List<Selectable>(loadSlotButtons) { loadBackButton };
            ChainVertical(all.ToArray());

            loadListContent.anchoredPosition = Vector2.zero;
        }

        // ================================================================== YARDIMCILAR

        private static string SlotSummary(PlayerSaveData d, bool includeName = true)
        {
            var parts = new List<string>();
            if (includeName) parts.Add(string.IsNullOrEmpty(d.characterName) ? "İsimsiz" : d.characterName);
            parts.Add($"Seviye {d.soulLevel}");
            parts.Add(d.hasSavedBonfire && !string.IsNullOrEmpty(d.lastBonfireName) ? d.lastBonfireName : "Başlangıç");
            if (!string.IsNullOrEmpty(d.saveTimestamp)) parts.Add(d.saveTimestamp);
            return string.Join("   ·   ", parts);
        }

        private void KeepVisible(RectTransform item)
        {
            if (item == null || item.parent != loadListContent) return;
            var viewport = loadScroll.viewport;
            float viewH = viewport.rect.height;
            float contentH = loadListContent.rect.height;
            if (contentH <= viewH) return;

            // item'ın içerik içindeki üst/alt konumu (içerik pivotu üstte)
            float top = -item.anchoredPosition.y - item.rect.height * (1f - item.pivot.y);
            float bottom = top + item.rect.height;
            float scroll = loadListContent.anchoredPosition.y;

            if (top < scroll) scroll = top;
            else if (bottom > scroll + viewH) scroll = bottom - viewH;
            loadListContent.anchoredPosition = new Vector2(loadListContent.anchoredPosition.x, Mathf.Clamp(scroll, 0f, contentH - viewH));
        }

        private GameObject Panel(string name)
        {
            var rt = Rect(name, canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rt.gameObject;
        }

        private Button CreateButton(Transform parent, string text, Vector2 anchor)
        {
            var rt = Rect(text + "Button", parent, anchor, anchor, new Vector2(-220f, -30f), new Vector2(220f, 30f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.white;
            var button = rt.gameObject.AddComponent<Button>();
            StyleButton(button, img, ButtonNormal);

            var label = Label(rt, "Text", text, 36, ButtonTextColor, FontStyle.Normal, new Vector2(0.5f, 0.5f), new Vector2(440f, 60f));
            label.raycastTarget = false;
            return button;
        }

        private static void StyleButton(Button button, Image img, Color normal)
        {
            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = ButtonHighlight;
            colors.selectedColor = ButtonHighlight;
            colors.pressedColor = new Color(0.95f, 0.6f, 0.2f, 0.35f);
            colors.disabledColor = ButtonNormal;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.targetGraphic = img;
        }

        private static void SetButtonEnabled(Button button, bool enabled)
        {
            button.interactable = enabled;
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.color = enabled ? ButtonTextColor : ButtonTextDisabled;
        }

        private static void ChainVertical(params Selectable[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = FindInteractable(items, i, -1),
                    selectOnDown = FindInteractable(items, i, +1),
                };
                items[i].navigation = nav;
            }
        }

        private static Selectable FindInteractable(Selectable[] items, int from, int step)
        {
            int n = items.Length;
            for (int k = 1; k < n; k++)
            {
                var s = items[((from + step * k) % n + n) % n];
                if (s != null && s.interactable) return s;
            }
            return null;
        }

        private Text Label(Transform parent, string name, string content, int size, Color color, FontStyle style, Vector2 anchor, Vector2 sizeDelta)
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

        private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

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

        private static Sprite CreateVerticalGradient(Color bottom, Color top)
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
        }
    }
}
