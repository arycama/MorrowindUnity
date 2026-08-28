using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class NativeRenderPassSystem : IDisposable
{
	private readonly NativeList<TextureHandle> attachments = new(8, Allocator.Persistent);
	private readonly NativeList<int> outputIndices = new(8, Allocator.Persistent);
	private readonly NativeList<int> inputIndices = new(8, Allocator.Persistent);
	private readonly NativeList<SubPassDescriptor> subPasses = new(8, Allocator.Persistent);
	private int depthIndex = -1;
	private int depthHandleIndex = -1;
	private SubPassFlags flags;
	private readonly StringBuilder passNameBuilder = new();
	private readonly List<NativePassDescriptor> nativePassDescriptors = new();

	public NativePassDescriptor GetDescriptor(int index) => nativePassDescriptors[index];

	public void Clear()
	{
		nativePassDescriptors.Clear();
	}

	public (int nativePassIndex, bool isNewSubPass) AddRenderPass(string name, int index, ResizableArray<RenderTargetInfo> targets, List<TextureHandle> resources, List<TextureHandle> outputs, List<TextureHandle> inputs)
	{
		// Native render pass logic
		var isNativePass = outputs.Count > 0;
		var canMergeWithExistingPass = isNativePass && subPasses.Length < 8;
		foreach (var resource in resources)
		{
			// Check to see if any of the resources read are part of the current render pass
			for (var i = 0; i < attachments.Length; i++)
			{
				if (attachments[i].index != resource.index)
					continue;

				canMergeWithExistingPass = false;
				break;
			}
		}

		// If we have a current pass in progress we can't merge with, end it
		var isInNativePass = attachments.Length > 0;
		if (!canMergeWithExistingPass && isInNativePass)
		{
			// End current sub pass
			subPasses.Add(new() { inputs = new(inputIndices.AsArray()), colorOutputs = new(outputIndices.AsArray()), flags = flags });
			outputIndices.Clear();
			inputIndices.Clear();
			flags = SubPassFlags.None;

			// Start new renderpass?
			var passEndIndex = index - 1; // Since this is called from the first pass that is not the render pass index, the previous pass is the end index
			nativePassDescriptors.Add(new(new(attachments.AsArray(), Allocator.Temp), new(subPasses.AsArray(), Allocator.Temp), depthIndex, passEndIndex, passNameBuilder.ToString()));
			attachments.Clear();
			subPasses.Clear();
			_ = passNameBuilder.Clear();
			depthIndex = -1;
		}

		var isNewSubPass = false;
		var nativePassIndex = -1;
		if (isNativePass) // TODO: Does it matter if we skip this check
		{
			nativePassIndex = nativePassDescriptors.Count;
			if (passNameBuilder.Length > 0)
				_ = passNameBuilder.Append(", ");
			_ = passNameBuilder.Append(name);

			// Check if we can merge with an existing subpass
			var canMergeSubPass = canMergeWithExistingPass && subPasses.Length < 8;
			if (canMergeSubPass)
			{
				// Depth must always be first, if assigned
				if (depthHandleIndex != -1)
				{
					if (outputs[0].index != depthHandleIndex || outputs.Count - 1 != outputIndices.Length)
						canMergeSubPass = false;
				}
				else if (outputs.Count != outputIndices.Length)
				{
					// Otherwise we can compare the output length and indices directly
					canMergeSubPass = false;
				}

				if (canMergeSubPass)
				{
					// Check if all input indices are equal to existing ones. We don't check more than this, because this allows subpasses with no inputs to be merged with subpasses with inputs.
					// It also allows a subpass with input 0 to be merged with a subpass with inputs 0 and 1, since this doesn't break the indexing.
					for (var i = 0; i < inputs.Count; i++)
					{
						var input = inputs[i];
						var currentInput = attachments[inputIndices[i]];
						if (currentInput.index == input.index)
							continue;

						canMergeSubPass = false;
						break;
					}

					// Check outputs
					if (canMergeSubPass)
					{
						// If a depth index is assigned and equal, it will be at zero, so skip it as we already compared
						var start = depthIndex != -1 ? 1 : 0;
						var offset = depthIndex != -1 ? 1 : 0;
						for (var i = start; i < outputs.Count; i++)
						{
							var output = outputs[i];
							var currentInput = attachments[outputIndices[i - offset]];
							if (currentInput.index == output.index)
								continue;

							canMergeSubPass = false;
							break;
						}
					}
				}
			}

			// If we can't merge or this is a new pass, add attachments
			if (!canMergeSubPass || !isInNativePass)
			{
				// If there is already a subpass, end it
				var isInSubPass = outputIndices.Length > 0;
				if (isInSubPass)
				{
					subPasses.Add(new() { inputs = new(inputIndices.AsArray()), colorOutputs = new(outputIndices.AsArray()), flags = flags });
					outputIndices.Clear();
					inputIndices.Clear();
					flags = SubPassFlags.None;
					isNewSubPass = true;
				}

				// Start new subpass
				// If we can't merge, start a new subpass and add the attachments and output+input indices
				int GetAttachmentIndexOrAdd(TextureHandle attachment)
				{
					// Check if handle already exists, otherwise add
					for (var i = 0; i < attachments.Length; i++)
						if (attachments[i].index == attachment.index)
							return i;

					attachments.Add(attachment);
					return attachments.Length - 1;
				}

				// Outputs
				foreach (var output in outputs)
				{
					var attachmentIndex = GetAttachmentIndexOrAdd(output);
					var target = targets[output.index];
					var isColor = target.descriptor.format switch
					{
						GraphicsFormat.D16_UNorm or GraphicsFormat.D24_UNorm or GraphicsFormat.D32_SFloat or GraphicsFormat.D16_UNorm_S8_UInt or GraphicsFormat.D24_UNorm_S8_UInt or GraphicsFormat.D32_SFloat_S8_UInt or GraphicsFormat.S8_UInt => false,
						_ => true,
					};

					if (isColor)
						outputIndices.Add(attachmentIndex);
					else
					{
						depthIndex = attachmentIndex;
						depthHandleIndex = output.index;
					}
				}

				// Input attachments
				// TODO: Detect read-only depth and set in later pass
				foreach (var input in inputs)
				{
					var attachmentIndex = GetAttachmentIndexOrAdd(input);
					inputIndices.Add(attachmentIndex);
					if (attachmentIndex == depthIndex)
						flags |= SubPassFlags.ReadOnlyDepth;
				}
			}
		}

		return (nativePassIndex, isNewSubPass);
	}

	public void Dispose()
	{
		attachments.Dispose();
		outputIndices.Dispose();
		inputIndices.Dispose();
		subPasses.Dispose();
	}
}
