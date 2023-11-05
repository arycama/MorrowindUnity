using Esm;
using UnityEngine;
using UnityEngine.Pool;

public class DoorRecord : CreatableRecord
{
	[SerializeField]
	private SoundRecord openSound;

	[SerializeField]
	private SoundRecord closeSound;

	public SoundRecord OpenSound => openSound;
	public SoundRecord CloseSound => closeSound;

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
				case SubRecordType.SoundName:
					openSound = Record.GetRecord<SoundRecord>(reader.ReadString(size));
					break;
				case SubRecordType.Anam:
					closeSound = Record.GetRecord<SoundRecord>(reader.ReadString(size));
					break;
			}
		}
	}

	public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
	{
		var gameObject = base.CreateGameObject(referenceData, parent);

		// Doors that actually lead somewhere won't move, so it's safe to static batch them
		if(referenceData.DoorExitData != null)
		{
            var meshRenderers = ListPool<MeshRenderer>.Get();
            gameObject.GetComponentsInChildren(meshRenderers);
            foreach (var meshRenderer in meshRenderers)
            {
                meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                //childGameObjects[i].gameObject.isStatic = true;
                //CellManager.StaticBatching.Add(childGameObjects[i].gameObject);
            }
            ListPool<MeshRenderer>.Release(meshRenderers);
        }

		Door.Create(gameObject, this, referenceData);

		return gameObject;
	}
}