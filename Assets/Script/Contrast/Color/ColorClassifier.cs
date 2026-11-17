namespace Contrast.Color
{
    public static class ColorClassifier
    {
        // Color ranges:
        // White: 0 -> 84 (chunk size 84)
        // Gray: 85 -> 169 (chunk size 84)
        // Black: 170 -> 254 (chunk size 84)
        // Everything / Universal: 255 (value of everything, any player can stand)
        public const float WhiteMin = 0f;
        public const float WhiteMax = 84f;
        public const float GrayMin = 85f;
        public const float GrayMax = 169f;
        public const float BlackMin = 170f;
        public const float BlackMax = 254f;
        public const float UniversalValue = 255f;
        public const float ChunkSpan = 84f;

        public static LogicalColor Classify(float raw)
        {
            if (raw >= 254.5f)
                return LogicalColor.Universal;
            if (raw <= WhiteMax + 0.5f)
                return LogicalColor.White;
            if (raw <= GrayMax + 0.5f)
                return LogicalColor.Gray;
            return LogicalColor.Black;
        }

        public static float DefaultValue(LogicalColor c)
        {
            switch (c)
            {
                case LogicalColor.White: return 0f;
                case LogicalColor.Gray: return 127f;
                case LogicalColor.Black: return 212f;
                case LogicalColor.Universal: return 255f;
                default: return 0f;
            }
        }

        public static float ToValue(LogicalColor c)
        {
            switch (c)
            {
                case LogicalColor.White: return 1f;
                case LogicalColor.Gray: return 0.522f;
                case LogicalColor.Black: return 0.02f;
                case LogicalColor.Universal: return 0.85f;
                default: return 1f;
            }
        }

        public static UnityEngine.Color GetPlatformVisualColor(float raw)
        {
            LogicalColor logical = Classify(raw);
            switch (logical)
            {
                case LogicalColor.Universal:
                    // Yellow visual display for Universal/FIXED (255) while raw property remains 255
                    return new UnityEngine.Color(0.95f, 0.78f, 0.18f, 1f);

                case LogicalColor.White:
                    return UnityEngine.Color.white;

                case LogicalColor.Gray:
                    return new UnityEngine.Color(0.55f, 0.55f, 0.55f, 1f);

                case LogicalColor.Black:
                default:
                    return new UnityEngine.Color(0.12f, 0.12f, 0.12f, 1f);
            }
        }

        public static string GetTypeName(LogicalColor c)
        {
            switch (c)
            {
                case LogicalColor.White: return "White (0..84)";
                case LogicalColor.Gray: return "Gray (85..169)";
                case LogicalColor.Black: return "Black (170..254)";
                case LogicalColor.Universal: return "Everything (255)";
                default: return "Unknown";
            }
        }
    }
}
