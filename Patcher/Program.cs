using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Patcher
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Run(args);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[CRITICAL ERROR]: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }
            finally
            {
                PauseIfInteractive();
            }
        }

        static void Run(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("GHPC Co-op Mod Installer / Uninstaller");
            Console.WriteLine("==================================================");

            // Auto-detect game directory: current dir, parent dir, or hardcoded default
            string managedDir = FindManagedDir();
            if (string.IsNullOrEmpty(managedDir))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: Could not find GHPC_Data\\Managed directory!");
                Console.WriteLine("Please place this installer in your GHPC game folder or run it from the mod directory.");
                Console.ResetColor();
                return;
            }

            string backupPath = Path.Combine(managedDir, "Assembly-CSharp.dll.bak");
            string targetPath = Path.Combine(managedDir, "Assembly-CSharp.dll");
            string coopDestPath = Path.Combine(managedDir, "GHPC.Coop.dll");

            // Source GHPC.Coop.dll location
            string coopSourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GHPC.Coop.dll");
            if (!File.Exists(coopSourcePath))
            {
                // Fallback to managedDir if already copied
                coopSourcePath = coopDestPath;
            }

            Console.WriteLine($"Game Managed: {managedDir}");
            Console.WriteLine($"Target DLL:   {targetPath}");
            Console.WriteLine();

            bool isUninstall = args.Contains("/u", StringComparer.OrdinalIgnoreCase) || 
                                args.Contains("-u", StringComparer.OrdinalIgnoreCase) ||
                                args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase);

            if (args.Length == 0)
            {
                Console.WriteLine("Select an option:");
                Console.WriteLine("  [1] Install / Update Co-op Mod (Patcher)");
                Console.WriteLine("  [2] Uninstall Co-op Mod (Restore Vanilla Game)");
                Console.WriteLine("  [3] Exit");
                Console.Write("Enter choice (1/2/3) [Default: 1]: ");
                string choice = Console.ReadLine()?.Trim();

                if (choice == "2") isUninstall = true;
                else if (choice == "3") return;
            }

            if (isUninstall)
            {
                Uninstall(backupPath, targetPath, coopDestPath);
            }
            else
            {
                Install(managedDir, backupPath, targetPath, coopSourcePath, coopDestPath);
            }
        }

        static void Uninstall(string backupPath, string targetPath, string coopDestPath)
        {
            Console.WriteLine("\n[UNINSTALLING MOD...]");
            if (!File.Exists(backupPath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Backup file (Assembly-CSharp.dll.bak) not found. Game might already be vanilla.");
                Console.ResetColor();
                return;
            }

            try
            {
                File.Copy(backupPath, targetPath, true);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SUCCESS: Original Assembly-CSharp.dll restored!");
                Console.ResetColor();

                if (File.Exists(coopDestPath))
                {
                    try { File.Delete(coopDestPath); } catch { }
                }

                Console.WriteLine("GHPC Co-op Mod completely uninstalled. The game is 100% vanilla.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Uninstall error: {ex.Message}");
                Console.ResetColor();
            }
        }

        static void Install(string managedDir, string backupPath, string targetPath, string coopSourcePath, string coopDestPath)
        {
            Console.WriteLine("\n[INSTALLING / UPDATING MOD...]");

            if (!File.Exists(targetPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Target file not found: {targetPath}");
                Console.ResetColor();
                return;
            }

            // 1. Create clean backup from current file if backup doesn't exist yet
            if (!File.Exists(backupPath))
            {
                Console.WriteLine("Creating initial backup: Assembly-CSharp.dll.bak ...");
                File.Copy(targetPath, backupPath, false);
            }

            // 2. Copy GHPC.Coop.dll into Managed if source is external
            if (File.Exists(coopSourcePath) && !string.Equals(Path.GetFullPath(coopSourcePath), Path.GetFullPath(coopDestPath), StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Copying GHPC.Coop.dll to game folder...");
                File.Copy(coopSourcePath, coopDestPath, true);
            }

            if (!File.Exists(coopDestPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: GHPC.Coop.dll not found in {coopDestPath} or installer directory!");
                Console.ResetColor();
                return;
            }

            try
            {
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(managedDir);

                var readerParams = new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadWrite = false
                };

                // Read GHPC.Coop.dll
                var coopAsm = AssemblyDefinition.ReadAssembly(coopDestPath, readerParams);
                var bootstrapperType = coopAsm.MainModule.GetType("GHPC.Coop.Core.Bootstrapper");
                if (bootstrapperType == null)
                {
                    Console.WriteLine("ERROR: Could not find GHPC.Coop.Core.Bootstrapper in GHPC.Coop.dll!");
                    return;
                }

                var onMenuStartDef = bootstrapperType.Methods.FirstOrDefault(m => m.Name == "OnMenuStart");
                var onMenuGUIDef = bootstrapperType.Methods.FirstOrDefault(m => m.Name == "OnMenuGUI");
                var onSceneStartDef = bootstrapperType.Methods.FirstOrDefault(m => m.Name == "OnSceneControllerStart");
                var onSceneGUIDef = bootstrapperType.Methods.FirstOrDefault(m => m.Name == "OnSceneControllerGUI");

                // Always patch from clean backup to avoid stacking hooks
                var gameAsm = AssemblyDefinition.ReadAssembly(backupPath, readerParams);
                var gameMod = gameAsm.MainModule;

                var onMenuStartRef = gameMod.ImportReference(onMenuStartDef);
                var onMenuGUIRef = gameMod.ImportReference(onMenuGUIDef);
                var onSceneStartRef = gameMod.ImportReference(onSceneStartDef);
                var onSceneGUIRef = gameMod.ImportReference(onSceneGUIDef);
                var voidType = gameMod.TypeSystem.Void;

                // Patch BuildMenuController
                var buildMenuType = gameMod.GetType("GHPC.UI.BuildMenuController");
                if (buildMenuType != null)
                {
                    var startMethod = buildMenuType.Methods.FirstOrDefault(m => m.Name == "Start");
                    if (startMethod != null)
                    {
                        var il = startMethod.Body.GetILProcessor();
                        var lastRet = startMethod.Body.Instructions.LastOrDefault(i => i.OpCode == OpCodes.Ret);
                        if (lastRet != null)
                        {
                            il.InsertBefore(lastRet, il.Create(OpCodes.Ldarg_0));
                            il.InsertBefore(lastRet, il.Create(OpCodes.Call, onMenuStartRef));
                        }
                    }

                    var existingOnGUI = buildMenuType.Methods.FirstOrDefault(m => m.Name == "OnGUI");
                    if (existingOnGUI == null)
                    {
                        var onGuiMethod = new MethodDefinition("OnGUI", MethodAttributes.Public | MethodAttributes.HideBySig, voidType);
                        var il = onGuiMethod.Body.GetILProcessor();
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Call, onMenuGUIRef);
                        il.Emit(OpCodes.Ret);
                        buildMenuType.Methods.Add(onGuiMethod);
                    }
                }

                // Patch SceneController
                var sceneCtrlType = gameMod.GetType("GHPC.SceneController");
                if (sceneCtrlType != null)
                {
                    var startMethod = sceneCtrlType.Methods.FirstOrDefault(m => m.Name == "Start")
                                      ?? sceneCtrlType.Methods.FirstOrDefault(m => m.Name == "Awake");
                    if (startMethod != null)
                    {
                        var il = startMethod.Body.GetILProcessor();
                        var lastRet = startMethod.Body.Instructions.LastOrDefault(i => i.OpCode == OpCodes.Ret);
                        if (lastRet != null)
                        {
                            il.InsertBefore(lastRet, il.Create(OpCodes.Ldarg_0));
                            il.InsertBefore(lastRet, il.Create(OpCodes.Call, onSceneStartRef));
                        }
                    }

                    var existingOnGUI = sceneCtrlType.Methods.FirstOrDefault(m => m.Name == "OnGUI");
                    if (existingOnGUI == null)
                    {
                        var onGuiMethod = new MethodDefinition("OnGUI", MethodAttributes.Public | MethodAttributes.HideBySig, voidType);
                        var il = onGuiMethod.Body.GetILProcessor();
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Call, onSceneGUIRef);
                        il.Emit(OpCodes.Ret);
                        sceneCtrlType.Methods.Add(onGuiMethod);
                    }
                }

                gameAsm.Write(targetPath);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n==================================================");
                Console.WriteLine("SUCCESS: GHPC Co-op Mod successfully installed!");
                Console.WriteLine("The menu will appear in the top-right corner of the game.");
                Console.WriteLine("To uninstall at any time, run this tool and choose [2].");
                Console.WriteLine("==================================================");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Patcher error: {ex.Message}");
                Console.ResetColor();
            }
        }

        static string FindManagedDir()
        {
            string[] candidates = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GHPC_Data", "Managed"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "GHPC_Data", "Managed"),
                Path.Combine(Directory.GetCurrentDirectory(), "GHPC_Data", "Managed"),
                @"D:\Games\Gunner.HEAT.PC.20261014\game\GHPC_Data\Managed"
            };

            foreach (var c in candidates)
            {
                if (Directory.Exists(c) && File.Exists(Path.Combine(c, "Assembly-CSharp.dll")))
                {
                    return Path.GetFullPath(c);
                }
            }

            // Interactive fallback prompt if run outside game folder
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Автопоиск папки игры не дал результата.");
            Console.WriteLine("Перетащите сюда папку с игрой GHPC (или файл GHPC.exe) и нажмите Enter:");
            Console.Write("Путь к игре (Game Path): ");
            Console.ResetColor();

            string input = Console.ReadLine()?.Trim('"', ' ', '\'');
            if (!string.IsNullOrWhiteSpace(input))
            {
                if (File.Exists(input) && Path.GetFileName(input).Equals("Assembly-CSharp.dll", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetDirectoryName(Path.GetFullPath(input));
                }

                string candidateManaged = Directory.Exists(Path.Combine(input, "GHPC_Data", "Managed"))
                    ? Path.Combine(input, "GHPC_Data", "Managed")
                    : (Directory.Exists(Path.Combine(input, "Managed")) ? Path.Combine(input, "Managed") : input);

                if (Directory.Exists(candidateManaged) && File.Exists(Path.Combine(candidateManaged, "Assembly-CSharp.dll")))
                {
                    return Path.GetFullPath(candidateManaged);
                }
            }

            return null;
        }

        static void PauseIfInteractive()
        {
            try
            {
                Console.WriteLine("\nНажмите Enter для выхода / Press Enter to exit...");
                Console.ReadLine();
            }
            catch { }
        }
    }
}
