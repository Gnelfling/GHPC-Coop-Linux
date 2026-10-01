using System;
using GhpcCoop;
class SafeCleanupTests
{
    static void Main()
    {
        int completed = 0, failures = 0;
        SafeCleanup.Run("first", () => { throw new InvalidOperationException("destroyed object"); }, text => failures++);
        SafeCleanup.Run("second", () => completed++, text => failures++);
        if (failures != 1 || completed != 1) throw new Exception("cleanup did not continue");
        SafeCleanup.Run("repeat", () => completed++, text => failures++);
        if (completed != 2 || failures != 1) throw new Exception("subsequent cleanup failed");
        Console.WriteLine("PASS cleanup continues after a native-object failure");
    }
}
