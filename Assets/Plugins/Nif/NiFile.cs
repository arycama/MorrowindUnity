using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace Nif
{
	public class NiFile
	{
		private NiObject[] niObjects;
		private NiAVObject[] roots;
		public Dictionary<string, ClipInfo> animationCache;
		private readonly List<GameObject> createdObjects = new List<GameObject>();

		public string Description { get; }

		public int Version { get; }
		public int ChildCount { get; }
		public int RootCount { get; }

		public IReadOnlyList<NiObject> NiObjects => niObjects;
		//public IDictionary<string, ClipInfo> animationCache => animationCache;
		public IList<GameObject> CreatedObjects => createdObjects;

		public BinaryReader Reader { get; private set; }

		public NiFile(BinaryReader reader)
		{
			Reader = reader;

			Description = Encoding.ASCII.GetString(reader.ReadBytes(40));
			Version = reader.ReadInt32();
			ChildCount = reader.ReadInt32();

			// Create the NiObjects, using a string for the type
			niObjects = new NiObject[ChildCount];
			for (var i = 0; i < ChildCount; i++)
			{
				var name = reader.ReadLengthPrefixedString();

				try
				{
					var type = Type.GetType("Nif." + name, true);
					niObjects[i] = (NiObject)Activator.CreateInstance(type, this);
				}
				catch(Exception ex)
				{
					// We don't support all Nif types currently, eg NiBSPArrayController. So just stop loading th emodel. Otherwise this might break other components depending on it
					Debug.LogError(ex.Message);
					return;
				}
			}

			RootCount = reader.ReadInt32();
			roots = new NiAVObject[RootCount];
			for (var i = 0; i < RootCount; i++)
			{
				var index = reader.ReadInt32();
				roots[i] = NiObjects[index] as NiAVObject;
			}

			// Use this to initialize stuff that can't be initialized in constructor
			foreach (var niObject in NiObjects)
			{
				niObject.Process();
			}
		}

		// Creates a GameObject hierachy from a NiFile. If a root GameObject is specified, it will attempt to "load" the NiFile into the existing hierachy, modifying mesh transforms and such. (Can be used for loading Npcs, and armour etc.)
		public GameObject CreateGameObject(Transform parent = null)
		{
			if(roots == null || roots.Length < 1)
			{
				return null;
			}

			roots[0].Parent = parent;
			roots[0].ProcessNiObject(null);

			return roots[0].GameObject;
		}
	}
}