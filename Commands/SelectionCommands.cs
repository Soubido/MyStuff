using System;
using System.Collections.Generic; // Für IEnumerable
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using NewRhinoGold.Core;

namespace NewRhinoGold.Commands
{
    // Basis-Klasse oder Hilfsmethode, um Wiederholungen zu vermeiden
    public static class SelectionHelper
    {
        public static Result SelectObjects(RhinoDoc doc, string objectName, Func<RhinoObject, bool> criteria)
        {
            doc.Objects.UnselectAll();

            // Einstellungen für den Iterator: Nur normale, nicht gelöschte Objekte
            var settings = new ObjectEnumeratorSettings
            {
                NormalObjects = true,
                LockedObjects = false,   // Wollen wir gesperrte Objekte auswählen? Meistens nein.
                HiddenObjects = false,   // Wollen wir versteckte Objekte auswählen? Meistens nein.
                DeletedObjects = false,
                IncludeLights = false,
                IncludeGrips = false
            };

            int count = 0;

            // Viel schneller als 'foreach (var obj in doc.Objects)'
            foreach (var obj in doc.Objects.GetObjectList(settings))
            {
                if (criteria(obj))
                {
                    obj.Select(true);
                    count++;
                }
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine($"Selected {count} {objectName}.");

            return Result.Success;
        }
    }

    // --- BEFEHL 1: Wähle alle Smart Gems ---
    public class SelGemsCommand : Command
    {
        public override string EnglishName => "SelGems";
        // WICHTIG: Generiere hier eine echte GUID!
        public override Guid Id => new Guid("A1111111-1111-1111-1111-111111111111");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return SelectionHelper.SelectObjects(doc, "Gems", obj =>
            {
                // Logik für Gems
                bool hasSmartData = obj.Geometry?.UserData.Find(typeof(GemSmartData)) != null;
                bool hasUserString = obj.Attributes.GetUserString("RG MATERIAL ID") != null;
                return hasSmartData || hasUserString;
            });
        }
    }

    // --- BEFEHL 2: Wähle alle Heads (Krappenfassungen) ---
    public class SelHeadsCommand : Command
    {
        public override string EnglishName => "SelHeads";
        public override Guid Id => new Guid("B2222222-2222-2222-2222-222222222222");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return SelectionHelper.SelectObjects(doc, "Heads", obj =>
            {
                // Logik für Heads
                return obj.Attributes.Name == "SmartHead" ||
                       obj.Attributes.GetUserString("RG HEAD") != null;
            });
        }
    }

    // --- BEFEHL 3: Wähle alle Bezels (Zargen) ---
    public class SelBezelsCommand : Command
    {
        public override string EnglishName => "SelBezels";
        public override Guid Id => new Guid("C3333333-3333-3333-3333-333333333333");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return SelectionHelper.SelectObjects(doc, "Bezels", obj =>
            {
                // Logik für Bezels
                // Hinweis: Namespace für BezelSmartData ggf. anpassen
                bool hasSmartData = obj.Geometry?.UserData.Find(typeof(BezelStudio.BezelSmartData)) != null;
                bool hasUserString = obj.Attributes.GetUserString("RG BEZEL") != null;
                return hasSmartData || hasUserString;
            });
        }
    }
    // --- BEFEHL 4: Wähle alle Cutter (Schneideobjekte) ---
    public class SelCuttersCommand : Command
    {
        public override string EnglishName => "SelCutters";
        // Denke daran, eine neue, einzigartige GUID zu generieren
        public override Guid Id => new Guid("D4444444-4444-4444-4444-444444444444");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Wir nutzen hier wieder den SelectionHelper aus dem vorigen Schritt
            return SelectionHelper.SelectObjects(doc, "Cutters", obj =>
            {
                // 1. Prüfung auf SmartData 
                // WICHTIG: Prüfe, ob die Klasse wirklich 'CutterSmartData' heißt (z.B. in NewRhinoGold.Core)
                bool hasSmartData = false;

                // Falls du eine CutterSmartData Klasse hast:
                // hasSmartData = obj.Geometry?.UserData.Find(typeof(CutterSmartData)) != null;

                // 2. Prüfung auf UserString (Standard RG Tag)
                bool hasUserString = obj.Attributes.GetUserString("RG CUTTER") != null;

                // 3. Prüfung auf Objekt-Name (falls Cutter oft so benannt werden)
                bool hasName = obj.Attributes.Name == "SmartCutter";

                return hasSmartData || hasUserString || hasName;
            });
        }
    }
}