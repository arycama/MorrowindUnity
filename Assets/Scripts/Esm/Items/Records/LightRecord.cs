using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Esm
{
	public class LightRecord : ItemRecord<LightRecordData>
	{
		[SerializeField]
		private SoundRecord sound;

		public override SoundRecord PickupSound => Record.GetRecord<SoundRecord>("Item Misc Up");
		public override SoundRecord DropSound => Record.GetRecord<SoundRecord>("Item Misc Down");

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.Id:
						name = reader.ReadString(size);
						break;
					case SubRecordType.Model:
						model = reader.ReadString(size);
						break;
					case SubRecordType.Name:
						fullName = reader.ReadString(size);
						break;
					case SubRecordType.Script:
						script = Script.Get(reader.ReadString(size));
						break;
					case SubRecordType.ItemTexture:
						CreateSprite(reader.ReadString(size));
						break;
					case SubRecordType.LightData:
						data = new LightRecordData(reader);
						break;
					case SubRecordType.SoundName:
						sound = Record.GetRecord<SoundRecord>(reader.ReadString(size));
						break;
				}
			}
		}

		public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
		{
			var gameObject = base.CreateGameObject(referenceData, parent);

			// Remove the Item component if it can't be picked up
			if (data.Flags.HasFlag(LightFlags.CanCarry))
			{
				LightItem.Create(gameObject, this, referenceData);
				//var ownerData = new OwnerData(referenceData.Owner, referenceData.Global, referenceData.Faction, referenceData.Rank);
				//var component = gameObject.AddComponent<LightItem>();
				//component.Initialize(this, referenceData.Quantity, ownerData, referenceData.Health == -1 ? data.Time : referenceData.Health);
			}

			// Add light component if not negative, as negative lights are not supported in Unity
			if (!data.Flags.HasFlag(LightFlags.Negative))
			{
				AddLightComponent(gameObject);
			}

			// Add sound component if needed
			sound?.AddAudioSource(gameObject, true, true);

			return gameObject;
		}

		private void AddLightComponent(GameObject gameObject)
		{
			// Use the root transform for the light by default
			var attachLight = gameObject.transform;

			// Check all children for a gameobject called "AttachLight", and attach the light component to that if it exists
			var transforms = gameObject.GetComponentsInChildren<Transform>();
			foreach (var transform in transforms)
			{
				if (transform.name == "AttachLight")
				{
					attachLight = transform;
					break;
				}
			}

			var light = attachLight.gameObject.AddComponent<Light>();

			light.color = data.Color;
			light.range = data.Radius;
			light.shadows = LightShadows.Soft;

			// Disable if it's meant to be off by default
			if (data.Flags.HasFlag(LightFlags.OffByDefault))
			{
				light.enabled = false;
			}

			// Ensure that only the shadowbox is used to cast shadows from this light
			var renderers = gameObject.GetComponentsInChildren<Renderer>();
			foreach (var renderer in renderers)
			{
				if (renderer.CompareTag("Shadow"))
				{
					renderer.gameObject.layer = 0;
					renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
				}
				else
				{
					renderer.shadowCastingMode = ShadowCastingMode.Off;
				}
			}

		}
	}
}