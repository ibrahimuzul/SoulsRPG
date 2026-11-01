using UnityEngine;
using SoulsRPG.CameraSystem;

namespace SoulsRPG.Player
{
    [RequireComponent(typeof(PlayerLocomotion))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("🎯 Kamera Eksenleri (Kesin Olarak Sabitlendi)")]
        [Tooltip("Sağ Analog Yatay: Sadece Eksen 4 okunur")]
        public readonly int cameraHorizontalAxis = 4;

        [Tooltip("Sağ Analog Dikey: Sadece Eksen 7 okunur")]
        public readonly int cameraVerticalAxis = 7;

        [Header("🎯 Sol Analog Hareket")]
        [Tooltip("Sol analog ileri-geri ekseni (Eksen 3)")]
        public int moveVerticalAxis = 3;
        public bool invertMoveY = true;

        [Header("Hassasiyet & Yön")]
        public float cameraSensitivityX = 1.2f;
        public float cameraSensitivityY = 1.0f;
        public bool invertCameraY = false;
        public float stickDeadzone = 0.12f;
        public float rollSprintThreshold = 0.20f;

        [Header("Ekran Göstergesi")]
        public bool showHUD = true;

        private PlayerLocomotion locomotion;
        private PlayerStats stats;
        private CameraManager cameraManager;

        // Girdiler
        private float moveH;
        private float moveV;
        private float camInputX;
        private float camInputY;

        // Koşma & Yuvarlanma Takibi
        private float bHoldTimer = 0f;
        private bool isBHeld = false;
        private bool isSprinting = false;

        // Canlı Eksen Değerleri (1 .. 10)
        private readonly float[] rawAxes = new float[11];

        // Durumlar
        private string statusBlock = "PASİF";
        private string statusAttack = "YOK";
        private string statusRoll = "BOŞTA";
        private string statusLock = "SERBEST";

        private void Awake()
        {
            locomotion = GetComponent<PlayerLocomotion>();
            stats = GetComponent<PlayerStats>();
            moveVerticalAxis = 3; // İleri-geri hareketi doğrudan Eksen 3'e sabitle
        }

        private void Start()
        {
            cameraManager = CameraManager.Instance ?? FindFirstObjectByType<CameraManager>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // F1 ile bilgi panelini gizle / aç
            if (Input.GetKeyDown(KeyCode.F1))
            {
                showHUD = !showHUD;
            }

            // F5 ile Kamera yukarı/aşağı ters çevir (Invert Y)
            if (Input.GetKeyDown(KeyCode.F5))
            {
                invertCameraY = !invertCameraY;
            }

            // F6 ile Hareket Y ters çevir
            if (Input.GetKeyDown(KeyCode.F6))
            {
                invertMoveY = !invertMoveY;
            }

            // ESC ile fare imlecini serbest bırak / kilitle
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            // Tüm Joystick Eksenlerini Oku
            ReadAllAxes();

            if (stats != null && stats.isDead) return;

            if (cameraManager == null)
            {
                cameraManager = CameraManager.Instance ?? FindFirstObjectByType<CameraManager>();
            }

            HandleMovement();
            HandleCamera();
            HandleCombat();
        }

        private void ReadAllAxes()
        {
            for (int i = 1; i <= 10; i++)
            {
                try
                {
                    rawAxes[i] = Input.GetAxisRaw("JoyAxis" + i);
                }
                catch
                {
                    rawAxes[i] = 0f;
                }
            }
        }

