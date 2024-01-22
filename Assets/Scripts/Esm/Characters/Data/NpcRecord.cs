using System.Collections.Generic;
using Esm;
using UnityEngine;

public class NpcRecord : AIRecord
{
	[SerializeField] private string cellEscortFollow;
	[SerializeField, EnumFlags]	private NpcFlags npcFlags;
	[SerializeField] private List<string> destinations = new List<string>();
	[SerializeField] private BodyPartRecord hair;
	[SerializeField] private BodyPartRecord head;
	[SerializeField] private ClassRecord classId;
	[SerializeField] private Faction faction;
	[SerializeField] private NpcRecordData npcData;
	[SerializeField] private Race race;
	[SerializeField] private List<DoorExitData> destinationData = new List<DoorExitData>();

	private readonly Rigidbody rb;

	public bool IsFemale => npcFlags.HasFlag(NpcFlags.Female);

	public AiData AiData => aiData;
	public ClassRecord Class => classId;
	public Faction Faction => faction;
	public NpcRecordData NpcSubData => npcData;
	public Race Race => race;
	public Script Script => script;
	
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
				case SubRecordType.RaceName:
					race = Record.GetRecord<Race>(reader.ReadString(size));
					break;

				// Npc's have this even if they are part of no faction, so it needs to be checked if empty first
				case SubRecordType.Anam:
					string fac = reader.ReadString(size);
					faction = string.IsNullOrEmpty(fac) ? null : faction = Record.GetRecord<Faction>(fac);
					break;
				case SubRecordType.BodyName:
					head = BodyPartRecord.Get(reader.ReadString(size));
					break;
				case SubRecordType.CreatureName:
					classId = Record.GetRecord<ClassRecord>(reader.ReadString(size));
					break;
				case SubRecordType.KeyName:
					hair = BodyPartRecord.Get(reader.ReadString(size));
					break;
				case SubRecordType.NpcData:
					npcData = new NpcRecordData(reader, size);
					break;
				case SubRecordType.Flag:
					npcFlags = (NpcFlags)reader.ReadInt32();
					break;
				case SubRecordType.InventoryItem:
					items.Add(new InventoryItem(reader));
					break;
				case SubRecordType.NpcSpell:
					spells.Add(Record.GetRecord<SpellRecord>(reader.ReadString(size)));
					break;
				case SubRecordType.AiData:
					aiData = new AiData(reader);
					break;
				case SubRecordType.AiWanderData:
					wanderData = new WanderData(reader);
					break;
				case SubRecordType.AiTravelData:
					travelData = new TravelData(reader);
					break;
				case SubRecordType.AiFollowData:
					followData = new FollowData(reader);
					break;
				case SubRecordType.AiEscortData:
					escortData = new EscortData(reader);
					break;
				case SubRecordType.ContainerData:
					cellEscortFollow = reader.ReadString(size);
					break;
				case SubRecordType.AiActivateData:
					activateData = new ActivateData(reader);
					break;
				case SubRecordType.DoorData:
					destinationData.Add(DoorExitData.Create(reader));
					break;
				case SubRecordType.DoorName:
					destinations.Add(reader.ReadString(size));
					break;
				case SubRecordType.Scale:
					scale = reader.ReadSingle();
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

		gameObject.AddComponent<CharacterVoice>().Initialize(this);

		var body = gameObject.AddComponent<CharacterBody>();
		body.Initialize(race, head, hair, npcFlags.HasFlag(NpcFlags.Female));

		DialogController.Create(gameObject, this, referenceData);

		var characterData = gameObject.AddComponent<Character>();
		characterData.Initialize(this);

		var animation = gameObject.GetComponent<CharacterAnimation>();
		var inventory = gameObject.GetComponent<IInventory>();

		var equipment = gameObject.GetComponent<CharacterEquipment>();
		equipment.Initialize(animation, body, inventory);

		foreach (var collider in gameObject.GetComponentsInChildren<Collider>())
		{
			collider.gameObject.layer = LayerMask.NameToLayer("Npc");
		}

		AddServices(gameObject);

		CharacterAudio.Create(gameObject);

		return gameObject;
	}

	// Should move this to another class
	private void AddServices(GameObject gameObject)
	{
		var services = aiData.GetServices();

		// Give all Npc's persuasion
		PersuasionService.Create(gameObject);

		foreach (var service in services)
		{
			switch (service)
			{
				case Service.Barter:
					BarterService.Create(gameObject);
					break;
				case Service.Enchanting:
					EnchantingService.Create(gameObject);
					break;
				case Service.Repair:
					RepairService.Create(gameObject);
					break;
				case Service.Spellmaking:
					SpellmakingService.Create(gameObject);
					break;
				case Service.Spells:
					SpellService.Create(gameObject);
					break;
				case Service.Training:
					TrainingService.Create(gameObject);
					break;
			}
		}

		if (destinationData != null && destinationData.Count > 0)
		{
			TravelService.Create(gameObject);
		}
	}

	public IEnumerable<TravelDestination> GetTravelDestinations()
	{
		for (var i = 0; i < destinationData.Count; i++)
		{
			if (destinations.Count > i)
				yield return new TravelDestination(destinationData[i], destinations[i]);
			else
				yield return new TravelDestination(destinationData[i]);
		}
	}

	protected override CharacterInput AddCharacterInput(GameObject gameObject)
	{
		if (name == "player")
		{
			return gameObject.AddComponent<PlayerInput>();
		}
		else
		{
			return gameObject.AddComponent<NpcInput>();
		}
	}
}