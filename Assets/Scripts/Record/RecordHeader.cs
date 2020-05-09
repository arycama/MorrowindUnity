public class RecordHeader
{
	public int DataOffset { get; }
	public int DataSize { get; }
	public bool IsDeleted { get; }
	public RecordFlags Flags { get; }
	public RecordType Type { get; }

	public RecordHeader(System.IO.BinaryReader reader)
	{
		Type = (RecordType)reader.ReadInt32();
		DataSize = reader.ReadInt32();
		IsDeleted = reader.ReadInt32() != 0;
		Flags = (RecordFlags)reader.ReadInt32();
		DataOffset = (int)reader.BaseStream.Position;
	}

	public int DataEndPos => DataOffset + DataSize - 4;

	public override string ToString()
	{
		return $"Type: {Type}, Size: {DataSize}, Deleted: {IsDeleted}, Flags: {Flags}, RecordType: {Flags}";
	}
}