using UnityEngine;
using UnityEngine.Pool;

public class StaticRecord : CreatableRecord
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
			}
		}
	}

	public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
	{
		var gameObject = base.CreateGameObject(referenceData, parent);

        var meshRenderers = ListPool<MeshRenderer>.Get();
        gameObject.GetComponentsInChildren(meshRenderers);
        foreach (var meshRenderer in meshRenderers)
        {
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
            meshRenderer.staticShadowCaster = true;
            //childGameObjects[i].gameObject.isStatic = true;
            //CellManager.StaticBatching.Add(childGameObjects[i].gameObject);
        }
        ListPool<MeshRenderer>.Release(meshRenderers);

        return gameObject;
	}
}