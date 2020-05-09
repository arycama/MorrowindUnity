using System;
using Esm;
using UnityEngine;

[Serializable]
public class OwnerData
{
	[SerializeField]
	private NpcRecord owner;

	[SerializeField]
	private Global global;

	[SerializeField]
	private Faction faction;

	[SerializeField]
	private int rank;

	public OwnerData(NpcRecord owner, Global global, Faction faction, int rank)
	{
		this.owner = owner;
		this.global = global;
		this.faction = faction;
		this.rank = rank;
	}

	public NpcRecord Owner => owner;
	public Global Global => global;
	public Faction Faction => faction;
	public int Rank => rank;
}