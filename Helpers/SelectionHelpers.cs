using Rhino.DocObjects;
using NewRhinoGold.Core;

namespace NewRhinoGold.Helpers
{
    /// <summary>
    /// Zentrale Logik zur Identifizierung von Objekttypen.
    /// Prüft sowohl Modern Architecture (UserData) als auch Legacy (UserStrings).
    /// </summary>
    public static class SelectionHelpers
    {
        public static bool IsGem(RhinoObject obj)
        {
            if (obj?.Geometry == null) return false;

            // 1. Modern
            if (obj.Geometry.UserData.Find(typeof(GemSmartData)) != null) return true;

            // 2. Legacy
            return HasUserString(obj, "RG GEM", "GemData");
        }

        public static bool IsBezel(RhinoObject obj)
        {
            if (obj?.Geometry == null) return false;

            if (obj.Geometry.UserData.Find(typeof(BezelSmartData)) != null) return true;

            return HasUserString(obj, "RG BEZEL", "BezelData");
        }

        public static bool IsHead(RhinoObject obj)
        {
            if (obj?.Geometry == null) return false;

            if (obj.Geometry.UserData.Find(typeof(HeadSmartData)) != null) return true;

            return HasUserString(obj, "RG HEAD", "HeadData");
        }

        public static bool IsCutter(RhinoObject obj)
        {
            if (obj?.Geometry == null) return false;

            if (obj.Geometry.UserData.Find(typeof(CutterSmartData)) != null) return true;

            return HasUserString(obj, "RG CUTTER", "CutterData");
        }

        public static bool IsProng(RhinoObject obj)
        {
            if (obj?.Geometry == null) return false;

            if (obj.Geometry.UserData.Find(typeof(ProngSmartData)) != null) return true;

            return HasUserString(obj, "RG DYNAMICPRONG", "RG PRONG", "ProngData");
        }

        // --- Helper ---

        private static bool HasUserString(RhinoObject obj, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!string.IsNullOrEmpty(obj.Geometry.GetUserString(key)))
                    return true;
                
                // Manche Legacy-Objekte haben Attribute am ObjectAttributes statt an Geometry
                if (!string.IsNullOrEmpty(obj.Attributes.GetUserString(key)))
                    return true;
            }
            return false;
        }
    }
}