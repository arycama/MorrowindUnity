using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Esm;
using UnityEngine;

[Serializable]
public class ReferenceData
{
	[SerializeField]
	private CreatableRecord target;

	[SerializeField]
	private bool referenceBlocked;

	[SerializeField]
	private float charge = -1;

	[SerializeField]
	private float scale = 1;

	[SerializeField]
	private int health = -1;

	[SerializeField]
	private int lockLevel;

	[SerializeField]
	private int nam0; // Should be in cell record. It's the number of references in the cell. Though it appears more than once for persistent records

	[SerializeField]
	private int quantity = 1;

	[SerializeField]
	private int rank;

	[SerializeField]
	private string loadCell; // Should be CellRecord reference once cells can be referenced without loading their contents

	[SerializeField]
	private SpellRecord trap;

	[SerializeField]
	private CreatureRecord soul;

	[SerializeField]
	private DoorExitData doorData;

	[SerializeField]
	private Faction faction;

	[SerializeField]
	private Global global;

	[SerializeField]
	private MiscItemRecord key;

	[SerializeField]
	private NpcRecord owner;

	[SerializeField]
	private TransformData transformData;

	public bool ReferenceBlocked => referenceBlocked;

	public float Charge => charge;
	public float Scale => scale;

	public int Health => health;
	public int LockLevel => lockLevel;
	public int Quantity => quantity;
	public int Rank => rank;

	public string LoadCell => loadCell;
	public CreatableRecord ObjectId => target;// Encoding.ASCII.GetString(objectIdBytes);

	public CreatureRecord Soul => soul;
	public DoorExitData DoorExitData => doorData;
	public Faction Faction => faction;
	public Global Global => global;
	public MiscItemRecord Key => key;
	public NpcRecord Owner => owner;
	public SpellRecord Trap => trap;
	public TransformData TransformData => transformData;

	public ReferenceData(System.IO.BinaryReader reader)
	{
		var index = reader.ReadInt32();

		while (true)
		{
			var type = (SubRecordType)reader.ReadInt32();
			var size = reader.ReadInt32();

			switch (type)
			{
				case SubRecordType.Id:
					target = Record.GetRecord<CreatableRecord>(reader.ReadString(size));
					break;
				case SubRecordType.Scale:
					scale = reader.ReadSingle();
					break;
				case SubRecordType.DoorData:
					doorData = DoorExitData.Create(reader);
					break;
				case SubRecordType.DoorName:
					loadCell = reader.ReadString(size);
					break;
				case SubRecordType.FloatValue:
					lockLevel = reader.ReadInt32();
					break;
				case SubRecordType.KeyName:
					key = Record.GetRecord<MiscItemRecord>(reader.ReadString(size));
					break;
				case SubRecordType.Trapname:
					trap = Record.GetRecord<SpellRecord>(reader.ReadString(size));
					break;
				case SubRecordType.ReferenceBlocked:
					referenceBlocked = reader.ReadByte() != 0;
					break;
				case SubRecordType.OwnerName:
					owner = Record.GetRecord<NpcRecord>(reader.ReadString(size));
					break;
				case SubRecordType.BodyName:
					global = Record.GetRecord<Global>(reader.ReadString(size));
					break;
				case SubRecordType.IntValue:
					health = reader.ReadInt32(); // Health remaining? Also charge for lights
					break;
				case SubRecordType.Name9:
					quantity = reader.ReadInt32(); //0, 1, 5, 10, 25, 100. Definitely value override  or object count
					break;
				case SubRecordType.SoulName:
					soul = Record.GetRecord<CreatureRecord>(reader.ReadString(size));
					break;
				case SubRecordType.Data:
					transformData = new TransformData(reader);
					break;
				case SubRecordType.CreatureName:
					faction = Record.GetRecord<Faction>(reader.ReadString(size));
					break;
				case SubRecordType.Index:
					rank = reader.ReadInt32();
					break;
				case SubRecordType.SoulCharge:
					charge = reader.ReadSingle();
					break;
				default:
					reader.BaseStream.Position -= 8;
					return;
			}
		}
	}
}