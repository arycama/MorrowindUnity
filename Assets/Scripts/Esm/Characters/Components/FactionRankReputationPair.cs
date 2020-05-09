#pragma warning disable 0108

using UnityEngine;

public class FactionRankReputationPair
{
	[SerializeField]
	private byte rank;

	[SerializeField]
	private byte reputation;

	public FactionRankReputationPair(byte rank, byte reputation)
	{
		this.rank = rank;
		this.reputation = reputation;
	}

	public byte Rank { get; set; }
	public byte Reputation { get; set; }
}
