using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiTextKeyExtraData : NiExtraData
	{
		private readonly int unknownInt1, numTextKeys;
		private readonly StringKey[] textKeys;

		public NiTextKeyExtraData(NiFile niFile) : base(niFile)
		{
			unknownInt1 = niFile.Reader.ReadInt32();
			numTextKeys = niFile.Reader.ReadInt32();

			textKeys = new StringKey[numTextKeys];
			for (var i = 0; i < textKeys.Length; i++)
			{
				textKeys[i] = new StringKey(niFile.Reader);
			}

			niFile.animationCache = new Dictionary<string, ClipInfo>(StringComparer.OrdinalIgnoreCase);

			// Save the current clips(usually there is only one, but sometimes more than one), so we can add any detected animation events to it
			// Support for multiple clips at a time by using an array?
			// Also check if loop start/stop are the same as clip start/stop
			var currentClips = new List<ClipInfo>();

			// When a clip is to be removed, add it to this list so it can be "Scheduled" for removal. This allows multiple events to be added at the end of a frame.
			var clipsToRemove = new HashSet<ClipInfo>();

			// Loop over each text key and extract information for each clip
			foreach (var key in textKeys)
			{
				// Loop over text keys, storing results
				var reader = new StringReader(key.value);

				string line;
				while ((line = reader.ReadLine()) != null)
				{
					// Split the clip name and data by a semicolon
					var index = line.IndexOf(':');
					if (index < 0)
					{
						continue;
					}

					// Get the name of the clip or event
					var name = line.Substring(0, index);

					// Read the data and set the time of the clip // If we wanted to be super safe, could keep going until we find a non-whitespace character
					var data = line.Substring(index + 2);

					// Most names are clips, but sounds are also included and must be treated differently
					switch (name)
					{
						case "Sound":
							foreach(var clip in currentClips)
							{
								clip.clip.AddEvent(new AnimationEvent()
								{
									functionName = "Sound",
									messageOptions = SendMessageOptions.DontRequireReceiver,
									stringParameter = data,
									time = key.time - clip.Start
								});
							}
							continue;
						case "SoundGen":
							foreach (var clip in currentClips)
							{
								clip.clip.AddEvent(new AnimationEvent()
								{
									functionName = "SoundGenerator",
									messageOptions = SendMessageOptions.DontRequireReceiver,
									stringParameter = data,
									time = key.time - clip.Start
								});
							}

						continue;
					}

					// Check if there is a second word
					var subDataIndex = data.IndexOf(' ');
					string subData = null;
					if(subDataIndex > -1)
					{
						subData = data.Substring(subDataIndex + 1);
						data = data.Substring(0, subDataIndex);
					}

					// Could maybe use default to add a function depending on the name (For hit, etc)
					switch (data)
					{
						case "start":
						case "Start":
							currentClips.Add(CreateClip(name, key.time));
							break;
						case "stop":
						case "Stop":
						case "Stop.":
							clipsToRemove.Add(StopClip(name, key.time));
							//currentClips.Remove(StopClip(name, key.time));
							break;
						case "loop":
						case "Loop":
							switch (subData)
							{
								case "start":
								case "Start":
									if (!niFile.animationCache.ContainsKey(name))
									{
										Debug.LogFormat("{0}: {1} {2}", name, data, subData);
										break;
									}
									niFile.animationCache[name].LoopStart = key.time;
									break;
								case "stop":
								case "stop.":
								case "Stop":
									AddAnimationEvent(name, data, key.time);
									break;
								default:
									throw new NotImplementedException(string.Format("{0}: {1} {2}", name, data, subData));
							}
							break;

						case "Hit":
							AddAnimationEvent(name, data, key.time);
							break;

						case "Block":
						case "Chop":
						case "Slash":
						case "Thrust":
						case "Shoot":
							switch (subData)
							{
								case "Follow Attach":
									AddAnimationEvent(name + data + "Follow", "Attach", key.time);
									break;
								case "Follow Start":
									currentClips.Add(CreateClip(name + data + "Follow", key.time));
									break;
								case "Follow Stop":
									clipsToRemove.Add(StopClip(name + data + "Follow", key.time));
									//currentClips.Remove(StopClip(name + data + "Follow", key.time));
									break;
								case "Large Follow Start":
									currentClips.Add(CreateClip(name + data + "Large Follow", key.time));
									break;
								case "Large Follow Stop":
									clipsToRemove.Add(StopClip(name + data + "Large Follow", key.time));
									//currentClips.Remove(StopClip(name + data + "Large Follow", key.time));
									break;
								case "Medium Follow Start":
									currentClips.Add(CreateClip(name + data + "Medium Follow", key.time));
									break;
								case "Medium Follow Stop":
									clipsToRemove.Add(StopClip(name + data + "Medium Follow", key.time));
									//currentClips.Remove(StopClip(name + data + "Medium Follow", key.time));
									break;
								case "Small Follow Start":
									currentClips.Add(CreateClip(name + data + "Small Follow", key.time));
									break;
								case "Small Follow Stop":
									clipsToRemove.Add(StopClip(name + data + "Small Follow", key.time));
									//currentClips.Remove(StopClip(name + data + "Small Follow", key.time));
									break;
								case "Start":
									currentClips.Add(CreateClip(name + data, key.time));
									break;
								case "Max Attack":
								case "Min Attack":
								case "Min Hit":
									AddAnimationEvent(name + data, subData, key.time);
									break;
								case "Hit":
									AddAnimationEvent(name + data, subData, key.time);
									clipsToRemove.Add(StopClip(name + data, key.time));
									//currentClips.Remove(StopClip(name + data, key.time));
									break;
								case "Release":
								case "Stop":
									clipsToRemove.Add(StopClip(name + data, key.time));
									//currentClips.Remove(StopClip(name + data, key.time));
									break;
								case "Attach":
									AddAnimationEvent(name + data, subData, key.time);
									break;
								default:
									Debug.LogFormat("{0} {1}", data, subData);
									break;
							}
							break;

						case "Equip":
						case "Unequip":
						case "Self":
						case "Target":
						case "Touch":
							switch (subData)
							{
								case "Attach":
								case "Detach":
								case "Release":
									AddAnimationEvent(name + data, subData, key.time);
									break;
								case "Start":
									currentClips.Add(CreateClip(name + data, key.time));
									break;
								case "Stop":
									clipsToRemove.Add(StopClip(name + data, key.time));
									//currentClips.Remove(StopClip(name + data, key.time));
									break;
								default:
									throw new NotImplementedException(string.Format("{0}: {1} {2}", name, data, subData));
							}
							break;
						default:
							Debug.LogFormat("{0}: {1}", name, data);
							break;
					}

					// End current line
				}

				// End current keyframe
				if (clipsToRemove.Count == 0)
				{
					continue;
				}

				// Remove any clips that were scheduled for removal
				foreach (var clipToRemove in clipsToRemove)
				{
					currentClips.Remove(clipToRemove);
				}

				clipsToRemove.Clear();
			}
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			var root = niObject.GameObject.transform.root.gameObject;
			var animation = root.GetComponent<Animation>();
			if (animation == null)
			{
				animation = root.AddComponent<Animation>();
			}

			foreach (var clipInfo in niFile.animationCache)
			{
				animation.AddClip(clipInfo.Value.clip, clipInfo.Value.clip.name);
			}
		}

		private ClipInfo CreateClip(string name, float start)
		{
			ClipInfo clipInfo;
			if(niFile.animationCache.TryGetValue(name, out clipInfo))
			{
				return clipInfo;
			}
				
			clipInfo = new ClipInfo();

			var clip = new AnimationClip()
			{
				// Rarely, animations may not have the first letter uppercased, such as idle4 for Rats. Manually uppercase it here for now. Internally, Morrowind probably uses a case insensitive lookup.
				name =char.ToUpper(name[0]) + name.Substring(1),
				legacy = true,
				frameRate = 15
			};

			clipInfo.clip = clip;
			clipInfo.Start = start;

			niFile.animationCache.Add(name, clipInfo);
			return clipInfo;
		}

		private ClipInfo StopClip(string name, float time)
		{
			var clipInfo = niFile.animationCache[name];
			clipInfo.Stop = time;
			return clipInfo;
		}

		private void AddAnimationEvent(string clipName, string functionName, float time)
		{
			var clipInfo = niFile.animationCache[clipName];
			clipInfo.clip.AddEvent(new AnimationEvent()
			{
				functionName = functionName.Replace(" ", string.Empty),
				floatParameter = clipInfo.LoopStart - clipInfo.Start,
				messageOptions = SendMessageOptions.DontRequireReceiver,
				time = time - clipInfo.Start
			});
		}


	}
}