#pragma warning disable 0108

using Esm;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlayerInput : CharacterInput
{
	private bool isIdle;
	private InventoryUI inventoryUI;
	private CharacterMenuUI characterUI;

	private void Update()
	{
		// Set some animation parameters to 0
		animation.Parameters.SetFloatParameter("Vertical", 0f);
		animation.Parameters.SetFloatParameter("Horizontal", 0f);
		animation.Parameters.SetBoolParameter("Walk", false);

		if (Time.timeScale == 0)
		{
			return;
		}

		// Set attack strength to none when it is frst pressed
		Attack = Input.GetMouseButton(0);
		animation.Parameters.SetBoolParameter("Attack", Attack);
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
		animation.Parameters.SetFloatParameter("Yaw", Yaw);

		// Equip
		Equip = Input.GetKeyDown(KeyCode.R);
		animation.Parameters.SetBoolParameter("Equip", Equip);

		// Jump
		Jump = Input.GetButtonDown("Jump");
		animation.Parameters.SetBoolParameter("Jump", Jump);

		Forward = Input.GetAxis("Vertical") > 0;
		animation.Parameters.SetBoolParameter("Forward", Forward);
		if (Forward)
		{
			animation.Parameters.SetBoolParameter("Walk", true);
			animation.Parameters.SetFloatParameter("Vertical", 1f);
		}

		Left = Input.GetAxis("Horizontal") < 0;
		animation.Parameters.SetBoolParameter("Left", Left);
		if (Left)
		{
			animation.Parameters.SetBoolParameter("Walk", true);
			animation.Parameters.SetFloatParameter("Horizontal", -1f);
		}

		Back = Input.GetAxis("Vertical") < 0;
		animation.Parameters.SetBoolParameter("Back", Back);
		if (Back)
		{
			animation.Parameters.SetBoolParameter("Walk", true);
			animation.Parameters.SetFloatParameter("Vertical", -1f);
		}

		Right = Input.GetAxis("Horizontal") > 0;
		animation.Parameters.SetBoolParameter("Right", Right);
		if (Right)
		{
			animation.Parameters.SetBoolParameter("Walk", true);
			animation.Parameters.SetFloatParameter("Horizontal", 1f);
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
				_ = lockable.Unlock(100);
			}
		}

		Sneak = Input.GetKey(KeyCode.LeftControl);
		animation.Parameters.SetBoolParameter("Sneak", Sneak);

		if (!Forward && !Left && !Right && !Back)
		{
			if (!isIdle)
			{
				animation.Parameters.SetIntParameter("Idle", Random.Range(2, 10));
				isIdle = true;

				Run = false;
				animation.Parameters.SetBoolParameter("Run", false);
			}
		}
		else
		{
			animation.Parameters.SetIntParameter("Idle", 0);
			isIdle = false;

			// Only set run if moving
			Run = Input.GetAxis("Vertical") > 0.5f;
			animation.Parameters.SetBoolParameter("Run", Run);
		}

		if (Input.GetKeyDown(KeyCode.C))
		{
			if (characterUI == null)
			{
				var character = GetComponent<Character>();

				UITitleInfoPair[] info = { new("Level", character.Level.ToString()), new("Race", character.Race.Name), new("Class", character.Class.Name) };
				UITitleInfoPair[] attributes =
				{
					new("Strength", character.GetAttribute(CharacterAttribute.sAttributeStrength).ToString()),
					new("Intelligence", character.GetAttribute(CharacterAttribute.sAttributeIntelligence).ToString()),
					new("Willpower", character.GetAttribute(CharacterAttribute.sAttributeWillpower).ToString()),
					new("Agility", character.GetAttribute(CharacterAttribute.sAttributeAgility).ToString()),
					new("Speed", character.GetAttribute(CharacterAttribute.sAttributeSpeed).ToString()),
					new("Endurance", character.GetAttribute(CharacterAttribute.sAttributeEndurance).ToString()),
					new("Personality", character.GetAttribute(CharacterAttribute.sAttributePersonality).ToString()),
					new("Luck", character.GetAttribute(CharacterAttribute.sAttributeLuck).ToString())
				};

				var skills = new UITitleInfoPair[character.Skills.Count];
				for (var i = 0; i < skills.Length; i++)
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
			inventoryUI.GetComponent<Canvas>().worldCamera = Camera.main;
		}
		else
		{
			Destroy(inventoryUI);
			inventoryUI = null;
		}
	}
}