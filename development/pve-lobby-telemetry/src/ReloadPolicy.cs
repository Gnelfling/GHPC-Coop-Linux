namespace GhpcCoop
{
    public static class ReloadPolicy
    {
        // Co-op mechanical loaders cycle automatically; forced-manual weapons remain exempt.
        public static bool PlayerAutomatic(int doctrine, int preference, bool switchedOn, bool carousel)
        {
            return carousel && doctrine != 3 || Automatic(doctrine, preference, switchedOn);
        }
        // Values match the supported game's DoctrinalReloadMode and player setting.
        public static bool Automatic(int doctrine, int preference, bool switchedOn)
        {
            if (doctrine == 4)
                return true;
            if (doctrine == 3)
                return false;
            if (preference == 1)
                return true;
            if (preference == 2)
                return false;
            if (doctrine == 0)
                return true;
            if (doctrine == 1)
                return false;
            if (doctrine == 2)
                return switchedOn;
            return true;
        }
    }
}
