#pragma warning disable 0108

using System;
using UnityEngine;

public enum AttackStrength
{
	None,
	Small,
	Medium,
	Large
};

public class CharacterCombat : MonoBehaviour
{
	[SerializeField]
	private float maxHealth = 100;

	private float currentHealth;

    private AttackStrength attackStrength;
	private CharacterAnimation animation;
	private CharacterEquipment equipment;
	private CharacterInput input;

	public Action<GameObject> OnHitEvent;

	public void Initialize(CharacterAnimation animation, CharacterEquipment equipment, CharacterInput input)
	{
		this.animation = animation;
		this.equipment = equipment;
		this.input = input;

		currentHealth = maxHealth;
	}

    // Possibly move to a CharacterCombat component which would calculate damage etc.
    public void MinHit()
    {
        // Set the attack strength back to it's default value
        //attackStrength = AttackStrength.None;
       // animation.SetParameter("AttackStrength", attackStrength);

		if(equipment == null)
		{
			return;
		}


		string weaponSound = null;
		var weaponType = equipment.GetWeaponType();
		switch (weaponType)
		{
			case WeaponType.MarksmanBow:
				weaponSound = null;
				break;
			case WeaponType.MarksmanCrossbow:
				weaponSound = null;
				break;
			default:
				weaponSound = "Weapon Swish";
				break;
		}

		if (weaponSound != null)
		{
			Record.GetRecord<SoundRecord>(weaponSound).PlaySoundAtPoint(transform.position);
		}
	}

	// Possibly move to a CharacterCombat component which would calculate damage etc.
	public void Hit()
	{
		// Raycast to see if there is a target
		RaycastHit hit;
		var ray = new Ray(transform.position + Vector3.up * 128, transform.forward);
		if (Physics.Raycast(ray, out hit, 256f))
		{
			var characterCombat = hit.transform.GetComponentInParent<CharacterCombat>();
			if (characterCombat != null)
			{
				characterCombat.OnHit(25, gameObject);
			}
		}
	}

	// Called when an attack has reached the minimum windup point
	public void MinAttack()
	{
        // Only set the animation parameter if the input key has been released, otherwise the character may be charging up a more powerful attack
        if (!input.Attack)
        {
            attackStrength = AttackStrength.Small;
            animation.SetParameter("AttackStrength", AttackStrength.Small);
        }
		else
		{
			attackStrength = AttackStrength.Medium;
			animation.SetParameter("AttackStrength", AttackStrength.Medium);
		}
    }

    // Called when an attack has reached the max windup point
    public void MaxAttack()
    {
        

        // If the attack key is still being held down, pause the animation until it's released
        if (input.Attack)
		{
			animation.SetParameter("AttackSpeed", 0f);
			
		}

		attackStrength = AttackStrength.Large;
		animation.SetParameter("AttackStrength", AttackStrength.Large);
	}

	public void OnHit(float damage, GameObject attacker)
	{
		// Apply some damage
		currentHealth -= damage;

		if (currentHealth > 0)
		{
			animation.SetParameter("IsHit", true);
		}
		else
		{
			animation.SetParameter("IsDead", true);
		}

		OnHitEvent?.Invoke(attacker);
	}

    //private void Update()
    //{
    //    if (attackStrength == AttackStrength.None)
    //    {
    //        animation.SetParameter("AttackStrength", attackStrength);
    //    }
            
    //    if (attackStrength == AttackStrength.Small)
    //    {
    //        //attackStrength = AttackStrength.None;
    //    }

    //    // If we stop attacking before it is fully charged up, trigger a medium attack
    //    if (attackStrength == AttackStrength.Medium && !input.Attack)
    //    {
    //        animation.SetParameter("AttackStrength", attackStrength);
    //        attackStrength = AttackStrength.None;
    //    }

    //    // If max attack, check to see if the input is no longer being held
    //    if(attackStrength == AttackStrength.Large && !input.Attack)
    //    {
    //        animation.SetParameter("AttackSpeed", 1f);
    //        animation.SetParameter("AttackStrength", attackStrength);
    //        //attackStrength = AttackStrength.None;
    //    }


    //}
}