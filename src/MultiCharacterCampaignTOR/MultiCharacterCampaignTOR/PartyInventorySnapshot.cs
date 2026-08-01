// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace MultiCharacterCampaignTOR
{
	internal sealed class PartyInventorySnapshot
	{
		private readonly List<object> _elements;

		private readonly string _sourcePartyId;

		private PartyInventorySnapshot(List<object> elements, string sourcePartyId)
		{
			_elements = elements ?? new List<object>();
			_sourcePartyId = sourcePartyId ?? string.Empty;
		}

		public static PartyInventorySnapshot Capture(MobileParty party)
		{
			try
			{
				ItemRoster itemRoster = ResolveItemRoster(party);
				if (itemRoster == null)
				{
					Log.Warning("Main-party inventory snapshot skipped because ItemRoster could not be resolved.");
					return null;
				}
				List<object> list = EnumerateElements(itemRoster);
				Log.Info("Captured main-party inventory before character switch. Party=" + Reflection.IdOf(party) + "; elements=" + list.Count + ".");
				return new PartyInventorySnapshot(list, Reflection.IdOf(party));
			}
			catch (Exception ex)
			{
				Log.Error("Could not capture main-party inventory before character switch", ex);
				return null;
			}
		}

		public void RestoreIfChanged(MobileParty party)
		{
			try
			{
				ItemRoster itemRoster = ResolveItemRoster(party);
				if (itemRoster == null)
				{
					Log.Warning("Main-party inventory restoration skipped because ItemRoster could not be resolved.");
					return;
				}
				List<object> list = EnumerateElements(itemRoster);
				if (Equivalent(_elements, list))
				{
					Log.Info("Main-party inventory remained unchanged during character switch.");
					return;
				}
				MethodInfo method = itemRoster.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
				if (method == null)
				{
					throw new MissingMethodException(itemRoster.GetType().FullName, "Clear()");
				}
				method.Invoke(itemRoster, null);
				MethodInfo methodInfo = null;
				Type elementType = null;
				for (int i = 0; i < _elements.Count; i++)
				{
					object obj = _elements[i];
					if (obj == null)
					{
						continue;
					}
					if (methodInfo == null || elementType != obj.GetType())
					{
						elementType = obj.GetType();
						methodInfo = itemRoster.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "Add" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == elementType);
						if (methodInfo == null)
						{
							throw new MissingMethodException(itemRoster.GetType().FullName, "Add(" + elementType.FullName + ")");
						}
					}
					methodInfo.Invoke(itemRoster, new object[1] { obj });
				}
				MethodInfo method2 = itemRoster.GetType().GetMethod("UpdateVersion", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
				if (method2 != null)
				{
					method2.Invoke(itemRoster, null);
				}
				Log.Warning("Main-party inventory changed during character switching and was restored exactly. This removed native/mod-added duplicate equipment. Party=" + ((_sourcePartyId.Length != 0) ? _sourcePartyId : Reflection.IdOf(party)) + "; beforeElements=" + _elements.Count + "; afterElements=" + list.Count + ".");
			}
			catch (Exception ex)
			{
				Log.Error("Could not restore main-party inventory after character switch", ex);
			}
		}

		private static ItemRoster ResolveItemRoster(MobileParty party)
		{
			if (party == null)
			{
				return null;
			}
			object member = Reflection.GetMember(party, "Party");
			if (Reflection.GetMember(member, "ItemRoster") is ItemRoster result)
			{
				return result;
			}
			return Reflection.GetMember(party, "ItemRoster") as ItemRoster;
		}

		private static List<object> EnumerateElements(ItemRoster roster)
		{
			List<object> list = new List<object>();
			if (!(roster is IEnumerable enumerable))
			{
				throw new InvalidOperationException("Runtime ItemRoster does not implement IEnumerable.");
			}
			foreach (object item in enumerable)
			{
				list.Add(item);
			}
			return list;
		}

		private static bool Equivalent(List<object> left, List<object> right)
		{
			if (object.ReferenceEquals(left, right))
			{
				return true;
			}
			if (left == null || right == null || left.Count != right.Count)
			{
				return false;
			}
			for (int i = 0; i < left.Count; i++)
			{
				object obj = left[i];
				object obj2 = right[i];
				if ((obj != null) ? (!obj.Equals(obj2)) : (obj2 != null))
				{
					return false;
				}
			}
			return true;
		}
	}
}
