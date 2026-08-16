## v1.3.3
- Added TOR career-ability support for AI-controlled registered shared heroes, including correct per-hero career ownership, direct combat charge accounting, AI targeting/casting, and safe AI-to-player controller handoffs.
- Added career-specific TOR AI handling where `CareerAbilityEffect` would otherwise fall back to generic missile behavior, including self/local abilities, Fey Paths, Greater Harbinger, targeted ground abilities, and moving career projectiles.
- Fixed the remaining loader-sensitive Harmony bootstrap path in reconstructed auxiliary MCC components. RuntimeCompatibility now resolves legacy auxiliary `0Harmony` type lookups through the module's linked Harmony assembly before later sidecars initialize.
- Fixed Ctrl+R not opening the shared-character manager on affected Harmony loader paths.
- Fixed `Create a new playable character` failing on the same affected loader path because NativeCreation still used the legacy assembly-qualified Harmony lookup.
- Fixed TOR party-screen career buttons retaining the previous active character's button state after an MCC character switch; the active career button is now rebound and an open party screen is refreshed through TOR's native refresh path.
- Expanded Harmony 2.4 compatibility CI to cover the first-loaded auxiliary Harmony resolver/load-order invariant that v1.3.2 did not test.
- Existing saves remain compatible. No recurring campaign-map scan, global hero/party scan, or new campaign tick work was added.
