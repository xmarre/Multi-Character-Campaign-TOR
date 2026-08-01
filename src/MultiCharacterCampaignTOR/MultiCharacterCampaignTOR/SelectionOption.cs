// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

namespace MultiCharacterCampaignTOR
{
	internal sealed class SelectionOption
	{
		public readonly object Value;

		public readonly string Label;

		public readonly string Hint;

		public readonly bool Enabled;

		public SelectionOption(object value, string label, string hint, bool enabled)
		{
			Value = value;
			Label = label;
			Hint = hint ?? string.Empty;
			Enabled = enabled;
		}
	}
}
