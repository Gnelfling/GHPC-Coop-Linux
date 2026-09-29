using System;
using System.Globalization;

namespace GhpcCoop
{
    public static class InfantryHealth
    {
        // Native GHPC health uses percentages (0..100); the wire uses fractions.
        public static float ToWire(float percent)
        {
            return Math.Max(0f, Math.Min(1f, percent / 100f));
        }

        public static float ToNative(float fraction)
        {
            if (float.IsNaN(fraction) || float.IsInfinity(fraction) || fraction < 0 || fraction > 1)
                throw new System.IO.InvalidDataException("Invalid normalized health.");
            return fraction * 100f;
        }

        public static float[] Parse(string text, int expectedComponentCount)
        {
            if (text == null || expectedComponentCount < 0 || expectedComponentCount > 128)
                throw new System.IO.InvalidDataException("Invalid infantry health.");
            var encodedValues = text.Length == 0 ? new string[0] : text.Split(',');
            if (encodedValues.Length != expectedComponentCount)
                throw new System.IO.InvalidDataException("Infantry damage layout differs.");
            var healthValues = new float[expectedComponentCount];
            for (int i = 0; i < expectedComponentCount; i++)
                if (!float.TryParse(encodedValues[i], NumberStyles.Float, CultureInfo.InvariantCulture, out healthValues[i]) ||
                    float.IsNaN(healthValues[i]) ||
                    float.IsInfinity(healthValues[i]) ||
                    healthValues[i] < 0 ||
                    healthValues[i] > 1)
                    throw new System.IO.InvalidDataException("Invalid infantry health value.");
            return healthValues;
        }
    }
}
