using UnityEngine;

namespace Esm
{
	public class IngredientRecord : ItemRecord<IngredientRecordData>
	{
		public override SoundRecord PickupSound => Record.GetRecord<SoundRecord>("Item Ingredient Up");
		public override SoundRecord DropSound => Record.GetRecord<SoundRecord>("Item Ingredient Down");

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
					case SubRecordType.IngredientData:
						data = new IngredientRecordData(reader);
						break;
				}
			}
		}

		public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
		{
			var gameObject = base.CreateGameObject(referenceData, parent);
			var ownerData = new OwnerData(referenceData.Owner, referenceData.Global, referenceData.Faction, referenceData.Rank);

			Ingredient.Create(gameObject, this, referenceData);

			return gameObject;
		}

		public override void UseItem(GameObject target)
		{
			Record.GetRecord<SoundRecord>("Swallow").PlaySound2D();

			// Needs to somehow also remove itself from the inventory.
		}
	}
}