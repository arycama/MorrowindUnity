using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryPanelUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
	[SerializeField]
	private Image image;

	[SerializeField]
	private Image magicImage;

	[SerializeField]
	private Text text;

	private InfoPanel infoPanel;
	private ItemRecord item;

	private Action onClick;

	public int Quantity { get; private set; }
	public ItemRecord Item => item;

	public void Initialize(ItemRecord item, int quantity, Action unityAction)
	{
		this.item = item;
		Quantity = quantity;

		image.sprite = item.Icon;
		text.text = quantity.ToString();

		onClick = unityAction;

		if(quantity < 2)
		{
			text.gameObject.SetActive(false);
		}
		else
		{
			text.text = quantity.ToString();
		}
	}

	// Transfer to inventory
	void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
	{
		onClick.Invoke();
	}

	void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
	{
		infoPanel = item.CreateInfo(transform.position, Quantity, false);
	}

	void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
	{
		if(infoPanel == null)
		{
			return;
		}

		Destroy(infoPanel.gameObject);
		infoPanel = null;
	}

	private void OnDestroy()
	{
		if (infoPanel == null)
		{
			return;
		}

		Destroy(infoPanel.gameObject);
		infoPanel = null;
	}
}