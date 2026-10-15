# Multi-Character Campaign - TOR v1.3.9

Released: 15 October 2026.

Target: Mount & Blade II: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

## AI Grey Lord Mind Control crash protection

The AI career-ability compatibility layer permits registered playable heroes to continue using TOR career abilities while controlled by the AI. In TOR's `MindControlScript`, the cast script caches its caster separately from `AbilityScript.CasterAgent` and may reach `OnAfterTick` when that cached caster is absent or has been invalidated. TOR's own career-script cancellation calls `Stop()` but does not prevent the current `OnAfterTick` body from continuing into the target selection path.

This release adds a narrowly scoped Harmony prefix at `MindControlScript.OnAfterTick`:

- A registered MCC hero's AI Mind Control effect first validates the authoritative caster, active agent state, and team.
- On a valid cast, the script's cached caster reference is restored to the authoritative `CasterAgent`, allowing TOR's original targeting and career effects to execute unchanged.
- If the caster is missing, no longer active, or has no team, the effect is stopped and this single script tick is skipped before unsafe targeting.
- Other TOR hero abilities and non-MCC Mind Control casts retain the original implementation. The patch does not generally swallow TOR exceptions.

This prevents the identified invalid-caster continuation path reported with an AI-controlled Grey Lord after switching the active player to a different character. It is not evidence that other independent native crashes or hangs have been resolved.

## Compatibility and installation

- No save migration is required; existing registered heroes, career progression, party state, and shared assets are preserved.
- The Bannerlord module ID remains `TORSharedCharacterCampaign`; the installation folder remains `Modules/MultiCharacterCampaignTOR`.
- Exit Bannerlord, remove the existing `Modules/MultiCharacterCampaignTOR` directory, and install the release archive. Avoid having the legacy `Modules/TORSharedCharacterCampaign` directory installed alongside it.
- The release retains v1.3.8's TOR missile transition and mixed-race NativeCreation repairs and earlier career/controller compatibility changes.

## Verification

The release is built from the maintained six-project source against Bannerlord 1.3.15 reference assemblies. CI includes full-solution builds against Harmony 2.3.3 and 2.4.2, runtime installation smoke, and existing career, WizardAI, character-creation, missile transition, battle/party, and module packaging regression checks.

The new synthetic runtime test invokes the actual Mind Control Harmony prefix and verifies five lifecycle decisions, unchanged behavior for unregistered TOR owners, cancellation when a registered caster is unusable, and correction of the private caster cache.

These tests establish the patched runtime behavior and compile compatibility, not a real-time battle simulation. In-game validation in a large siege with the affected Grey Lord and TOR build remains important. The other reported freeze and native access violation have no confirmed common root cause with the Mind Control exception.
