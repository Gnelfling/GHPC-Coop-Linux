using System;
using GhpcCoop;

class ReloadPolicyTests
{
    static void Main()
    {
        int count = 0;
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
