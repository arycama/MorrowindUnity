using System;
using System.Collections.Generic;
using Esm;
using UnityEngine;
using UnityEngine.Events;

public abstract class CharacterService : MonoBehaviour
{
	protected IDialogController controller;
	protected ListPanelUI listPanelUI;

	// The name of the service displayed in the dialog window
	public abstract string ServiceName { get; }

	public virtual bool IsRefusable => true;

	// Whether to close the service box on purchase
	protected virtual bool CloseOnPurchase => false;

	// Whether to close the entire UI on purchase (I.e. travel)
	protected virtual bool CloseUIOnPurchase => false;

	// Whether to use cost calculations (Ignored for bribes etc)
	protected virtual bool UseCostCalculations => true;

	// The text displayed in the UI window after the title
	protected virtual string Description => null;

	// The text displayed on the button
	protected virtual string ButtonText => "OK";

	// Override this to use a different title in the UI than the service's menu name
	protected virtual string ServiceTitle => ServiceName;

	protected abstract IEnumerable<ServiceOption> GetOptions(Character player, Character npc);

	public void DisplayService(IDialogController controller, Character player, Character npc)
	{
		// Destroy any existing list panel UI, used for rebuilding. Should eventually just update the current instead of destroying and remaking
		if (listPanelUI != null)
			Destroy(listPanelUI.gameObject);

		// Save some values for re-creating the list if needed
		this.controller = controller;

		var options = GetOptions(player, npc);
		var availableGold = player.Inventory.GetItemQuantity(EconomyManager.Gold);

		var uiOptions = new List<ListUIOption>();
		foreach(var option in options)
		{
			int cost = (int)option.Price;
			string description;
			if (UseCostCalculations)
			{
				cost = CalculateCost(option.Price, player, npc, true);
				var variation = cost - option.Price;
				var variationText = variation > 0 ? $"+{variation}" : variation.ToString();
				description = $"{option.Description}  - {cost}gp ({variationText})";
			}
			else
			{
				description = option.Description;
			}

			Action action = () =>
			{
				if (option.Action())
					player.Inventory.RemoveItem(EconomyManager.Gold, cost);
				
				DisplayService(controller, player, npc);
			};

			var isEnabled = availableGold >= option.Price;
			var uiOption = new ListUIOption(description, action, isEnabled);
			uiOptions.Add(uiOption);
		}
		
		listPanelUI = ListPanelUI.Create(uiOptions, ServiceName, Description, ButtonText, availableGold, controller as PauseGameUI, CloseOnPurchase, CloseUIOnPurchase);
	}

	protected int CalculateCost(float basePrice, Character player, Character npc,  bool buying = true)
	{
		var disposition = npc.GetDisposition(player);
		var a = player.GetSkill(CharacterSkill.sSkillMercantile);
		var b = Mathf.Min(10, 0.1f * player.GetAttribute(CharacterAttribute.sAttributeLuck));
		var c = Mathf.Min(10, 0.2f * player.GetAttribute(CharacterAttribute.sAttributePersonality));

		var d = npc.GetSkill(CharacterSkill.sSkillMercantile);
		var e = Mathf.Min(10, 0.1f * npc.GetAttribute(CharacterAttribute.sAttributeLuck));
		var f = Mathf.Min(10, 0.2f * npc.GetAttribute(CharacterAttribute.sAttributePersonality));

		var pcTerm = (disposition - 50 + a + b + c) * player.FatigueTerm;
		var npcTerm = (d + e + f) * npc.FatigueTerm;

		var buyTerm = 1 - 0.005f * (pcTerm - npcTerm);
		var sellTerm = 0.5f - 0.005f * (npcTerm - pcTerm);

		var x = buying ? buyTerm : Mathf.Min(buyTerm, sellTerm);

		int offerPrice;
		if (x < 1) offerPrice = (int)(x * basePrice);
		else offerPrice = (int)basePrice + (int)((x - 1) * basePrice);

		return Mathf.Max(1, offerPrice);
	}
}