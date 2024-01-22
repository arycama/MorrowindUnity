using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Esm
{
	public class Script : EsmRecordCollection<Script>
	{
		[SerializeField]
		private ScriptHeader scriptHeader;

		[SerializeField, TextArea(1, 10)]
		private string scriptData;

		[SerializeField, TextArea(1, 30)]
		private string scriptText;

		private readonly Dictionary<string, float> scriptVariables = new Dictionary<string, float>();

		public string Id => name;
		public string ScriptText => scriptText;
		public IReadOnlyDictionary<string, float> ScriptVariables => scriptVariables;
		public string ScriptData => scriptData;
		public ScriptHeader ScriptHeader => scriptHeader;

		public void SetScriptVariable(string name, float value)
		{
			scriptVariables[name] = value;
		}

		public override void Initialize(BinaryReader reader, RecordHeader header)
		{
			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.ScriptHeader:
						scriptHeader = new ScriptHeader(reader);
						name = scriptHeader.Name;
						break;
					case SubRecordType.ScriptVariable:
						CreateScriptVariables(reader.ReadBytes(size));
						break;
					case SubRecordType.ScriptData:
						CreateScriptData(reader.ReadBytes(size));
						break;
					case SubRecordType.ScriptText:
						scriptText = reader.ReadString(size);
						break;
				}
			}
		}

		private void CreateScriptVariables(byte[] data)
		{
			var sb = new StringBuilder();
			foreach (var character in data)
			{
				if (character == 0)
				{
					scriptVariables.Add(sb.ToString(), 0);
					sb.Clear();
					continue;
				}

				sb.Append((char)character);
			}
		}

		private void CreateScriptData(byte[] data)
		{
			var sb = new StringBuilder();
			foreach (var character in data)
			{
				if (character != 0)
					sb.Append((char)character);
			}
			scriptData = sb.ToString();
		}
	}
}