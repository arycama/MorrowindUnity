using Esm;
using UnityEngine;

public class ActivatorRecord : CreatableRecord
{
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
			}
		}
	}

	public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
	{
		var gameObject = base.CreateGameObject(referenceData, parent);

		//var childGameObjects = gameObject.GetComponentsInChildren<MeshFilter>();
		//var length = childGameObjects.Length;
		//for (var i = 0; i < length; i++)
		//{
			//childGameObjects[i].gameObject.isStatic = true;
			//CellManager.StaticBatching.Add(childGameObjects[i].gameObject);
		//}

		Activator.Create(gameObject, this, referenceData);

		return gameObject;
	}

	public InfoPanel DisplayInfo(Vector3 position, ReferenceData referenceData)
	{
		// Some objects like activators have no description and are just used for scripts etc, so return null for these
		if (string.IsNullOrEmpty(fullName))
		{
			return null;
		}

		var infoPanel = InfoPanel.Create(new Vector2(0.5f, 0.5f));
		infoPanel.AddTitle(fullName);
		return infoPanel;
	}
}