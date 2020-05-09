using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public abstract class EsmRecord : ScriptableObject
{
	public bool IsInitialized { get; set; }
	public RecordHeader Header { get; set; }

	public abstract void Initialize(BinaryReader reader, RecordHeader header);
	public virtual void Deserialize(BinaryReader reader, RecordHeader header) { }
}

public abstract class EsmRecordCollection<T> : EsmRecord where T : EsmRecord
{
	protected static Dictionary<string, T> Records = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

	public static T Get(string key)
	{

		return Records[key];
	}

	public static T Create(BinaryReader reader, RecordHeader header)
	{
		var instance = CreateInstance<T>();
		instance.Initialize(reader, header);
		Records.Add(instance.name, instance);

		return instance;
	}
}

public abstract class EsmRecordCollection<T, K> : EsmRecord where K : EsmRecord
{
	protected static Dictionary<T, K> records = new Dictionary<T, K>();

	public static IReadOnlyDictionary<T, K> Records => records;

	public static K Get(T key) => records[key];

	public static K Create(BinaryReader reader, RecordHeader header)
	{
		var instance = CreateInstance<K>();
		instance.Initialize(reader, header);
		return instance;
	}
}