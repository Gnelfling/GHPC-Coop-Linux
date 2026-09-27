namespace GhpcCoop
{
    public static class ReloadPolicy
    {
        // Values match the supported game's DoctrinalReloadMode and player setting.
        public static bool Automatic(int doctrine, int preference, bool switchedOn)
        {
            if (doctrine == 4) return true;
            if (doctrine == 3) return false;
            if (preference == 1) return true;
            if (preference == 2) return false;
            if (doctrine == 0) return true;
            if (doctrine == 1) return false;
            if (doctrine == 2) return switchedOn;
            return true;
        }
    }
}
