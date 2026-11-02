using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SoulsRPG.Player;
using SoulsRPG.Environment;
using SoulsRPG.Audio;

namespace SoulsRPG.UI
{
    /// <summary>
    /// Dark Souls Bonfire ve Seviye Atlama (Level Up) Arayüzü.
    /// Hem Fare (Mouse) hem Klavye (W/S/A/D/Oklar/Enter/Esc) hem de Gamepad/Oyun Kolu (D-Pad/Analog/A/B/X/Y)
    /// ile tam uyumlu, çift girişli hibrit menü sistemi.
    /// </summary>
    public class BonfireMenuHUD : MonoBehaviour
    {
        public static BonfireMenuHUD Instance { get; private set; }

        private GameObject rootGo;
        private CanvasGroup menuCanvasGroup;
        private GameObject mainPanel;
        private GameObject levelUpPanel;

        // Navigation state
        private bool isLevelUpOpen = false;
        private int mainIndex = 0;
        private int levelUpIndex = 0;
        private float navInputCooldown = 0f;

        // Main Menu Button Visuals
        private readonly List<Image> mainButtonImages = new List<Image>();
        private readonly List<Text> mainButtonTexts = new List<Text>();
        private readonly List<Action> mainButtonActions = new List<Action>();

        // Level Up Elements
        private Text levelText;
        private Text soulsText;
        private Text costText;
        private Text feedbackText;

        // Attribute Rows
        private Text vigText, endText, strText, dexText, vitText;
        private readonly List<Image> rowBgs = new List<Image>();
        private readonly List<Button> rowMinusButtons = new List<Button>();
        private readonly List<Button> rowPlusButtons = new List<Button>();

        // Preview Stat Texts
        private Text previewHpText, previewStamText, previewDmgText, previewDefText, previewPoiseText;

        // Level Up Action Buttons
        private Button confirmBtnComponent;
        private Image confirmBtnImg;
        private Text confirmBtnTxt;
        private Image backBtnImg;
        private Text backBtnTxt;

        // Pending points
        private int pendingVig = 0;
        private int pendingEnd = 0;
        private int pendingStr = 0;
        private int pendingDex = 0;
        private int pendingVit = 0;

        private Bonfire activeBonfire;
        private bool isOpen = false;

        public bool IsOpen => isOpen;

        // Visual Palette (Dark Souls Authentic)
        private static readonly Color ColNormalBg = new Color(0.14f, 0.15f, 0.19f, 0.94f);
        private static readonly Color ColSelectedBg = new Color(0.45f, 0.38f, 0.16f, 1.0f); // Altın Sarısı
        private static readonly Color ColNormalText = new Color(0.88f, 0.85f, 0.78f, 1.0f);
        private static readonly Color ColSelectedText = new Color(1.0f, 0.96f, 0.75f, 1.0f);
        private static readonly Color ColRowNormal = new Color(0.12f, 0.13f, 0.16f, 0.70f);
        private static readonly Color ColRowSelected = new Color(0.38f, 0.32f, 0.14f, 0.90f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            EnsureEventSystem();
            EnsureUIHierarchy();
            CloseMenuImmediate();
        }

        public static BonfireMenuHUD GetOrCreate()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<BonfireMenuHUD>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var canvas = FindAnyObjectByType<Canvas>();
            GameObject host = canvas != null ? canvas.gameObject : new GameObject("--- BONFIRE MENU HUD ---");
            Instance = host.AddComponent<BonfireMenuHUD>();
            return Instance;
        }

