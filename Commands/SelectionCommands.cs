using System;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using NewRhinoGold.Core; // Für GemSmartData etc.

namespace NewRhinoGold.Commands
{
    // --- BEFEHL 1: Wähle alle Smart Gems ---
    public class SelGemsCommand : Command
    {
        public override string EnglishName => "SelGems";
        public override Guid Id => new Guid("A1111111-1111-1111-1111-111111111111");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            int count = 0;
            doc.Objects.UnselectAll();

            foreach (var obj in doc.Objects)
            {
                // Prüfen auf SmartData oder UserString
                if (obj.Geometry.UserData.Find(typeof(GemSmartData)) != null ||
                    obj.Attributes.GetUserString("RG MATERIAL ID") != null)
                {
                    obj.Select(true);
                    count++;
                }
            }
            doc.Views.Redraw();
            RhinoApp.WriteLine($"Selected {count} Gems.");
            return Result.Success;
        }
    }

    // --- BEFEHL 2: Wähle alle Heads (Krappenfassungen) ---
    public class SelHeadsCommand : Command
    {
        public override string EnglishName => "SelHeads";
        public override Guid Id => new Guid("B2222222-2222-2222-2222-222222222222");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            int count = 0;
            doc.Objects.UnselectAll();

            foreach (var obj in doc.Objects)
            {
                // Prüfen auf Name "SmartHead" oder UserString "RG HEAD" (falls wir das mal gesetzt haben)
                if (obj.Attributes.Name == "SmartHead" ||
                    obj.Attributes.GetUserString("RG HEAD") != null)
                {
                    obj.Select(true);
                    count++;
                }
            }
            doc.Views.Redraw();
            RhinoApp.WriteLine($"Selected {count} Heads.");
            return Result.Success;
        }
    }

    // --- BEFEHL 3: Wähle alle Bezels (Zargen) ---
    public class SelBezelsCommand : Command
    {
        public override string EnglishName => "SelBezels";
        public override Guid Id => new Guid("C3333333-3333-3333-3333-333333333333");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            int count = 0;
            doc.Objects.UnselectAll();

            foreach (var obj in doc.Objects)
            {
                if (obj.Geometry.UserData.Find(typeof(BezelStudio.BezelSmartData)) != null ||
                    obj.Attributes.GetUserString("RG BEZEL") != null)
                {
                    obj.Select(true);
                    count++;
                }
            }
            doc.Views.Redraw();
            RhinoApp.WriteLine($"Selected {count} Bezels.");
            return Result.Success;
        }
    }
}