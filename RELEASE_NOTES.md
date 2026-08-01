# Multi-Character Campaign - TOR v1.1.1

Released: 1 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

Validated source merge: `8bacb5dd9f3c234a5449d87ef228664d0178e1ce`.

## Fixed: excessive Caravan and Party Income

- Fixed remote shared-character parties receiving a copy of the active player's complete shared denar balance.
- Bannerlord interpreted that copied wallet as party surplus and transferred 20% of everything above its 10,000-denar reserve into `Caravan and Party Income`. With 1,180,981 denars, this produced the reported 234,196-denar phantom income.
- Remote shared-character party leaders now retain an independent party treasury while the currently controlled character continues using the shared player wallet.
- Existing v1.1.0 saves with a clearly mirrored remote-party wallet are normalized to a 10,000-denar party reserve on the first synchronization after loading.
- A second narrowly scoped finance guard prevents Bannerlord from collecting a still-mirrored shared wallet before that normalization occurs.
- Normal mercenary contract pay, workshop income, caravan income, legitimate party profits, wages, and garrison expenses remain native.

The mod does not remove denars already credited by the bug. Those funds cannot be distinguished safely from money earned or spent after the daily calculation.

## Fixed: shared characters joining existing battles

- Added an event-driven hook for `OnPartyAddedToMapEvent`, covering shared-character parties that reinforce an AI battle after it has already started.
- Battle intervention now recognizes a registered shared character on either the attacker or defender side.
- A helper party temporarily attached to an AI battle-side leader can become the player MainParty for the exact active battle after the existing ownership, encounter, settlement, transition, siege, and current-party safety checks pass.
- The existing intervention inquiry can then transfer control and reopen the same native `PlayerEncounter`.
- Stale or unrelated battles remain blocked.

## Performance and compatibility

- No recurring campaign-party scan, global hero scan, or save migration behavior was added.
- Finance repair runs only at the existing shared-gold synchronization and native party-income boundaries.
- Late battle detection runs only when Bannerlord adds a party to a map event.
- No new serialized MCC save keys were added.

## Validation

- Complete six-project Release build against Bannerlord 1.3.15 reference assemblies.
- CI validates `DefaultClanFinanceModel.AddIncomeFromParty`, `CampaignEventDispatcher.OnPartyAddedToMapEvent`, map-event lifecycle methods, and the native `PlayerEncounter` continuation methods.
- The live takeover path cannot be executed inside a running Bannerlord campaign in CI. Keep a backup save and use a new save slot for the first test.
