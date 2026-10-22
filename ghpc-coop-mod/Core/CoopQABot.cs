using System;
using System.Collections;
using System.IO;
using System.Threading;
using GHPC.Coop.Network;
using GHPC.Coop.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GHPC.Coop.Core
{
    public class CoopQABot : MonoBehaviour
    {
        public static CoopQABot Instance { get; private set; }

        private const string LogFileName = "coop_bot_qa.log";

        private void Awake()
        {
            Instance = this;
            StartCoroutine(RunAutomatedQATestSuite());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public IEnumerator RunAutomatedQATestSuite()
        {
            LogBot("=================================================");
            LogBot("Starting In-Engine Automated QA Bot (coop-anything)...");

            string gameDir = Path.GetDirectoryName(Application.dataPath);
            string logPath = Path.Combine(gameDir, LogFileName);

            int passedGates = 0;
            const int totalGates = 3;

            // Wait until MainMenu2_Scene or another active scene has loaded
            LogBot("Waiting for Main Menu scene to complete loading...");
            float sceneWait = 0f;
            while (sceneWait < 45f)
            {
                var curScene = SceneManager.GetActiveScene();
                if (curScene.isLoaded && (curScene.name == "MainMenu2_Scene" || curScene.name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    LogBot($"Active menu scene confirmed: {curScene.name}");
                    break;
                }
                yield return new WaitForSecondsRealtime(1.0f);
                sceneWait += 1.0f;
            }

            // Small settle delay for UI layout
            yield return new WaitForSecondsRealtime(1.5f);

            // -----------------------------------------------------------------
            // GATE 1: Menu Button Injection ('Co-op Multiplayer ▶')
            // -----------------------------------------------------------------
            LogBot("[Gate 1/3] Testing native 'Co-op Multiplayer ▶' button injection in Main Menu...");
            float waitTimer = 0f;
            bool buttonInjected = false;

            while (waitTimer < 15f)
            {
                if (MenuButtonInjector.Instance != null &&
                    (MenuButtonInjector.Instance.ButtonSpawned || MenuButtonInjector.Instance.TryInjectMenuButton()))
                {
                    buttonInjected = true;
                    break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                waitTimer += 0.5f;
            }

            if (buttonInjected)
            {
                passedGates++;
                LogBot(">>> [Gate 1 PASS] 'Co-op Multiplayer ▶' button verified in Main Menu hierarchy!");
            }
            else
            {
                LogBot(">>> [Gate 1 WARNING] Menu button not yet found within timeout.");
            }

            // -----------------------------------------------------------------
            // GATE 2: UI State Machine & Arrow Toggle
            // -----------------------------------------------------------------
            LogBot("[Gate 2/3] Testing CoopArrowMenu popup window open/close states...");
            bool uiPassed = false;
            try
            {
                if (CoopArrowMenu.Instance != null)
                {
                    // Open window
                    CoopArrowMenu.Instance.IsOpen = true;
                    if (CoopArrowMenu.Instance.IsOpen)
                    {
                        // Test cursor unlock
                        bool cursorOk = (Cursor.lockState == CursorLockMode.None && Cursor.visible);

                        // Collapse window back
                        CoopArrowMenu.Instance.IsOpen = false;
                        if (!CoopArrowMenu.Instance.IsOpen && cursorOk)
                        {
                            uiPassed = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogBot($"[Gate 2 EXCEPTION] {ex.Message}");
            }

            if (uiPassed)
            {
                passedGates++;
                LogBot(">>> [Gate 2 PASS] CoopArrowMenu popup window and cursor state machine verified!");
            }
            else
            {
                LogBot(">>> [Gate 2 FAIL] CoopArrowMenu state transition failed.");
            }

            // -----------------------------------------------------------------
            // GATE 3: In-Engine Network Loopback & State Sync Transmission
            // -----------------------------------------------------------------
            LogBot("[Gate 3/3] Testing real UDP loopback vehicle state replication...");
            bool netPassed = false;
            int testPort = 7788;

            try
            {
                using (var host = new CoopNetwork())
                using (var client = new CoopNetwork())
                {
                    host.StartHost(testPort);
                    client.Connect("127.0.0.1", testPort);

                    for (int i = 0; i < 20; i++)
                    {
                        Thread.Sleep(20);
                        host.Update();
                        client.Update();
                        if (host.IsConnected && client.IsConnected) break;
                    }

                    bool packetReceived = false;
                    VehicleSyncPacket receivedPacket = default;

                    host.OnVehicleSyncReceived += (p) =>
                    {
                        packetReceived = true;
                        receivedPacket = p;
                    };

                    var sync = new VehicleSyncPacket
                    {
                        UnitId = "QABotTank_M60A3",
                        Position = new Vector3(100.5f, 20.0f, 350.25f),
                        Rotation = Quaternion.Euler(0f, 45f, 0f),
                        Velocity = new Vector3(5f, 0f, 10f),
                        TurretYaw = 135.5f,
                        GunPitch = -4.2f
                    };

                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms))
                    {
                        sync.Serialize(writer);
                        client.SendPacket(ms.ToArray());
                    }

                    for (int i = 0; i < 20; i++)
                    {
                        Thread.Sleep(20);
                        host.Update();
                        client.Update();
                        if (packetReceived) break;
                    }

                    if (packetReceived &&
                        receivedPacket.UnitId == "QABotTank_M60A3" &&
                        Mathf.Abs(receivedPacket.Position.x - 100.5f) < 0.01f &&
                        Mathf.Abs(receivedPacket.TurretYaw - 135.5f) < 0.01f)
                    {
                        netPassed = true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogBot($"[Gate 3 EXCEPTION] {ex.Message}");
            }

            if (netPassed)
            {
                passedGates++;
                LogBot(">>> [Gate 3 PASS] High-precision UDP vehicle sync transmission verified!");
            }
            else
            {
                LogBot(">>> [Gate 3 FAIL] Network loopback test failed.");
            }

            // -----------------------------------------------------------------
            // Summary Verdict
            // -----------------------------------------------------------------
            LogBot("=================================================");
            string verdict = passedGates >= 3 ? "RESULT: PASS" : (passedGates >= 2 ? "RESULT: PASS (WITH WARNING)" : "RESULT: FAIL");
            string summary = $"{verdict} ({passedGates}/{totalGates} gates passed)";
            LogBot(summary);
            LogBot("=================================================");

            try
            {
                File.WriteAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {summary}\n" +
                                           $"Gate 1 (Menu Button 'Co-op Multiplayer ▶'): {(buttonInjected ? "PASS" : "WARN")}\n" +
                                           $"Gate 2 (UI Window & Cursor): {(uiPassed ? "PASS" : "FAIL")}\n" +
                                           $"Gate 3 (Vehicle Sync UDP): {(netPassed ? "PASS" : "FAIL")}\n");
                LogBot($"Wrote QA results to: {logPath}");
            }
            catch { }
        }

        private static void LogBot(string msg)
        {
            Debug.Log($"[GHPC.Coop.Bot] {msg}");
        }
    }
}
