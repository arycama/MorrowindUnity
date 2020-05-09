namespace Esm
{
	public class ApparelRecordPiece
	{
		public BipedPart Index { get; set; }
		public BodyPartRecord MalePart { get; set; }
		public BodyPartRecord FemalePart { get; set; }

		public ApparelRecordPiece() { }

		public ApparelRecordPiece(BipedPart index, string malePart, string femalePart)
		{
			Index = index;

			if(malePart != null)
			{
				MalePart = BodyPartRecord.Get(malePart);
			}
			
			if(femalePart != null)
			{
				FemalePart = BodyPartRecord.Get(femalePart);
			}
		}
	}
}