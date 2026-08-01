## 1.1.1

- Fixed remote shared-character party leaders receiving the active player's complete shared denar wallet. Bannerlord treated that mirrored balance as party surplus and added 20% above 10,000 denars to `Caravan and Party Income`, producing the reported 234,196-denar phantom income from a 1,180,981-denar wallet.
- Remote shared-character parties now retain independent treasuries across shared-player-gold synchronization. Clearly mirrored v1.1.0 balances are normalized to a 10,000-denar party reserve on first synchronization.
- Added a narrowly scoped fallback that blocks only a still-mirrored shared wallet from native party-income withdrawal. Legitimate party profits, caravans, workshops, mercenary contract pay, wages, and garrison expenses remain native.
- Added event-driven alerts when a registered shared-character party joins an already-running AI battle through `OnPartyAddedToMapEvent`.
- Battle intervention now accepts registered shared characters on either map-event side instead of only the original defender.
- Permits takeover of a helper party temporarily attached to an AI battle-side leader after the exact map event and all existing current-party, ownership, settlement, transition, siege, and encounter invariants are revalidated.
- Added no recurring party scan, global hero scan, or new serialized save key.
- Denars already credited by the v1.1.0 finance bug are not removed automatically because they cannot be distinguished safely from later legitimate earnings and spending.
