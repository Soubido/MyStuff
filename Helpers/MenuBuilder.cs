using System;
using Rhino;
using Rhino.UI;
using Eto.Forms; // Wir nutzen jetzt rein Eto, kein System.Windows.Forms mehr!

namespace NewRhinoGold.Helpers
{
    public static class MenuBuilder
    {
        public static void CreateBStudioMenu()
        {
            // DEBUG: Statusmeldung
            // RhinoApp.WriteLine("[BJewel] Starte Eto-MenuBuilder...");

            try
            {
                // 1. Zugriff auf das Rhino-Hauptfenster über Eto (funktioniert immer in Rhino 8)
                var mainWindow = RhinoEtoApp.MainWindow;
                if (mainWindow == null)
                {
                    RhinoApp.WriteLine("[BJewel] Fehler: Eto MainWindow ist null.");
                    return;
                }

                // 2. Zugriff auf das Hauptmenü
                var mainMenu = mainWindow.Menu;
                if (mainMenu == null)
                {
                    RhinoApp.WriteLine("[BJewel] Fehler: Hauptmenü nicht gefunden.");
                    return;
                }

                // 3. Prüfen, ob "BJewel" schon da ist
                string menuName = "BJewel";
                foreach (var item in mainMenu.Items)
                {
                    if (item.Text == menuName) return; // Schon da
                }

                // --- 4. Menü bauen ---

                var rootMenu = new ButtonMenuItem { Text = menuName };

                // Creation
                AddHeader(rootMenu, "Creation");
                AddItem(rootMenu, "Gem Studio", "_GemStudio");
                AddItem(rootMenu, "Gem Creator", "_GemCreator");
                AddItem(rootMenu, "Cutter Studio", "_CutterStudio");
                rootMenu.Items.Add(new SeparatorMenuItem());

                // Components
                AddHeader(rootMenu, "Components");
                AddItem(rootMenu, "Bezel Studio", "_BezelStudio");
                AddItem(rootMenu, "Head Studio", "_HeadStudio");
                AddItem(rootMenu, "Pave Studio", "_PaveStudio");
                rootMenu.Items.Add(new SeparatorMenuItem());

                // Tools
                AddHeader(rootMenu, "Tools");
                AddItem(rootMenu, "Extract Curve", "_ExtractGemCurve");
                AddItem(rootMenu, "Move on Surface", "_MoveOnSurface");
                AddItem(rootMenu, "Engraving Builder", "_EngraveRing");
                rootMenu.Items.Add(new SeparatorMenuItem());

                // Analysis
                AddHeader(rootMenu, "Analysis");
                AddItem(rootMenu, "Gold Weight", "_GoldWeight");
                AddItem(rootMenu, "Gem Report", "_GemReport");
                rootMenu.Items.Add(new SeparatorMenuItem());

                // System
                AddItem(rootMenu, "Show Toolbar", "_RhinoGoldToolbar");

                // --- 5. Menü einfügen ---
                // Wir fügen es vor dem vorletzten Item ein (meistens vor "Help" oder "Window")
                int index = Math.Max(0, mainMenu.Items.Count - 2);
                mainMenu.Items.Insert(index, rootMenu);

                RhinoApp.WriteLine("[BJewel] Menü erfolgreich geladen.");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[BJewel] Kritischer Fehler: {ex.Message}");
            }
        }

        private static void AddItem(ButtonMenuItem parent, string text, string command)
        {
            var item = new ButtonMenuItem { Text = text };
            item.Click += (s, e) => RhinoApp.RunScript(command, true);
            parent.Items.Add(item);
        }

        private static void AddHeader(ButtonMenuItem parent, string text)
        {
            // Eto hat keine direkten "Header", wir simulieren es mit einem deaktivierten Item
            var item = new ButtonMenuItem { Text = text, Enabled = false };
            parent.Items.Add(item);
        }
    }
}