namespace Nif
{
	public class StringKey
	{
		public float time;
		public string value;

		public StringKey(System.IO.BinaryReader reader)
		{
			time = reader.ReadSingle();
			value = reader.ReadLengthPrefixedString();
		}
	}
}