        private void HandleMovement()
        {
            // Klavye WASD
            float kbH = 0f;
            if (Input.GetKey(KeyCode.D)) kbH += 1f;
            if (Input.GetKey(KeyCode.A)) kbH -= 1f;

            float kbV = 0f;
            if (Input.GetKey(KeyCode.W)) kbV += 1f;
            if (Input.GetKey(KeyCode.S)) kbV -= 1f;

            // Kol Sol Analog:
            // Yatay: Eksen 1 veya Standart Horizontal
            float padH = rawAxes[1];
            if (Mathf.Abs(padH) < 0.05f) padH = Input.GetAxisRaw("Horizontal");

            // Dikey: Seçili moveVerticalAxis veya Standart Vertical
            float padV = rawAxes[Mathf.Clamp(moveVerticalAxis, 1, 10)];
            if (invertMoveY) padV = -padV;
            if (Mathf.Abs(padV) < 0.05f) padV = Input.GetAxisRaw("Vertical");

            // Ölü bölge
            float finalH = Mathf.Abs(padH) > stickDeadzone ? padH : 0f;
            float finalV = Mathf.Abs(padV) > stickDeadzone ? padV : 0f;

            moveH = Mathf.Abs(finalH) > 0.05f ? finalH : kbH;
            moveV = Mathf.Abs(finalV) > 0.05f ? finalV : kbV;

            bool kbSprint = Input.GetKey(KeyCode.LeftShift);

            // DualSense ◯ (Button 2) veya ✕ (Button 1)
            bool bDown = Input.GetKeyDown(KeyCode.JoystickButton2) || Input.GetKeyDown(KeyCode.JoystickButton1);
            bool bHeld = Input.GetKey(KeyCode.JoystickButton2) || Input.GetKey(KeyCode.JoystickButton1);
            bool bUp   = Input.GetKeyUp(KeyCode.JoystickButton2) || Input.GetKeyUp(KeyCode.JoystickButton1);

            if (bDown)
            {
                isBHeld = true;
                bHoldTimer = 0f;
                isSprinting = false;
                statusRoll = "BASILDI";
            }

            if (isBHeld && bHeld)
            {
                bHoldTimer += Time.deltaTime;
                if (bHoldTimer >= rollSprintThreshold)
                {
                    isSprinting = true;
                    statusRoll = "🏃 DEPAR";
                }
            }

            if (bUp)
            {
                if (bHoldTimer < rollSprintThreshold)
                {
                    locomotion.AttemptDodgeRoll(moveH, moveV);
                    statusRoll = "🔄 YUVARLANMA";
                }
                else
                {
                    statusRoll = "BOŞTA";
                }
                isBHeld = false;
                isSprinting = false;
                bHoldTimer = 0f;
            }

            locomotion.HandleMovement(moveH, moveV, kbSprint || isSprinting);
        }

        private void HandleCamera()
        {
            float mX = (Cursor.lockState == CursorLockMode.Locked) ? Input.GetAxis("Mouse X") : 0f;
            float mY = (Cursor.lockState == CursorLockMode.Locked) ? Input.GetAxis("Mouse Y") : 0f;

            // KESİN VE DEĞİŞMEZ KURAL:
            // Sağ Analog Yatay SADECE Eksen 4'tür!
            // Sağ Analog Dikey SADECE Eksen 7'dir!
            // Eksen 5 ve Eksen 6 (L2/R2 tetikleri) KAMERAYA ASLA VE KAT'A DOKUNAMAZ!
            float rawX = rawAxes[4];
            float rawY = rawAxes[7];

            float rsX = Mathf.Abs(rawX) > stickDeadzone ? rawX : 0f;
            float rsY = Mathf.Abs(rawY) > stickDeadzone ? rawY : 0f;

            if (!invertCameraY)
            {
                rsY = -rsY;
            }

            camInputX = mX + (rsX * cameraSensitivityX);
            camInputY = mY + (rsY * cameraSensitivityY);

            if (cameraManager != null)
            {
                cameraManager.HandleCameraInput(camInputX, camInputY);
            }
        }

