# 0.9.9.3 — Steam friend portraits and invitation menu

- Steam profile pictures beside friends, with a placeholder while unavailable or loading.
- Persona status, aligned invitation buttons, clipped long names and an olive/dark GHPC-style panel.
- Only visible rows request portraits; a bounded 64-entry memory cache retries pending images and releases textures on eviction/shutdown. No separate login or web API key is required.

## Updating
Users who installed the 0.9.9.2 setup: close GHPC, open the desktop GHPC Co-op Manager and click Check Updates, or launch via GHPC Co-op / Update and Play. This update changes the DLL and can be delivered automatically. Players should update together.
Older broken launchers must install the full Setup ZIP once; DLL updates do not replace launcher scripts. Direct Steam or GHPC.exe launch does not run the updater.

## Verification
Compilation, 185 protocol/transport tests and 21 fake-Steam adapter tests passed. These do not verify live avatar appearance; in-game visual and remote invitation testing remain pending. Existing gameplay limitations remain. Smoke color/range have not been changed.
Full source and pinned build instructions are supplied. SHA256SUMS.txt covers uploaded payloads. Hashes verify integrity, not independent security certification.
