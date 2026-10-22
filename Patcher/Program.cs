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
            string managedDir = @"D:\Games\Gunner.HEAT.PC.20261014\game\GHPC_Data\Managed";
            string backupPath = Path.Combine(managedDir, "Assembly-CSharp.dll.bak");
            string targetPath = Path.Combine(managedDir, "Assembly-CSharp.dll");
            string coopPath = Path.Combine(managedDir, "GHPC.Coop.dll");

            Console.WriteLine("==================================================");
            Console.WriteLine("Starting Direct Game Assembly-CSharp Patcher...");
            Console.WriteLine($"Backup: {backupPath}");
            Console.WriteLine($"Target: {targetPath}");
            Console.WriteLine($"Co-op:  {coopPath}");

            if (!File.Exists(backupPath))
            {
                Console.WriteLine("ERROR: Backup file does not exist!");
                return;
            }

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(managedDir);

            var readerParams = new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadWrite = false
            };

            // 1. Load GHPC.Coop.dll to get Bootstrapper method references
            var coopAsm = AssemblyDefinition.ReadAssembly(coopPath, readerParams);
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

            Console.WriteLine("Found all Bootstrapper methods in GHPC.Coop.dll.");

            // 2. Load Assembly-CSharp from backup
            var gameAsm = AssemblyDefinition.ReadAssembly(backupPath, readerParams);
            var gameMod = gameAsm.MainModule;

            // Import methods into Assembly-CSharp module
            var onMenuStartRef = gameMod.ImportReference(onMenuStartDef);
            var onMenuGUIRef = gameMod.ImportReference(onMenuGUIDef);
            var onSceneStartRef = gameMod.ImportReference(onSceneStartDef);
            var onSceneGUIRef = gameMod.ImportReference(onSceneGUIDef);
            var voidType = gameMod.TypeSystem.Void;

            // -------------------------------------------------------------
            // Patch 1: GHPC.UI.BuildMenuController
            // -------------------------------------------------------------
            var buildMenuType = gameMod.GetType("GHPC.UI.BuildMenuController");
            if (buildMenuType != null)
            {
                Console.WriteLine("Found GHPC.UI.BuildMenuController.");

                // A. Hook Start()
                var startMethod = buildMenuType.Methods.FirstOrDefault(m => m.Name == "Start");
                if (startMethod != null)
                {
                    var il = startMethod.Body.GetILProcessor();
                    var lastRet = startMethod.Body.Instructions.LastOrDefault(i => i.OpCode == OpCodes.Ret);
                    if (lastRet != null)
                    {
                        il.InsertBefore(lastRet, il.Create(OpCodes.Ldarg_0));
                        il.InsertBefore(lastRet, il.Create(OpCodes.Call, onMenuStartRef));
                        Console.WriteLine("Injected OnMenuStart call into BuildMenuController.Start()");
                    }
                }

                // B. Add OnGUI() method
                var existingOnGUI = buildMenuType.Methods.FirstOrDefault(m => m.Name == "OnGUI");
                if (existingOnGUI == null)
                {
                    var onGuiMethod = new MethodDefinition("OnGUI",
                        MethodAttributes.Public | MethodAttributes.HideBySig,
                        voidType);

                    var il = onGuiMethod.Body.GetILProcessor();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, onMenuGUIRef);
                    il.Emit(OpCodes.Ret);

                    buildMenuType.Methods.Add(onGuiMethod);
                    Console.WriteLine("Added native OnGUI() method to BuildMenuController!");
                }
            }
            else
            {
                Console.WriteLine("WARNING: GHPC.UI.BuildMenuController not found!");
            }

            // -------------------------------------------------------------
            // Patch 2: GHPC.SceneController
            // -------------------------------------------------------------
            var sceneCtrlType = gameMod.GetType("GHPC.SceneController");
            if (sceneCtrlType != null)
            {
                Console.WriteLine("Found GHPC.SceneController.");

                // A. Hook Start() if present, or Awake()
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
                        Console.WriteLine($"Injected OnSceneControllerStart call into SceneController.{startMethod.Name}()");
                    }
                }

                // B. Add OnGUI() method
                var existingOnGUI = sceneCtrlType.Methods.FirstOrDefault(m => m.Name == "OnGUI");
                if (existingOnGUI == null)
                {
                    var onGuiMethod = new MethodDefinition("OnGUI",
                        MethodAttributes.Public | MethodAttributes.HideBySig,
                        voidType);

                    var il = onGuiMethod.Body.GetILProcessor();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, onSceneGUIRef);
                    il.Emit(OpCodes.Ret);

                    sceneCtrlType.Methods.Add(onGuiMethod);
                    Console.WriteLine("Added native OnGUI() method to SceneController!");
                }
            }

            // 3. Save modified Assembly-CSharp.dll
            gameAsm.Write(targetPath);
            Console.WriteLine("SUCCESS: Assembly-CSharp.dll successfully patched and written to game folder!");
            Console.WriteLine("==================================================");
        }
    }
}
