using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GHPC.UI;
using GHPC.Coop.UI;

namespace GHPC.Coop.Core
{
    public class MenuButtonInjector : MonoBehaviour
    {
        public static MenuButtonInjector Instance { get; private set; }

        private float _lastCheckTime = 0f;
        public bool ButtonSpawned { get; private set; } = false;

        private void Awake()
        {
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ButtonSpawned = false;
        }

        private void Update()
        {
            // Fallback periodic check if Harmony patch hasn't run yet in MainMenu2_Scene
            if (!ButtonSpawned && Time.unscaledTime - _lastCheckTime > 0.5f)
            {
                _lastCheckTime = Time.unscaledTime;
                TryInjectMenuButton();
            }
        }

        /// <summary>
        /// Direct injection called from Harmony patch on BuildMenuController.Start()
        /// </summary>
        public static bool InjectIntoController(BuildMenuController controller)
        {
            if (controller == null) return false;

            try
            {
                var instantBtnField = AccessTools.Field(typeof(BuildMenuController), "_instantActionButton");
                var titleMenuField = AccessTools.Field(typeof(BuildMenuController), "_titleMenu");

                var instantBtn = instantBtnField?.GetValue(controller) as Button;
                var titleMenu = titleMenuField?.GetValue(controller) as RectTransform;

                if (instantBtn == null || titleMenu == null)
                {
                    Debug.LogWarning("[GHPC.Coop] _instantActionButton or _titleMenu not found on BuildMenuController.");
                    return false;
                }

                // Check if already injected
                var existing = titleMenu.Find("GHPC_Coop_MainMenuButton");
                if (existing != null)
                {
                    if (Instance != null) Instance.ButtonSpawned = true;
                    return true;
                }

                // Clone the Instant Action button to inherit exact styling, fonts, sounds, animations
                var clone = UnityEngine.Object.Instantiate(instantBtn.gameObject, titleMenu);
                clone.name = "GHPC_Coop_MainMenuButton";

                // Position directly below Instant Action in the vertical list
                clone.transform.SetSiblingIndex(instantBtn.transform.GetSiblingIndex() + 1);

                // Set text matching the native menu style
                var tmp = clone.GetComponentInChildren<TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = "Co-op Multiplayer ▶";
                }

                // Setup click listener
                var btn = clone.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        Debug.Log("[GHPC.Coop] 'Co-op Multiplayer ▶' clicked in Main Menu -> Opening Co-op popup window");
                        if (CoopArrowMenu.Instance != null)
                        {
                            CoopArrowMenu.Instance.OpenMenu();
                        }
                    });
                }

                if (Instance != null) Instance.ButtonSpawned = true;
                Debug.Log("[GHPC.Coop] >>> SUCCESS: 'Co-op Multiplayer ▶' button injected via BuildMenuController! <<<");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GHPC.Coop] Error in InjectIntoController: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public bool TryInjectMenuButton()
        {
            if (ButtonSpawned) return true;

            try
            {
                // First try finding the active BuildMenuController
                var bmc = UnityEngine.Object.FindObjectOfType<BuildMenuController>();
                if (bmc != null)
                {
                    if (InjectIntoController(bmc))
                    {
                        ButtonSpawned = true;
                        return true;
                    }
                }

                // Fallback: search all buttons in active scenes
                var buttons = Resources.FindObjectsOfTypeAll<Button>();
                foreach (var btn in buttons)
                {
                    if (btn == null || !btn.gameObject.scene.isLoaded) continue;

                    var tmp = btn.GetComponentInChildren<TMP_Text>();
                    if (tmp == null) continue;

                    string text = tmp.text != null ? tmp.text.Trim() : "";
                    string btnName = btn.gameObject.name != null ? btn.gameObject.name : "";

                    // Match English menu names (as confirmed in user screenshot) and Russian fallback
                    bool isTarget = text.Equals("Instant Action", StringComparison.OrdinalIgnoreCase) ||
                                    text.IndexOf("Instant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    text.IndexOf("БЫСТРЫЙ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    btnName.IndexOf("Instant", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!isTarget) continue;

                    var container = btn.transform.parent;
                    if (container == null) continue;

                    var existing = container.Find("GHPC_Coop_MainMenuButton");
                    if (existing != null)
                    {
                        ButtonSpawned = true;
                        return true;
                    }

                    var clone = UnityEngine.Object.Instantiate(btn.gameObject, container);
                    clone.name = "GHPC_Coop_MainMenuButton";
                    clone.transform.SetSiblingIndex(btn.transform.GetSiblingIndex() + 1);

                    var cloneTmp = clone.GetComponentInChildren<TMP_Text>();
                    if (cloneTmp != null)
                    {
                        cloneTmp.text = "Co-op Multiplayer ▶";
                    }

                    var cloneBtn = clone.GetComponent<Button>();
                    if (cloneBtn != null)
                    {
                        cloneBtn.onClick.RemoveAllListeners();
                        cloneBtn.onClick.AddListener(() =>
                        {
                            Debug.Log("[GHPC.Coop] 'Co-op Multiplayer ▶' clicked -> Opening Co-op popup window");
                            if (CoopArrowMenu.Instance != null)
                            {
                                CoopArrowMenu.Instance.OpenMenu();
                            }
                        });
                    }

                    ButtonSpawned = true;
                    Debug.Log($"[GHPC.Coop] >>> SUCCESS: 'Co-op Multiplayer ▶' button injected after '{text}'! <<<");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GHPC.Coop] TryInjectMenuButton error: {ex.Message}");
            }

            return false;
        }
    }

    /// <summary>
    /// Harmony patch targeting BuildMenuController.Start()
    /// </summary>
    [HarmonyPatch(typeof(BuildMenuController), "Start")]
    public static class BuildMenuController_Start_Patch
    {
        private static void Postfix(BuildMenuController __instance)
        {
            Debug.Log("[GHPC.Coop] BuildMenuController.Start() invoked -> Injecting Co-op Multiplayer button...");
            MenuButtonInjector.InjectIntoController(__instance);
        }
    }
}
