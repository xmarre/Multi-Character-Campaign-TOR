// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

namespace MultiCharacterCampaignTOR
{
	internal sealed class BackgroundProfile
	{
		public readonly string Id;

		public readonly string Name;

		public readonly string Description;

		public readonly string Attribute;

		public readonly string[] Skills;

		public BackgroundProfile(string id, string name, string description, string attribute, params string[] skills)
		{
			Id = id;
			Name = name;
			Description = description;
			Attribute = attribute;
			Skills = skills;
		}
	}
}