        private void HandleCombat()
        {
            // SADECE DİJİTAL BUTONLAR:
            // Kalkan Blok: L1 (Button 4) veya L2 (Button 6) veya Sağ Tık
            bool isBlocking = Input.GetKey(KeyCode.JoystickButton4) ||
                              Input.GetKey(KeyCode.JoystickButton6) ||
                              ((Cursor.lockState == CursorLockMode.Locked) && Input.GetMouseButton(1));

            locomotion.SetBlocking(isBlocking);
            statusBlock = isBlocking ? "<color=lime>🛡️ BLOK AKTİF</color>" : "<color=grey>PASİF</color>";

            // Saldırı: R1 (Button 5) / R2 (Button 7) / Sol Tık
            bool r1 = Input.GetKeyDown(KeyCode.JoystickButton5);
            bool r2 = Input.GetKeyDown(KeyCode.JoystickButton7);
            bool mouseLeft = (Cursor.lockState == CursorLockMode.Locked) && Input.GetMouseButtonDown(0);

            if (r1 || mouseLeft)
            {
                locomotion.AttemptAttack();
                statusAttack = "<color=yellow>⚔️ HAFİF (R1)</color>";
            }
            else if (r2)
            {
                locomotion.AttemptAttack();
                statusAttack = "<color=orange>💥 AĞIR (R2)</color>";
            }

            // Hedef Kilidi: R3 (Button 11 / 9) / Q / Orta Tık
            bool r3 = Input.GetKeyDown(KeyCode.JoystickButton11) ||
                      Input.GetKeyDown(KeyCode.JoystickButton9) ||
                      Input.GetKeyDown(KeyCode.Q) ||
                      Input.GetMouseButtonDown(2);

            if (r3 && cameraManager != null)
            {
                cameraManager.ToggleLockOn();
                statusLock = cameraManager.IsLockedOn ? "<color=red>🎯 KİLİTLİ</color>" : "<color=grey>SERBEST</color>";
            }

            // Space: Klavye Yuvarlanma
            if (Input.GetKeyDown(KeyCode.Space))
            {
                locomotion.AttemptDodgeRoll(moveH, moveV);
                statusRoll = "🔄 YUVARLANMA (Space)";
            }
        }

        private void OnGUI()
        {
            if (!showHUD) return;

            int w = 480;
            int h = 230;
            int x = Screen.width - w - 10;
            int y = 10;

            GUI.Box(new Rect(x, y, w, h), "🎮 DualSense Canlı Monitör (E1 - E10)");

            GUILayout.BeginArea(new Rect(x + 10, y + 25, w - 20, h - 30));

            GUILayout.Label($"<b>Sağ Kamera :</b> Yatay: <color=lime>Eksen 4</color> | Dikey: <color=lime>Eksen 7</color> <color=grey>[KİLİTLİ]</color>");
            GUILayout.Label($"<b>Tetikler   :</b> L2/R2: <color=red>Eksen 5 & 6</color> <color=red>[BLOKLANDI - KAMERAYI DÖNDÜREMEZ]</color>");
            GUILayout.Label($"<b>Sol Hareket:</b> Yatay: E1 ({moveH:F2}) | Dikey: E{moveVerticalAxis} ({moveV:F2})");

            GUILayout.Space(6);
            GUILayout.Label("<b>Canlı Eksenler:</b>");

            // 1. Satır: E1 - E5
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 5; i++)
            {
                string col = (i == 4 || i == 7) ? "lime" : ((i == 5 || i == 6) ? "red" : (Mathf.Abs(rawAxes[i]) > 0.25f ? "yellow" : "white"));
                GUILayout.Label($"<color={col}><b>E{i}:</b>{rawAxes[i]:+0.00;-0.00; 0.00}</color> ");
            }
            GUILayout.EndHorizontal();

            // 2. Satır: E6 - E10
            GUILayout.BeginHorizontal();
            for (int i = 6; i <= 10; i++)
            {
                string col = (i == 4 || i == 7) ? "lime" : ((i == 5 || i == 6) ? "red" : (Mathf.Abs(rawAxes[i]) > 0.25f ? "yellow" : "white"));
                GUILayout.Label($"<color={col}><b>E{i}:</b>{rawAxes[i]:+0.00;-0.00; 0.00}</color> ");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("<size=11><color=yellow>Kamera artık asla kendi kendine dönmez. Sol analogu ileri-geri itin, hangi eksen değişiyor bakın.</color></size>");

            GUILayout.EndArea();
        }
    }
}
