using System;
using System.IO;
using System.Collections.Generic;

namespace TrailmakersSurroundUI
{
    internal static class InstallerTests
    {
        public static int Run(string loader)
        {
            string root = Path.Combine(Path.GetTempPath(), "TrailmakersUiInstallerTests-" + Guid.NewGuid().ToString("N"));
            List<string> results = new List<string>();
            try
            {
                if (String.IsNullOrEmpty(loader) || !File.Exists(loader)) throw new Exception("Supply the built ReShade stub fixture.");
                Directory.CreateDirectory(root);
                string game = NewGame(root, "normal");
                File.Copy(loader, Path.Combine(game, "dxgi.dll"));
                string shader = Path.Combine(game, "ReShade.ini"); File.WriteAllText(shader, "keep existing settings");
                string other = Path.Combine(game, "another.addon64"); File.WriteAllText(other, "unrelated add-on");
                string gameBefore = Payload.FileHash(Path.Combine(game, "GameAssembly.dll"));
                string loaderBefore = Payload.FileHash(Path.Combine(game, "dxgi.dll"));
                InstallerCore.Install(game);
                Check(Payload.FileHash(Path.Combine(game, InstallerCore.AddonName)) == PayloadInfo.Sha256, "payload matches release hash");
                InstallerCore.Install(game);
                Check(File.Exists(Path.Combine(game, InstallerCore.RecordName)), "idempotent install has ownership record");
                string config = Path.Combine(game, "surround-ui.ini"); File.WriteAllText(config, "[Inventory]\nEnabled=0");
                InstallerCore.Uninstall(game);
                Check(!File.Exists(Path.Combine(game, InstallerCore.AddonName)), "uninstall removes the owned add-on");
                Check(File.ReadAllText(config).Contains("Enabled=0"), "uninstall preserves the user's configuration");
                Check(File.ReadAllText(shader) == "keep existing settings" && File.ReadAllText(other) == "unrelated add-on", "existing ReShade and other add-ons preserved");
                Check(Payload.FileHash(Path.Combine(game, "GameAssembly.dll")) == gameBefore && Payload.FileHash(Path.Combine(game, "dxgi.dll")) == loaderBefore, "game DLL and loader bytes unchanged");
                results.Add("PASS install, repeated install, uninstall and preservation");
                InstallerCore.Uninstall(game);
                results.Add("PASS repeated uninstall");

                string missing = NewGame(root, "missing-loader");
                Throws(delegate { InstallerCore.Install(missing); }, "missing ReShade rejected");
                Check(!File.Exists(Path.Combine(missing, InstallerCore.AddonName)), "missing loader leaves no payload");
                File.WriteAllText(Path.Combine(missing, "dxgi.dll"), "not a loader");
                Throws(delegate { InstallerCore.Install(missing); }, "unrelated dxgi.dll rejected");
                results.Add("PASS missing and unrelated loaders rejected without changes");

                string conflict = NewGame(root, "conflicting-addon");
                File.Copy(loader, Path.Combine(conflict, "dxgi.dll"));
                File.WriteAllText(Path.Combine(conflict, InstallerCore.AddonName), "somebody else's file");
                Throws(delegate { InstallerCore.Install(conflict); }, "unowned conflicting file rejected");
                Throws(delegate { InstallerCore.Uninstall(conflict); }, "unowned conflicting file not removed");
                Check(File.ReadAllText(Path.Combine(conflict, InstallerCore.AddonName)) == "somebody else's file", "conflicting file preserved");
                results.Add("PASS unknown add-on never overwritten or deleted");

                string changed = NewGame(root, "changed-addon");
                File.Copy(loader, Path.Combine(changed, "dxgi.dll"));
                InstallerCore.Install(changed);
                File.AppendAllText(Path.Combine(changed, InstallerCore.AddonName), "tampered");
                Throws(delegate { InstallerCore.Install(changed); }, "changed owned add-on rejected");
                Throws(delegate { InstallerCore.Uninstall(changed); }, "changed owned add-on not removed");
                results.Add("PASS modified installations preserved for review");

                string corrupt = NewGame(root, "corrupt-record");
                File.Copy(loader, Path.Combine(corrupt, "dxgi.dll"));
                File.WriteAllText(Path.Combine(corrupt, InstallerCore.RecordName), "invalid JSON");
                Throws(delegate { InstallerCore.Install(corrupt); }, "corrupt record rejected");
                Check(!File.Exists(Path.Combine(corrupt, InstallerCore.AddonName)), "corrupt record leaves no payload");
                results.Add("PASS corrupt ownership records fail safely");
                Throws(delegate { InstallerCore.Install(root); }, "non-game folder rejected");
                results.Add("PASS non-game folder rejected");
                File.WriteAllLines(Path.Combine(root, "results.txt"), results.ToArray());
                Console.WriteLine(String.Join(Environment.NewLine, results));
                Console.WriteLine("Fixture results: " + root);
                return 0;
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "failure.txt"), error.ToString());
                Console.Error.WriteLine(error.ToString());
                Console.Error.WriteLine("Fixtures retained at: " + root);
                return 1;
            }
        }

        private static string NewGame(string root, string name)
        {
            string path = Path.Combine(root, name); Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "Trailmakers.exe"), "fixture game marker");
            File.WriteAllText(Path.Combine(path, "GameAssembly.dll"), "fixture game DLL marker");
            return path;
        }
        private static void Check(bool result, string name) { if (!result) throw new Exception("FAIL: " + name); }
        private static void Throws(Action action, string name)
        {
            try { action(); } catch (InvalidOperationException) { return; }
            throw new Exception("FAIL: " + name);
        }
    }
}
