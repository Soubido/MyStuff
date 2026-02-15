using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using NewRhinoGold.Core;

namespace NewRhinoGold.Commands
{
    public static class SelectionHelper
    {
        public static Result SelectObjects(RhinoDoc doc, string objectName, Func<RhinoObject, bool> criteria)
        {
            doc.Objects.UnselectAll();

            var settings = new ObjectEnumeratorSettings
            {
                NormalObjects = true,
                LockedObjects = false,
                HiddenObjects = false,
                DeletedObjects = false,
                IncludeLights = false,
                IncludeGrips = false
            };

            int count = 0;
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
        public override Guid Id => new Guid("A1111111-1111-1111-1111-111111111111");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return SelectionHelper.SelectObjects(doc, "Gems", obj =>
            {
                // KORREKTUR: Suche in Attributen UND Geometrie
                bool hasSmartData = obj.Attributes.UserData.Find(typeof(GemSmartData)) != null ||
                                    (obj.Geometry != null && obj.Geometry.UserData.Find(typeof(GemSmartData)) != null);

                // Fallback: Legacy Strings oder Namenskonvention
                bool hasUserString = obj.Attributes.GetUserString("RG MATERIAL ID") != null;
                bool isNamedGem = (obj.Name != null && obj.Name.IndexOf("PlacedGem", StringComparison.OrdinalIgnoreCase) >= 0);

                return hasSmartData || hasUserString || isNamedGem;
            });
        }
    }

    // --- BEFEHL 2: Wähle alle Heads ---
    public class SelHeadsCommand : Command
    {
        public override string EnglishName => "SelHeads";
        public override Guid Id => new Guid("B2222222-2222-2222-2222-222222222222");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return SelectionHelper.SelectObjects(doc, "Heads", obj =>
            {
                // KORREKTUR: Auch hier Attribute prüfen (falls HeadSmartData existiert)
                // bool hasSmartData = obj.Attributes.UserData.Find(typeof(HeadSmartData)) != null || ...

                return obj.Attributes.Name == "SmartHead" ||
                       obj.Attributes.GetUserString("RG HEAD") != null;
            });
        }
    }

    // --- BEFEHL 3: Wähle alle Bezels ---
    public class SelBezelsCommand : Command
    {
        public override string EnglishName => "SelBezels";
        public override Guid Id => new Guid("C3333333-3333-3333-3333-333333333333");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return SelectionHelper.SelectObjects(doc, "Bezels", obj =>
            {
                // KORREKTUR: Attribute + Geometrie
                bool hasSmartData = obj.Attributes.UserData.Find(typeof(BezelSmartData)) != null ||
                                    (obj.Geometry != null && obj.Geometry.UserData.Find(typeof(BezelSmartData)) != null);

                bool hasUserString = obj.Attributes.GetUserString("RG BEZEL") != null;
                return hasSmartData || hasUserString;
            });
        }
    }

    // --- BEFEHL 4: Wähle alle Cutter ---
    public class SelCuttersCommand : Command
    {
        public override string EnglishName => "SelCutters";
        public override Guid Id => new Guid("D4444444-4444-4444-4444-444444444444");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return SelectionHelper.SelectObjects(doc, "Cutters", obj =>
            {
                // KORREKTUR: Attribute + Geometrie
                bool hasSmartData = obj.Attributes.UserData.Find(typeof(CutterSmartData)) != null ||
                                    (obj.Geometry != null && obj.Geometry.UserData.Find(typeof(CutterSmartData)) != null);

                bool hasUserString = obj.Attributes.GetUserString("RG CUTTER") != null;
                bool hasName = obj.Attributes.Name == "SmartCutter" || obj.Attributes.Name == "RG Cutter";

                return hasSmartData || hasUserString || hasName;
            });
        }
    }
}