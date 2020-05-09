using System.Collections.Generic;
using Esm;
using Nif;
using UnityEngine;

public abstract class AIRecord : CreatableRecord, IInventoryRecord
{
	[SerializeField]
	protected float scale;

	[SerializeField]
	protected AiData aiData = new AiData();

	[SerializeField]
	protected ActivateData activateData;

	[SerializeField]
	protected EscortData escortData;

	[SerializeField]
	protected FollowData followData;

	[SerializeField]
	protected TravelData travelData;

	[SerializeField]
	protected WanderData wanderData;

	[SerializeField]
	protected List<InventoryItem> items = new List<InventoryItem>();

	[SerializeField]
	protected List<SpellRecord> spells = new List<SpellRecord>();

	public IReadOnlyList<SpellRecord> Spells => spells;

	IEnumerable<InventoryItem> IInventoryRecord.Items => items;

	public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
	{
		var model = this.model ?? "base_anim.nif";

		NiFile niFile;
		if (!niCache.TryGetValue(model, out niFile))
		{
			var reader = BsaFileReader.LoadArchiveFileData($"meshes\\{model}");
			niFile = new NiFile(reader);
			niCache.Add(model, niFile);
		}

		var gameObject = niFile.CreateGameObject(parent);

		if(gameObject == null)
		{
			return null;
		}

		gameObject.name = name;

		CharacterInventory.Create(gameObject, this, referenceData);

		var equipment = gameObject.AddComponent<CharacterEquipment>();

		var animation = gameObject.AddComponent<CharacterAnimation>();

		var combat = gameObject.AddComponent<CharacterCombat>();

		var input = AddCharacterInput(gameObject);

		combat.Initialize(animation, equipment, input);
		input.Initialize(aiData, wanderData, animation, combat);

		var character = gameObject.AddComponent<CharacterMovement>();
		character.Initialize(animation, input);

		return gameObject;
	}

	protected abstract CharacterInput AddCharacterInput(GameObject gameObject);
}