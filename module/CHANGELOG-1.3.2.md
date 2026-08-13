## v1.3.2
- Fixed a new-campaign/session-launch crash reported on Bannerlord 1.3.15 with Bannerlord.Harmony / Lib.Harmony 2.4.2.x.
- Replaced brittle `Type.GetType("HarmonyLib.Harmony, 0Harmony")` / `HarmonyMethod` bootstrap lookups with the already-linked Harmony types, so MCC's core Harmony compatibility patches install on the affected runtime path.
- Made the auxiliary TOR active-equipment refresh hook fail safely when its reflected TOR target or prefix surface is unavailable, preventing optional compatibility setup from aborting campaign startup.
- Added a dedicated full-solution Harmony 2.4.2 compatibility build while retaining the existing Harmony 2.3.3 build/API validation and runtime patch-installation smoke coverage.
- No save data, character switching, party state, finance, career-selection, battle-intervention, reinforcement, or campaign-map behavior was changed.
