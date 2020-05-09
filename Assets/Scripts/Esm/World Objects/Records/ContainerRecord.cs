using System.Collections.Generic;
using System.IO;
using Esm;
using UnityEngine;

public class ContainerRecord : CreatableRecord, IInventoryRecord
{
	[SerializeField]
	private ContainerRecordData data;

	[SerializeField, EnumFlags]
	private ContainerFlags flags;

	[SerializeField]
	private List<InventoryItem> items = new List<InventoryItem>();

	public ContainerRecordData Data => data;
	public ContainerFlags Flags => flags;
	public IEnumerable<InventoryItem> Items => items;

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
					model = reader.ReadString(size);
					break;
				case SubRecordType.Name:
					fullName = reader.ReadString(size);
					break;
				case SubRecordType.Script:
					script = Script.Get(reader.ReadString(size));
					break;
				case SubRecordType.ContainerData:
					data = new ContainerRecordData(reader);
					break;
				case SubRecordType.Flag:
					flags = (ContainerFlags)reader.ReadInt32();
					break;
				case SubRecordType.InventoryItem:
					items.Add(new InventoryItem(reader));
					break;
			}
		}
	}

	public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
	{
		var gameObject = base.CreateGameObject(referenceData, parent);

		var childGameObjects = gameObject.GetComponentsInChildren<MeshFilter>();
		var length = childGameObjects.Length;
		for (var i = 0; i < length; i++)
		{
			childGameObjects[i].gameObject.isStatic = true;
			CellManager.StaticBatching.Add(childGameObjects[i].gameObject);
		}

		var lockData = new LockData(referenceData.LockLevel, referenceData.Trap, referenceData.Key);

		Container.Create(gameObject, this, referenceData);

		return gameObject;
	}
}