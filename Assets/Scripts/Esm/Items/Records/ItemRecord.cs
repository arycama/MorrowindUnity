using Esm;
using UnityEngine;

public abstract class ItemRecord : CreatableRecord
{
	[SerializeField]
	protected Sprite icon;

	public abstract SoundRecord PickupSound { get; }
	public abstract SoundRecord DropSound { get; }

	public Sprite Icon => icon;

	public virtual void AddToInventory(IInventory inventory, int quantity = 1)
	{
		inventory.AddItem(this, quantity);
	}

	/// <summary>
	/// Called when the item is Activated by a character from within an inventory.
	/// </summary>
	/// <param name="target"></param>
	/// 
	public virtual void UseItem(GameObject target)
	{

	}

	public InfoPanel CreateInfo(Vector3 position, int quantity, bool displayIcon)
	{
		var infoPanel = InfoPanel.Create(new Vector2(0.5f, 0.5f));

		// Display object count if greated than 1
		if(quantity > 1)
		{
			infoPanel.AddTitle($"{fullName} ({quantity})");
		}
		else
		{
			infoPanel.AddTitle(fullName);
		}

		if (displayIcon)
		{
			infoPanel.DisplayIcon(Icon);
		}

		return infoPanel;
	}

	public virtual InfoPanel DisplayInfo(Vector3 position, int quantity, bool displayIcon)
	{
		return CreateInfo(position, quantity, displayIcon);
	}

	protected void CreateSprite(string fileName)
	{
		var texture = BsaFileReader.LoadTexture("icons/" + fileName) as Texture2D;
		var rect = new Rect(0, 0, texture.width, texture.height);
		var pivot = new Vector2(0.5f, 0.5f);
		this.icon = Sprite.Create(texture, rect, pivot);
	}
}

public abstract class ItemRecord<T> : ItemRecord where T : ItemRecordData
{
	[SerializeField]
	protected T data;

	public T Data => data;

	public override InfoPanel DisplayInfo(Vector3 position, int quantity, bool displayIcon)
	{
		var infoPanel = base.DisplayInfo(position, quantity, displayIcon);
		data.DisplayInfo(infoPanel);
		return infoPanel;
	}
}