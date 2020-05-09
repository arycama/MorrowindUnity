using System;
using System.Collections;
using System.Collections.Generic;
using Esm;
using UnityEngine;

/// <summary>
/// Controls the loading of body parts and equipping/unequipping of items
/// </summary>
public class CharacterBody : MonoBehaviour
{
	/// <summary>
	/// Converts a Biped Part into a Body Part to be loaded onto a character
	/// </summary>
	private static readonly Dictionary<BipedPart, BodyPartPiece> bodyPartNames = new Dictionary<BipedPart, BodyPartPiece>
	{
		{BipedPart.Head, BodyPartPiece.Head },
		{BipedPart.Hair, BodyPartPiece.Hair },
		{BipedPart.Neck, BodyPartPiece.Neck },
		{BipedPart.Chest, BodyPartPiece.Chest },
		{BipedPart.Groin, BodyPartPiece.Groin },
		{BipedPart.RightHand, BodyPartPiece.Hand },
		{BipedPart.LeftHand, BodyPartPiece.Hand },
		{BipedPart.RightWrist, BodyPartPiece.Wrist },
		{BipedPart.LeftWrist, BodyPartPiece.Wrist },
		{BipedPart.RightForearm, BodyPartPiece.Forearm },
		{BipedPart.LeftForearm, BodyPartPiece.Forearm },
		{BipedPart.RightUpperArm, BodyPartPiece.Upperarm },
		{BipedPart.LeftUpperArm, BodyPartPiece.Upperarm },
		{BipedPart.RightUpperLeg, BodyPartPiece.Upperleg },
		{BipedPart.LeftUpperLeg, BodyPartPiece.Upperleg },
		{BipedPart.RightKnee, BodyPartPiece.Knee },
		{BipedPart.LeftKnee, BodyPartPiece.Knee },
		{BipedPart.RightAnkle, BodyPartPiece.Ankle },
		{BipedPart.LeftAnkle, BodyPartPiece.Ankle },
		{BipedPart.RightFoot, BodyPartPiece.Foot },
		{BipedPart.LeftFoot, BodyPartPiece.Foot }
	};

	/// <summary>
	/// Converts a transform name to a Biped Part
	/// </summary>
	private static readonly Dictionary<string, BipedPart> bipedPartNames = new Dictionary<string, BipedPart>()
	{
		{"Head", BipedPart.Head },
		{"Hair", BipedPart.Hair },
		{"Neck", BipedPart.Neck},
		{"Bip01", BipedPart.Chest },
		{"Groin", BipedPart.Groin},
		{"Skirt", BipedPart.Skirt },
		{"Right Hand", BipedPart.RightHand },
		{"Left Hand", BipedPart.LeftHand },
		{"Right Wrist", BipedPart.RightWrist},
		{"Left Wrist", BipedPart.LeftWrist},
		{"Shield Bone", BipedPart.Shield },
		{"Right Forearm", BipedPart.RightForearm},
		{"Left Forearm", BipedPart.LeftForearm},
		{"Right Upper Arm", BipedPart.RightUpperArm},
		{"Left Upper Arm", BipedPart.LeftUpperArm},
		{"Right Foot", BipedPart.RightFoot },
		{"Left Foot", BipedPart.LeftFoot },
		{"Right Ankle", BipedPart.RightAnkle},
		{"Left Ankle", BipedPart.LeftAnkle},
		{"Right Knee", BipedPart.RightKnee},
		{"Left Knee", BipedPart.LeftKnee},
		{"Right Upper Leg", BipedPart.RightUpperLeg},
		{"Left Upper Leg", BipedPart.LeftUpperLeg},
		{"Right Clavicle", BipedPart.RightPauldron },
		{"Left Clavicle", BipedPart.LeftPauldron },
		{"Weapon Bone", BipedPart.Weapon },
		{"Tail", BipedPart.Tail }
	};

	[SerializeField]
	private BodyPartRecord head;

	[SerializeField]
	private BodyPartRecord hair;

	[SerializeField]
	private Race race;

	[SerializeField]
	private bool isFemale;

	[SerializeField]
	private Dictionary<BipedPart, EquippedPart> bodyPartPairs = new Dictionary<BipedPart, EquippedPart>();

	public Dictionary<BipedPart, EquippedPart> PartParts => bodyPartPairs;

	public void Initialize(Race race, BodyPartRecord head, BodyPartRecord hair, bool isFemale)
	{
		this.race = race;
		this.head = head;
		this.hair = hair;
		this.isFemale = isFemale;

		// Go through each transform, see if their name matches a biped part, and if so, add them to the list
		var children = GetComponentsInChildren<Transform>();
		foreach (var child in children)
		{
			// Get the biped part name from the transform name (Many parts are joints only and will not contain a transform)
			BipedPart bipedPart;
			if(!bipedPartNames.TryGetValue(child.name, out bipedPart))
			{
				continue;
			}

			// Create the body part
			CreateEquippedPart(child, bipedPart);

			// Invert the X scale of left pieces
			switch (child.name)
			{
				case "Left Clavicle":
				case "Left Upper Arm":
				case "Left Forearm":
				case "Left Wrist":
				case "Left Upper Leg":
				case "Left Knee":
				case "Left Ankle":
				case "Left Foot":
					child.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
					break;
			}

			// If this is a head, also add it as the hair
			if (bipedPart == BipedPart.Head)
			{
				var hairGameObject = new GameObject("Hair");
				hairGameObject.transform.SetParent(child, false);
				CreateEquippedPart(hairGameObject.transform, BipedPart.Hair);
			}

			if(bipedPart == BipedPart.Groin)
			{
				CreateEquippedPart(child, BipedPart.Skirt);
			}
		}

		// Sets the height/weight of this npc
		race.SetNpcData(gameObject.transform, isFemale);
	}

	public void CreateEquippedPart(Transform child, BipedPart part)
	{
		BodyPartPiece bodyPartType;
		if (bodyPartNames.TryGetValue(part, out bodyPartType))
		{
			BodyPartRecord bodyPart = null;
			switch (bodyPartType)
			{
				case BodyPartPiece.Head:
					bodyPart = head;
					break;
				case BodyPartPiece.Hair:
					bodyPart = hair;
					break;
				case BodyPartPiece.Chest:
					LoadSkin();
					break;
				case BodyPartPiece.Hand:
					break;
				case BodyPartPiece.Foot:
					if (!race.IsBeastRace) { bodyPart = BodyPartRecord.GetBodyPart(race, bodyPartType, isFemale); }
					break;
				case BodyPartPiece.None:
					break;
				default:
					bodyPart = BodyPartRecord.GetBodyPart(race, bodyPartType, isFemale);
					break;
			}

			if(bodyPart != null)
			{
				BodyPartRecord.Create(bodyPart, null, child);
			}
		}

		var equippedPart = new EquippedPart(child);
		bodyPartPairs.Add(part, equippedPart);
	}

	private void LoadSkin()
	{
		var bodyPart = BodyPartRecord.GetBodyPart(race, BodyPartPiece.Chest, isFemale);
		BodyPartRecord.Create(bodyPart, null, transform);
	}
}