using System;
using System.Collections.Generic;
using System.IO;
using Esm;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Pool;

namespace Esm
{
	public class BodyPartRecord : EsmRecordCollection<BodyPartRecord>
	{
		// Eventually try auto-populating this from Race records
		public static Dictionary<Race, RaceBody> RaceBodyParts = new Dictionary<Race, RaceBody>();

		[SerializeField]
		public string Model;

		[SerializeField]
		private Race race;

		[SerializeField]
		private BodyPartData data;

		public static GameObject Create(BodyPartRecord record, ReferenceData data, Transform parent = null)
		{
			var reader = BsaFileReader.LoadArchiveFileData($"meshes\\{record.Model}");
			var gameObject = new Nif.NiFile(reader).CreateGameObject(parent);

			using (var scope = ListPool<Renderer>.Get(out var renderers))
			{
				gameObject.GetComponentsInChildren(renderers);
				foreach (var skinnedMeshRenderer in renderers)
					skinnedMeshRenderer.rayTracingMode = RayTracingMode.Off;
			}

			return gameObject;
		}

		// Used to create Npc bodies
		public static BodyPartRecord GetBodyPart(Race race, BodyPartPiece bodyPartType, bool isFemale) => RaceBodyParts[race].GetPart(bodyPartType, isFemale);

		public override void Initialize(BinaryReader reader, RecordHeader header)
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
						Model = reader.ReadString(size);
						break;
					case SubRecordType.Name:
						race = Record.GetRecord<Race>(reader.ReadString(size));
						break;
					case SubRecordType.BodyData:
						data = new BodyPartData(reader);
						break;
				}
			}

			data.SetBodyPart(race, this);
		}
	}
}