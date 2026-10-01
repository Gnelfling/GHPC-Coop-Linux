using System;

namespace GhpcCoop
{
    // One failing native object must not prevent the rest of the session teardown.
    public static class SafeCleanup
    {
        public static void Run(string operation, Action cleanup, Action<string> log)
        {
            try { cleanup(); }
            catch (Exception error) { log("Cleanup " + operation + ": " + error); }
        }
    }
}