        public static void EnsureEventSystem()
        {
            var es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("--- EVENT SYSTEM ---");
                es = esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
                Debug.Log("<color=green><b>[BonfireMenu] UI EventSystem (Tıklama ve Giriş Sistemi) oluşturuldu!</b></color>");
            }
            else
            {
                if (es.GetComponent<StandaloneInputModule>() == null && es.GetComponent<BaseInputModule>() == null)
                {
                    es.gameObject.AddComponent<StandaloneInputModule>();
                }
            }
        }

        public void OpenMenu(Bonfire bonfire)
        {
            activeBonfire = bonfire;
            isOpen = true;

            EnsureEventSystem();
            EnsureUIHierarchy();

            if (rootGo != null)
            {
                rootGo.transform.SetAsLastSibling();
            }

            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = 1f;
                menuCanvasGroup.interactable = true;
                menuCanvasGroup.blocksRaycasts = true;
            }

            ShowMainPanel();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Rest ses efekti çal
            if (SoulsAudioManager.Instance != null && activeBonfire != null)
            {
                SoulsAudioManager.Instance.PlayBonfireRest(activeBonfire.transform.position);
            }
        }

        public void CloseMenu()
        {
            isOpen = false;
            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = 0f;
                menuCanvasGroup.interactable = false;
                menuCanvasGroup.blocksRaycasts = false;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (activeBonfire != null)
            {
                activeBonfire.OnPlayerLeftBonfire();
                activeBonfire = null;
            }
        }

        private void CloseMenuImmediate()
        {
            isOpen = false;
            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = 0f;
                menuCanvasGroup.interactable = false;
                menuCanvasGroup.blocksRaycasts = false;
            }
        }

        private void Update()
        {
            if (!isOpen) return;

            // Fare imlecinin serbest kalmasını garantiye al
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            HandleUnifiedInput();
        }

        // ==========================================
        // UNIFIED INPUT (Gamepad + Keyboard + Mouse)
        // ==========================================
        private void HandleUnifiedInput()
        {
            if (navInputCooldown > 0f)
            {
                navInputCooldown -= Time.unscaledDeltaTime;
            }

            // Dikey Yön (Yukarı / Aşağı)
            float v = Input.GetAxisRaw("Vertical");
            bool upPressed = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
            bool downPressed = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);

            // Yatay Yön (Sol / Sağ)
            float h = Input.GetAxisRaw("Horizontal");
            bool leftPressed = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
            bool rightPressed = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);

            // Onay Tuşları: Enter, Boşluk, Kol 'A'/'✕' (JoystickButton0)
            bool submitPressed = Input.GetKeyDown(KeyCode.Return) ||
                                 Input.GetKeyDown(KeyCode.KeypadEnter) ||
                                 Input.GetKeyDown(KeyCode.Space) ||
                                 Input.GetKeyDown(KeyCode.JoystickButton0);

            // İptal / Geri Tuşları: Escape, Backspace, Kol 'B'/'◯' (JoystickButton1)
            bool cancelPressed = Input.GetKeyDown(KeyCode.Escape) ||
                                 Input.GetKeyDown(KeyCode.Backspace) ||
                                 Input.GetKeyDown(KeyCode.JoystickButton1);

            if (!isLevelUpOpen)
            {
                // ============================
                // MAIN PANEL NAVIGATION
                // ============================
                if (navInputCooldown <= 0f)
                {
                    if (upPressed || v > 0.55f)
                    {
                        mainIndex = (mainIndex - 1 + mainButtonImages.Count) % mainButtonImages.Count;
                        RefreshMainMenuHighlights();
                        navInputCooldown = 0.22f;
                    }
                    else if (downPressed || v < -0.55f)
                    {
                        mainIndex = (mainIndex + 1) % mainButtonImages.Count;
                        RefreshMainMenuHighlights();
                        navInputCooldown = 0.22f;
                    }
                }

                if (submitPressed)
                {
                    if (mainIndex >= 0 && mainIndex < mainButtonActions.Count)
                    {
                        mainButtonActions[mainIndex]?.Invoke();
                    }
                }

                if (cancelPressed)
                {
                    CloseMenu();
                }
            }
            else
            {
                // ============================
                // LEVEL UP PANEL NAVIGATION
                // 0: Vigor, 1: Endurance, 2: Strength, 3: Dexterity, 4: Vitality
                // 5: Confirm Button, 6: Back Button
                // ============================
                if (navInputCooldown <= 0f)
                {
                    if (upPressed || v > 0.55f)
                    {
                        levelUpIndex = (levelUpIndex - 1 + 7) % 7;
                        RefreshLevelUpHighlights();
                        navInputCooldown = 0.22f;
                    }
                    else if (downPressed || v < -0.55f)
                    {
                        levelUpIndex = (levelUpIndex + 1) % 7;
                        RefreshLevelUpHighlights();
                        navInputCooldown = 0.22f;
                    }
                    else if (leftPressed || h < -0.55f)
                    {
                        // Stat azalt
                        if (levelUpIndex >= 0 && levelUpIndex <= 4)
                        {
                            ModifyAttributeByIndex(levelUpIndex, -1);
                            navInputCooldown = 0.18f;
                        }
                    }
                    else if (rightPressed || h > 0.55f)
                    {
                        // Stat arttır
                        if (levelUpIndex >= 0 && levelUpIndex <= 4)
                        {
                            ModifyAttributeByIndex(levelUpIndex, 1);
                            navInputCooldown = 0.18f;
                        }
                    }
                }

                if (submitPressed)
                {
                    if (levelUpIndex == 5)
                    {
                        OnConfirmLevelUpClicked();
                    }
                    else if (levelUpIndex == 6)
                    {
                        ShowMainPanel();
                    }
                }

                if (cancelPressed)
                {
                    ShowMainPanel();
                }
            }
        }

        private void ModifyAttributeByIndex(int index, int delta)
        {
            switch (index)
            {
                case 0: ModifyAttribute(ref pendingVig, delta); break;
                case 1: ModifyAttribute(ref pendingEnd, delta); break;
                case 2: ModifyAttribute(ref pendingStr, delta); break;
                case 3: ModifyAttribute(ref pendingDex, delta); break;
                case 4: ModifyAttribute(ref pendingVit, delta); break;
            }
        }

        private void ShowMainPanel()
        {
            isLevelUpOpen = false;
            if (mainPanel != null) mainPanel.SetActive(true);
            if (levelUpPanel != null) levelUpPanel.SetActive(false);
            RefreshMainMenuHighlights();
        }

        private void ShowLevelUpPanel()
        {
            isLevelUpOpen = true;
            if (mainPanel != null) mainPanel.SetActive(false);
            if (levelUpPanel != null) levelUpPanel.SetActive(true);

            levelUpIndex = 0;
            ResetPendingPoints();
            RefreshLevelUpUI();
            RefreshLevelUpHighlights();
        }

        private void RefreshMainMenuHighlights()
        {
            for (int i = 0; i < mainButtonImages.Count; i++)
            {
                bool sel = (i == mainIndex);
                if (mainButtonImages[i] != null)
                {
                    mainButtonImages[i].color = sel ? ColSelectedBg : ColNormalBg;
                }
                if (mainButtonTexts[i] != null)
                {
                    mainButtonTexts[i].color = sel ? ColSelectedText : ColNormalText;
                }
            }
        }

        private void RefreshLevelUpHighlights()
        {
            // Stat Rows (0-4)
            for (int i = 0; i < rowBgs.Count; i++)
            {
                bool sel = (i == levelUpIndex);
                if (rowBgs[i] != null)
                {
                    rowBgs[i].color = sel ? ColRowSelected : ColRowNormal;
                }
            }

            // Confirm Button (5)
            if (confirmBtnImg != null)
            {
                bool sel = (levelUpIndex == 5);
                confirmBtnImg.color = sel ? ColSelectedBg : ColNormalBg;
                if (confirmBtnTxt != null) confirmBtnTxt.color = sel ? ColSelectedText : ColNormalText;
            }

            // Back Button (6)
            if (backBtnImg != null)
            {
                bool sel = (levelUpIndex == 6);
                backBtnImg.color = sel ? ColSelectedBg : ColNormalBg;
                if (backBtnTxt != null) backBtnTxt.color = sel ? ColSelectedText : ColNormalText;
            }
        }

        private void ResetPendingPoints()
        {
            pendingVig = 0;
            pendingEnd = 0;
            pendingStr = 0;
            pendingDex = 0;
            pendingVit = 0;
            if (feedbackText != null) feedbackText.text = "";
        }

        private void ModifyAttribute(ref int pendingStat, int delta)
        {
            var stats = PlayerStats.Instance;
            if (stats == null) return;

            if (delta < 0 && pendingStat > 0)
            {
                pendingStat += delta;
            }
            else if (delta > 0)
            {
                int totalPending = pendingVig + pendingEnd + pendingStr + pendingDex + pendingVit;
                int nextCost = CalculateTotalCost(stats.SoulLevel, totalPending + 1);

                if (stats.CurrentSouls >= nextCost)
                {
                    pendingStat += delta;
                }
                else
                {
                    if (feedbackText != null)
                    {
                        feedbackText.text = "<color=red>Yetersiz Ruh!</color>";
                    }
                }
            }

            RefreshLevelUpUI();
        }

        private int CalculateTotalCost(int currentLevel, int addedPoints)
        {
            int total = 0;
            for (int i = 0; i < addedPoints; i++)
            {
                total += PlayerStats.GetRequiredSoulsForLevel(currentLevel + 1 + i);
            }
            return total;
        }

        private void RefreshLevelUpUI()
        {
            var stats = PlayerStats.Instance;
            if (stats == null) return;

            int totalPending = pendingVig + pendingEnd + pendingStr + pendingDex + pendingVit;
            int previewLevel = stats.SoulLevel + totalPending;
            int totalCost = CalculateTotalCost(stats.SoulLevel, totalPending);

            // Level & Souls
            if (levelText != null)
            {
                levelText.text = totalPending > 0
                    ? $"Ruh Seviyesi: <color=yellow>{stats.SoulLevel}</color>  ➔  <color=green><b>{previewLevel}</b></color>"
                    : $"Ruh Seviyesi: <color=yellow><b>{stats.SoulLevel}</b></color>";
            }

            if (soulsText != null)
            {
                int remainingSouls = stats.CurrentSouls - totalCost;
                soulsText.text = $"Mevcut Ruhlar: <color=white>{stats.CurrentSouls:N0}</color>  ➔  <color={(remainingSouls >= 0 ? "cyan" : "red")}>{remainingSouls:N0}</color>";
            }

            if (costText != null)
            {
                int nextSingleCost = PlayerStats.GetRequiredSoulsForLevel(stats.SoulLevel + 1);
                costText.text = totalPending > 0
                    ? $"Gereken Ruh: <color={(stats.CurrentSouls >= totalCost ? "green" : "red")}><b>{totalCost:N0}</b></color>"
                    : $"Sonraki Seviye İçin: <color=yellow>{nextSingleCost:N0}</color> Ruh";
            }

            // Attributes Row
            UpdateRow(vigText, "Canlılık (Vigor)", stats.Vigor, pendingVig);
            UpdateRow(endText, "Dayanıklılık (Endurance)", stats.Endurance, pendingEnd);
            UpdateRow(strText, "Kuvvet (Strength)", stats.Strength, pendingStr);
            UpdateRow(dexText, "Beceri (Dexterity)", stats.Dexterity, pendingDex);
            UpdateRow(vitText, "Direnç (Vitality)", stats.Vitality, pendingVit);

            // Preview Derived Stats
            int prevVig = stats.Vigor + pendingVig;
            int prevEnd = stats.Endurance + pendingEnd;
            int prevStr = stats.Strength + pendingStr;
            int prevVit = stats.Vitality + pendingVit;

            int previewMaxHp = 100 + (prevVig - 10) * 15 + (previewLevel - 1) * 2;
            float previewMaxStam = 120f + (prevEnd - 10) * 5f;
            int previewDmg = 25 + (prevStr - 10) * 2;
            int previewDef = (prevVit - 10) + previewLevel;
            float previewPoise = 50f + (prevVit - 10) * 4f;

            if (previewHpText != null)
                previewHpText.text = $"Can (HP): {stats.MaxHealth}  ➔  <color=green><b>{previewMaxHp}</b></color>";
            if (previewStamText != null)
                previewStamText.text = $"Dayanıklılık: {stats.MaxStamina}  ➔  <color=green><b>{previewMaxStam}</b></color>";
            if (previewDmgText != null)
                previewDmgText.text = $"Saldırı Gücü: {25 + stats.BonusAttackDamage}  ➔  <color=green><b>{previewDmg}</b></color>";
            if (previewDefText != null)
                previewDefText.text = $"Savunma (Oto): {stats.PhysicalDefense}  ➔  <color=green><b>{previewDef}</b></color>";
            if (previewPoiseText != null)
                previewPoiseText.text = $"Denge (Poise): {50 + (stats.Vitality - 10) * 4}  ➔  <color=green><b>{previewPoise}</b></color>";

            // Confirm button interactable
            if (confirmBtnComponent != null)
            {
                confirmBtnComponent.interactable = totalPending > 0 && stats.CurrentSouls >= totalCost;
            }
        }

        private void UpdateRow(Text rowText, string statName, int baseVal, int pendingVal)
        {
            if (rowText == null) return;
            if (pendingVal > 0)
            {
                rowText.text = $"{statName}: {baseVal}  ➔  <color=green><b>{baseVal + pendingVal}</b></color>";
            }
            else
            {
                rowText.text = $"{statName}: <color=white><b>{baseVal}</b></color>";
            }
        }

        private void OnConfirmLevelUpClicked()
        {
            var stats = PlayerStats.Instance;
            if (stats == null) return;

            bool success = stats.ApplyLevelUp(pendingVig, pendingEnd, pendingStr, pendingDex, pendingVit);
            if (success)
            {
                if (feedbackText != null)
                {
                    feedbackText.text = "<color=green><b>Seviye Başarıyla Atlandı!</b></color>";
                }

                // Level up zili çal
                if (SoulsAudioManager.Instance != null && activeBonfire != null)
                {
                    SoulsAudioManager.Instance.PlayBonfireLevelUp(activeBonfire.transform.position);
                }

                ResetPendingPoints();
                RefreshLevelUpUI();
            }
            else
            {
                if (feedbackText != null)
                {
                    feedbackText.text = "<color=red>Hata: Seviye atlanamadı!</color>";
                }
            }
        }

        // ==========================================
        // PROCEDURAL DARK SOULS UI BUILDER
        // ==========================================

        private void EnsureUIHierarchy()
        {
            if (menuCanvasGroup != null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                var canvasGo = new GameObject("--- SOULS HUD CANVAS ---");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
            }
            else
            {
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                }
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Root Container
            rootGo = new GameObject("BonfireMenu_Root");
            rootGo.transform.SetParent(canvas.transform, false);
            var rootRect = rootGo.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            menuCanvasGroup = rootGo.AddComponent<CanvasGroup>();

            // Fullscreen Dark Ambient Dim (raycastTarget = false)
            var dimGo = new GameObject("BackgroundDim");
            dimGo.transform.SetParent(rootGo.transform, false);
            var dimRect = dimGo.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0.04f, 0.04f, 0.05f, 0.76f);
            dimImg.raycastTarget = false;

            // ====================================================
            // 1. MAIN BONFIRE PANEL (Left Side Menu)
            // ====================================================
            mainPanel = new GameObject("MainBonfirePanel");
            mainPanel.transform.SetParent(rootGo.transform, false);
            var mRect = mainPanel.AddComponent<RectTransform>();
            mRect.anchorMin = new Vector2(0.22f, 0.5f);
            mRect.anchorMax = new Vector2(0.22f, 0.5f);
            mRect.pivot = new Vector2(0.5f, 0.5f);
            mRect.anchoredPosition = Vector2.zero;
            mRect.sizeDelta = new Vector2(380f, 460f);

            var mBg = mainPanel.AddComponent<Image>();
            mBg.color = new Color(0.08f, 0.08f, 0.10f, 0.95f);
            mBg.raycastTarget = false; // Panelin kendisi tıklamayı engellemesin

            // Title & Subtitle
            CreateText(mainPanel.transform, "Title", "ATEŞ BAŞI (BONFIRE)", font, 22, FontStyle.Bold, new Color(0.95f, 0.85f, 0.55f), new Vector2(0f, 175f), new Vector2(340f, 40f));
            CreateText(mainPanel.transform, "SubTitle", "Kale Avlusu Ateşi", font, 13, FontStyle.Italic, new Color(0.7f, 0.65f, 0.55f), new Vector2(0f, 142f), new Vector2(340f, 30f));

            // Main Buttons
            mainButtonImages.Clear();
            mainButtonTexts.Clear();
            mainButtonActions.Clear();

            AddMainButton("LevelUpBtn", "⚔️  Seviye Atla (Level Up)", font, new Vector2(0f, 50f), ShowLevelUpPanel, 0);
            AddMainButton("RestSaveBtn", "💾  Dinlen ve Kaydet (Save)", font, new Vector2(0f, -15f), () =>
            {
                if (PlayerStats.Instance != null && activeBonfire != null)
                {
                    PlayerStats.Instance.RestAtBonfire(activeBonfire.transform.position);
                    Debug.Log("<color=green>[Bonfire] Oyun kaydedildi, can ve stamina dolduruldu!</color>");
                    if (feedbackText != null) feedbackText.text = "<color=green>Oyun Kaydedildi!</color>";
                }
            }, 1);
            AddMainButton("LeaveBtn", "🚪  Ateşten Ayrıl (Leave)", font, new Vector2(0f, -80f), CloseMenu, 2);

            // ====================================================
            // 2. LEVEL UP PANEL (Center-Widescreen Dark Souls Menu)
            // ====================================================
            levelUpPanel = new GameObject("LevelUpPanel");
            levelUpPanel.transform.SetParent(rootGo.transform, false);
            var lRect = levelUpPanel.AddComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0.5f, 0.5f);
            lRect.anchorMax = new Vector2(0.5f, 0.5f);
            lRect.pivot = new Vector2(0.5f, 0.5f);
            lRect.anchoredPosition = Vector2.zero;
            lRect.sizeDelta = new Vector2(760f, 540f);

            var lBg = levelUpPanel.AddComponent<Image>();
            lBg.color = new Color(0.08f, 0.08f, 0.10f, 0.97f);
            lBg.raycastTarget = false;

            // Level Up Header
            CreateText(levelUpPanel.transform, "LTitle", "SEVİYE ATLA (LEVEL UP)", font, 24, FontStyle.Bold, new Color(0.95f, 0.85f, 0.55f), new Vector2(0f, 230f), new Vector2(650f, 40f));

            // Level & Souls Summary Bar
            levelText = CreateText(levelUpPanel.transform, "LevelText", "Ruh Seviyesi: 1", font, 16, FontStyle.Bold, Color.white, new Vector2(-170f, 180f), new Vector2(320f, 30f), TextAnchor.MiddleLeft);
            soulsText = CreateText(levelUpPanel.transform, "SoulsText", "Mevcut Ruhlar: 500", font, 15, FontStyle.Normal, Color.white, new Vector2(170f, 180f), new Vector2(320f, 30f), TextAnchor.MiddleRight);
            costText = CreateText(levelUpPanel.transform, "CostText", "Gereken Ruh: 500", font, 16, FontStyle.Bold, new Color(1f, 0.85f, 0.4f), new Vector2(0f, 140f), new Vector2(660f, 30f), TextAnchor.MiddleCenter);

            // 5 Attribute Rows (Left Column)
            float startY = 85f;
            float rowSpacing = 42f;
            rowBgs.Clear();
            rowMinusButtons.Clear();
            rowPlusButtons.Clear();

            CreateAttributeRow(levelUpPanel.transform, "VigRow", startY - (rowSpacing * 0), font, out vigText, () => ModifyAttribute(ref pendingVig, -1), () => ModifyAttribute(ref pendingVig, 1), 0);
            CreateAttributeRow(levelUpPanel.transform, "EndRow", startY - (rowSpacing * 1), font, out endText, () => ModifyAttribute(ref pendingEnd, -1), () => ModifyAttribute(ref pendingEnd, 1), 1);
            CreateAttributeRow(levelUpPanel.transform, "StrRow", startY - (rowSpacing * 2), font, out strText, () => ModifyAttribute(ref pendingStr, -1), () => ModifyAttribute(ref pendingStr, 2), 2);
            CreateAttributeRow(levelUpPanel.transform, "DexRow", startY - (rowSpacing * 3), font, out dexText, () => ModifyAttribute(ref pendingDex, -1), () => ModifyAttribute(ref pendingDex, 1), 3);
            CreateAttributeRow(levelUpPanel.transform, "VitRow", startY - (rowSpacing * 4), font, out vitText, () => ModifyAttribute(ref pendingVit, -1), () => ModifyAttribute(ref pendingVit, 1), 4);

            // Derived Stats Preview (Right Column)
            previewHpText = CreateText(levelUpPanel.transform, "PrevHp", "Can (HP): 100", font, 14, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f), new Vector2(190f, startY - (rowSpacing * 0)), new Vector2(300f, 30f), TextAnchor.MiddleLeft);
            previewStamText = CreateText(levelUpPanel.transform, "PrevStam", "Dayanıklılık: 120", font, 14, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f), new Vector2(190f, startY - (rowSpacing * 1)), new Vector2(300f, 30f), TextAnchor.MiddleLeft);
            previewDmgText = CreateText(levelUpPanel.transform, "PrevDmg", "Saldırı Gücü: 25", font, 14, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f), new Vector2(190f, startY - (rowSpacing * 2)), new Vector2(300f, 30f), TextAnchor.MiddleLeft);
            previewDefText = CreateText(levelUpPanel.transform, "PrevDef", "Savunma: 11", font, 14, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f), new Vector2(190f, startY - (rowSpacing * 3)), new Vector2(300f, 30f), TextAnchor.MiddleLeft);
            previewPoiseText = CreateText(levelUpPanel.transform, "PrevPoise", "Denge (Poise): 50", font, 14, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f), new Vector2(190f, startY - (rowSpacing * 4)), new Vector2(300f, 30f), TextAnchor.MiddleLeft);

            // Feedback Message
            feedbackText = CreateText(levelUpPanel.transform, "Feedback", "", font, 14, FontStyle.Bold, Color.yellow, new Vector2(0f, -155f), new Vector2(640f, 26f), TextAnchor.MiddleCenter);

            // Action Buttons
            confirmBtnComponent = CreateActionButton(levelUpPanel.transform, "ConfirmBtn", "✅  Seviyeyi Onayla (Confirm)", font, new Vector2(-140f, -210f), out confirmBtnImg, out confirmBtnTxt, OnConfirmLevelUpClicked, 5);
            CreateActionButton(levelUpPanel.transform, "BackBtn", "↩️  Geri (Back)", font, new Vector2(140f, -210f), out backBtnImg, out backBtnTxt, ShowMainPanel, 6);
        }

        private Text CreateText(Transform parent, string name, string content, Font font, int size, FontStyle style, Color color, Vector2 pos, Vector2 sizeDelta, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;

            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.text = content;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private void AddMainButton(string name, string label, Font font, Vector2 pos, Action onClick, int buttonIndex)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(mainPanel.transform, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(300f, 46f);

            var img = btnGo.AddComponent<Image>();
            img.color = ColNormalBg;
            img.raycastTarget = true; // Tıklama açık

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = ColNormalBg;
            colors.highlightedColor = ColSelectedBg;
            colors.pressedColor = new Color(0.60f, 0.50f, 0.20f, 1.0f);
            colors.selectedColor = ColSelectedBg;
            btn.colors = colors;

            btn.onClick.AddListener(() => onClick());

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var tRect = txtGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var t = txtGo.AddComponent<Text>();
            t.font = font;
            t.fontSize = 15;
            t.fontStyle = FontStyle.Bold;
            t.color = ColNormalText;
            t.text = label;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;

            // Hover Detector (Mouse gezinince Gamepad seçimiyle eşitlensin)
            var hover = btnGo.AddComponent<BonfireHoverDetector>();
            hover.onHover = () =>
            {
                mainIndex = buttonIndex;
                RefreshMainMenuHighlights();
            };

            mainButtonImages.Add(img);
            mainButtonTexts.Add(t);
            mainButtonActions.Add(onClick);
        }

        private Button CreateActionButton(Transform parent, string name, string label, Font font, Vector2 pos, out Image outImg, out Text outTxt, UnityEngine.Events.UnityAction onClick, int actionIndex)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(250f, 44f);

            var img = btnGo.AddComponent<Image>();
            img.color = ColNormalBg;
            img.raycastTarget = true;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = ColNormalBg;
            colors.highlightedColor = ColSelectedBg;
            colors.pressedColor = new Color(0.55f, 0.48f, 0.22f, 1.0f);
            colors.disabledColor = new Color(0.12f, 0.12f, 0.14f, 0.5f);
            colors.selectedColor = ColSelectedBg;
            btn.colors = colors;

            btn.onClick.AddListener(onClick);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var tRect = txtGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var t = txtGo.AddComponent<Text>();
            t.font = font;
            t.fontSize = 14;
            t.fontStyle = FontStyle.Bold;
            t.color = ColNormalText;
            t.text = label;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;

            var hover = btnGo.AddComponent<BonfireHoverDetector>();
            hover.onHover = () =>
            {
                levelUpIndex = actionIndex;
                RefreshLevelUpHighlights();
            };

            outImg = img;
            outTxt = t;
            return btn;
        }

        private void CreateAttributeRow(Transform parent, string name, float yPos, Font font, out Text rowText, UnityEngine.Events.UnityAction onMinus, UnityEngine.Events.UnityAction onPlus, int rowIndex)
        {
            var rowGo = new GameObject(name);
            rowGo.transform.SetParent(parent, false);
            var rect = rowGo.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(-155f, yPos);
            rect.sizeDelta = new Vector2(340f, 38f);

            var rowBg = rowGo.AddComponent<Image>();
            rowBg.color = ColRowNormal;
            rowBg.raycastTarget = true;
            rowBgs.Add(rowBg);

            var hover = rowGo.AddComponent<BonfireHoverDetector>();
            hover.onHover = () =>
            {
                levelUpIndex = rowIndex;
                RefreshLevelUpHighlights();
            };

            // Stat Label
            var tGo = new GameObject("Label");
            tGo.transform.SetParent(rowGo.transform, false);
            var tRect = tGo.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.04f, 0f);
            tRect.anchorMax = new Vector2(0.68f, 1f);
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            rowText = tGo.AddComponent<Text>();
            rowText.font = font;
            rowText.fontSize = 14;
            rowText.fontStyle = FontStyle.Bold;
            rowText.alignment = TextAnchor.MiddleLeft;
            rowText.color = Color.white;
            rowText.raycastTarget = false;

            // Minus Button
            var minusBtn = CreateMiniButton(rowGo.transform, "MinusBtn", "－", font, new Vector2(235f, 0f), onMinus, rowIndex);
            rowMinusButtons.Add(minusBtn);

            // Plus Button
            var plusBtn = CreateMiniButton(rowGo.transform, "PlusBtn", "＋", font, new Vector2(285f, 0f), onPlus, rowIndex);
            rowPlusButtons.Add(plusBtn);
        }

        private Button CreateMiniButton(Transform parent, string name, string symbol, Font font, Vector2 pos, UnityEngine.Events.UnityAction onClick, int rowIndex)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(36f, 32f);

            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.22f, 0.23f, 0.28f, 1f);
            img.raycastTarget = true;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = new Color(0.22f, 0.23f, 0.28f, 1f);
            colors.highlightedColor = ColSelectedBg;
            colors.pressedColor = new Color(0.65f, 0.55f, 0.25f, 1f);
            colors.selectedColor = ColSelectedBg;
            btn.colors = colors;

            btn.onClick.AddListener(onClick);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var tRect = txtGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var t = txtGo.AddComponent<Text>();
            t.font = font;
            t.fontSize = 16;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.text = symbol;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;

            var hover = btnGo.AddComponent<BonfireHoverDetector>();
            hover.onHover = () =>
            {
                levelUpIndex = rowIndex;
                RefreshLevelUpHighlights();
            };

            return btn;
        }
    }

    /// <summary>
    /// Fare ile buton veya satır üzerine gelindiğinde EventSystem ile menü seçim indeksini eşitleyen dedektör.
    /// </summary>
    public class BonfireHoverDetector : MonoBehaviour, IPointerEnterHandler
    {
        public Action onHover;

        public void OnPointerEnter(PointerEventData eventData)
        {
            onHover?.Invoke();
        }
    }
}
