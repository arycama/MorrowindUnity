#pragma warning disable 0108

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Esm;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlayerInput : CharacterInput
{
	private bool isIdle;
	private InventoryUI inventoryUI;

	private void Update()
	{
		// Set some animation parameters to 0
		animation.SetParameter("Vertical", 0f);
		animation.SetParameter("Horizontal", 0f);
		animation.SetParameter("Walk", false);

		if (Time.timeScale == 0)
		{
			return;
		}

        // Set attack strength to none when it is frst pressed
		Attack = Input.GetMouseButton(0);
		animation.SetParameter("Attack", Attack);
		//if (Input.GetMouseButtonDown(0))
		//{
		//	// set attack strength to none here
		//	animation.SetParameter("AttackStrength", AttackStrength.None);	
		//}
		

		//// When the button is pressed, set attack strength to none to start
		//if (Input.GetMouseButtonDown(0))
		//{
		//	animation.SetParameter("AttackStrength", AttackStrength.None);
		//}

		// Add for now, tidy later
		//if (Input.GetMouseButtonUp(0))
		//{
		//	animation.SetParameter("AttackSpeed", 1f);
		//}

		// Averaging?
		Yaw = (Yaw + Input.GetAxis("Mouse X")) * 0.5f;
		animation.SetParameter("Yaw", Yaw);

		// Equip
		Equip = Input.GetKeyDown(KeyCode.R);
		animation.SetParameter("Equip", Equip);

		// Jump
		Jump = Input.GetButtonDown("Jump");
		animation.SetParameter("Jump", Jump);

		Forward = Input.GetAxis("Vertical") > 0;
		animation.SetParameter("Forward", Forward);
		if (Forward)
		{
			animation.SetParameter("Walk", true);
			animation.SetParameter("Vertical", 1f);
		}

		Left = Input.GetAxis("Horizontal") < 0;
		animation.SetParameter("Left", Left);
		if (Left)
		{
			animation.SetParameter("Walk", true);
			animation.SetParameter("Horizontal", -1f);
		}

		Back = Input.GetAxis("Vertical") < 0;
		animation.SetParameter("Back", Back);
		if (Back)
		{
			animation.SetParameter("Walk", true);
			animation.SetParameter("Vertical", -1f);
		}

		Right = Input.GetAxis("Horizontal") > 0;
		animation.SetParameter("Right", Right);
		if (Right)
		{
			animation.SetParameter("Walk", true);
			animation.SetParameter("Horizontal", 1f);
		}

		GetTarget();

		if (Input.GetButtonDown("Fire1"))
		{
			currentActivator?.Activate(gameObject);
		}

		if (Input.GetKeyDown(KeyCode.I))
		{
			OpenInventory();
		}

		// Unlocking doors
		if (Input.GetButtonDown("Fire3"))
		{
			var lockable = currentActivator as ILockable;
			if (lockable != null)
			{
				lockable.Unlock(100);
			}
		}

		Sneak = Input.GetKey(KeyCode.LeftControl);
		animation.SetParameter("Sneak", Sneak);

		if (!Forward && !Left && !Right && !Back)
		{
			if(!isIdle)
			{
				animation.SetParameter("Idle", Random.Range(2, 10));
				isIdle = true;

				Run = false;
				animation.SetParameter("Run", false);
			}
		}
		else
		{
			animation.SetParameter("Idle", 0);
			isIdle = false;

			// Only set run if moving
			Run = Input.GetAxis("Vertical") > 0.5f;
			animation.SetParameter("Run", Run);
		}

		if (Input.GetKeyDown(KeyCode.C))
		{
			if (characterUI == null)
			{
				var character = GetComponent<Character>();

				UITitleInfoPair[] info = { new UITitleInfoPair("Level", character.Level.ToString()), new UITitleInfoPair("Race", character.Race.Name), new UITitleInfoPair("Class", character.Class.Name) };
				UITitleInfoPair[] attributes =
				{
					new UITitleInfoPair("Strength", character.GetAttribute(CharacterAttribute.sAttributeStrength).ToString()),
					new UITitleInfoPair("Intelligence", character.GetAttribute(CharacterAttribute.sAttributeIntelligence).ToString()),
					new UITitleInfoPair("Willpower", character.GetAttribute(CharacterAttribute.sAttributeWillpower).ToString()),
					new UITitleInfoPair("Agility", character.GetAttribute(CharacterAttribute.sAttributeAgility).ToString()),
					new UITitleInfoPair("Speed", character.GetAttribute(CharacterAttribute.sAttributeSpeed).ToString()),
					new UITitleInfoPair("Endurance", character.GetAttribute(CharacterAttribute.sAttributeEndurance).ToString()),
					new UITitleInfoPair("Personality", character.GetAttribute(CharacterAttribute.sAttributePersonality).ToString()),
					new UITitleInfoPair("Luck", character.GetAttribute(CharacterAttribute.sAttributeLuck).ToString())
				};

				var skills = new UITitleInfoPair[character.Skills.Count];
				for(var i = 0; i < skills.Length; i++)
				{
					skills[i] = new UITitleInfoPair(((CharacterSkill)i).ToString(), character.Skills[i].ToString());
				}

				characterUI = CharacterMenuUI.Create(name, info, attributes, skills);
			}
			else
			{
				Destroy(characterUI.gameObject);
			}
		}
	}

	CharacterMenuUI characterUI;

	private void GetTarget()
	{
		var ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f));
		RaycastHit hit;

		// Check for object in front of the middle of the screen
		if (Physics.Raycast(ray, out hit, GameSetting.Get("iMaxActivateDist").IntValue))
		{
			// Return if the same collider was hit as the last Raycast
			if (targetCollider == hit.collider)
			{
				return;
			}

			// Save the detected collider as the current target, so it can be compared against next Raycast
			targetCollider = hit.collider;

			// Check for an IActivatable in the object
			var targetActivator = targetCollider.GetComponentInParent<IActivatable>();
			if (targetActivator != null)
			{
				if (targetActivator != currentActivator)
				{
					currentActivator?.CloseInfo();
					currentActivator = targetActivator;
					currentActivator.DisplayInfo();
				}

				return;
			}
		}
		else
		{
			targetCollider = null;
		}

		// If we hit nothing, hide any current activator infos
		if (currentActivator != null)
		{
			currentActivator.CloseInfo();
			currentActivator = null;
		}
	}

	// Should probably move into player input
	private void OpenInventory()
	{
		if (inventoryUI == null)
		{
			inventoryUI = InventoryUI.Create(gameObject, GetComponent<IInventory>(), name);
		}
		else
		{
			Destroy(inventoryUI);
			inventoryUI = null;
		}
	}
}