using System; using System.Globalization; namespace GhpcCoop {
    public static class InfantryHealth
    {
        public static float[] Parse(string text, int expected)
        {
            if (text == null || expected < 0 || expected > 128) throw new System.IO.InvalidDataException("Invalid infantry health.");
            var fields = text.Length == 0 ? new string[0] : text.Split(',');
            if (fields.Length != expected) throw new System.IO.InvalidDataException("Infantry damage layout differs.");
            var values = new float[expected];
            for (int i = 0; i < expected; i++)
                if (!float.TryParse(fields[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) ||
                    float.IsNaN(values[i]) || float.IsInfinity(values[i]) || values[i] < 0 || values[i] > 1)
                    throw new System.IO.InvalidDataException("Invalid infantry health value.");
            return values;
        }
    }
}



