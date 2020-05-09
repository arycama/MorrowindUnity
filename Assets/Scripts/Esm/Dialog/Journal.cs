using System.Collections.Generic;
using Esm;
using UnityEngine;

public class Journal : MonoBehaviour
{
	[SerializeField]
	private Dictionary<string, DialogRecord> topics = new Dictionary<string, DialogRecord>();

	[SerializeField]
	private Dictionary<string, int> entries = new Dictionary<string, int>();

	public IReadOnlyDictionary<string, DialogRecord> Topics { get { return topics; } }
	public IReadOnlyDictionary<string, int> Entires { get { return entries; } }

	public void AddTopic(string topic, DialogRecord dialog)
	{
		topics[topic] = dialog;
	}

	public bool AddOrUpdateEntry(string entry, int index)
	{
		if(entries.TryGetValue(entry, out index))
		{
			entries[entry] = index;
			return true;
		}

		entries.Add(entry, index);
		return false;
	}

	public bool KnowsTopic(string id)
	{
		return Topics.ContainsKey(id);
	}
}