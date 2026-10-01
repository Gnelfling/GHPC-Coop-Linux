using System;
using System.IO;
using GhpcCoop;

class InfantryHealthTests
{
    static void Main()
    {
        foreach (float percent in new[]
        {
            0f,
            1f,
            25f,
            50f,
            100f
        }

        )
            if (Math.Abs(InfantryHealth.ToNative(InfantryHealth.ToWire(percent)) - percent) > .0001f)
                throw new Exception("Native health percentage changed during replication: " + percent);
        if (InfantryHealth.ToNative(1f) != 100f || InfantryHealth.ToWire(100f) != 1f)
            throw new Exception("Healthy soldier must remain at 100 percent");
        if (InfantryHealth.Parse("", 0).Length != 0)
            throw new Exception();
        var values = InfantryHealth.Parse("1,0.5,0", 3);
        if (values[0] != 1 || values[1] != .5f || values[2] != 0)
            throw new Exception();
        foreach (var text in new[]
        {
            "NaN",
            "Infinity",
            "-1",
            "1.1",
            "garbage",
            "0,1"
        }

        )
        {
            bool rejected = false;
            try
            {
                InfantryHealth.Parse(text, 1);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }

            if (!rejected)
                throw new Exception("Accepted invalid damage " + text);
        }

        Console.WriteLine("PASS 14 infantry health validation and native scale cases");
    }
}
