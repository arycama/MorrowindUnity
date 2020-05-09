namespace Esm
{
	public class LeveledItemRecordData : ItemRecordData
	{
		public LeveledItemFlags Flags { get; private set; }

		public LeveledItemRecordData(System.IO.BinaryReader reader)
		{
			Flags = (LeveledItemFlags)reader.ReadInt32();
		}
	}
}