using System;
using GhpcCoop;

class ReloadPolicyTests
{
    static void Main()
    {
        int count = 0;
        if (!ReloadPolicy.PlayerAutomatic(2, 0, false, true))
            throw new Exception("Mechanical loader must cycle with default switch off");
        if (!ReloadPolicy.PlayerAutomatic(2, 2, false, true))
            throw new Exception("Mechanical loader must not inherit manual crew preference");
        if (ReloadPolicy.PlayerAutomatic(3, 1, true, true))
            throw new Exception("Forced-manual feed must remain manual");
        if (ReloadPolicy.PlayerAutomatic(1, 2, false, false))
            throw new Exception("Human loader manual preference changed");
        count += 4;
        for (int doctrine = 0; doctrine <= 4; doctrine++)
            for (int preference = 0; preference <= 2; preference++)
                foreach (bool switched in new[]
                {
                    false,
                    true
                }

                )
                {
                    bool expected = doctrine == 4 ||
                        (doctrine != 3 &&
                        (preference == 1 ||
                        (preference != 2 &&
                        (doctrine == 0 ||
                        doctrine == 2 &&
                        switched))));
                    if (ReloadPolicy.Automatic(doctrine, preference, switched) != expected)
                        throw new Exception("Reload rule mismatch");
                    count++;
                }

        Console.WriteLine("PASS " + count + " native reload policy combinations");
    }
}
