using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine.Rendering;
using static Unmath.Math;

public class NativeRenderPassSystem : IDisposable
{
	private readonly NativeList<TextureHandle> attachments = new(8, Allocator.Persistent);
	private readonly NativeList<int> outputIndices = new(8, Allocator.Persistent);
	private readonly NativeList<int> inputIndices = new(8, Allocator.Persistent);
	private readonly NativeList<SubPassDescriptor> subPasses = new(8, Allocator.Persistent);
	private int depthStencilAttachmentIndex = -1;
	private int depthStencilIndex = -1;
	private SubPassFlags flags;
	private readonly StringBuilder passNameBuilder = new();
	private readonly List<NativePassDescriptor> nativePassDescriptors = new();

	public NativePassDescriptor GetDescriptor(int index) => nativePassDescriptors[index];

	public void Clear()
	{
		nativePassDescriptors.Clear();
	}

	public (int nativePassIndex, bool isNewSubPass) AddRenderPass(string name, int index, List<TextureHandle> resources, List<TextureHandle> outputs, List<TextureHandle> inputs, TextureHandle depthStencil)
	{
		var isNativePass = outputs.Count > 0 || depthStencil.index != -1;
		var canMergeWithExistingPass = isNativePass && subPasses.Length < 8;

		// If depth stencil is set, we can only merge if it is equal
		if (depthStencilIndex != -1 && depthStencil.index != -1 && depthStencil.index != depthStencilIndex)
			canMergeWithExistingPass = false;

		// If any current attachments are read as regualr resources, we need to start a new render pass
		if (canMergeWithExistingPass)
		{
			foreach (var attachment in attachments)
			{
				if (!resources.Contains(attachment))
					continue;

				canMergeWithExistingPass = false;
				break;
			}
		}

		void EndSubPass()
		{
			subPasses.Add(new() { inputs = new(inputIndices.AsArray()), colorOutputs = new(outputIndices.AsArray()), flags = flags });
			outputIndices.Clear();
			inputIndices.Clear();
			flags = SubPassFlags.None;
		}

		void EndRenderPass()
		{
			// TODO: Should this just call end subpass?
			var passEndIndex = index - 1; // Since this is called from the first pass that is not the render pass index, the previous pass is the end index
			nativePassDescriptors.Add(new(new(attachments.AsArray(), Allocator.Temp), new(subPasses.AsArray(), Allocator.Temp), depthStencilAttachmentIndex, passEndIndex, passNameBuilder.ToString()));
			attachments.Clear();
			subPasses.Clear();
			_ = passNameBuilder.Clear();
			depthStencilAttachmentIndex = -1;
			depthStencilIndex = -1;
		}

		// If we have a current pass in progress we can't merge with, end it
		var isInNativePass = attachments.Length > 0 || depthStencilIndex != -1;
		if (isInNativePass && !canMergeWithExistingPass)
		{
			EndSubPass();
			EndRenderPass();
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
			var canMergeSubPass = canMergeWithExistingPass;
			if (canMergeSubPass)
			{
				// Check if all input indices are equal to existing ones. We don't check more than this, because this allows subpasses with no inputs to be merged with subpasses with inputs.
				for (var i = 0; i < Min(inputs.Count, inputIndices.Length); i++)
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
					if (outputs.Count != outputIndices.Length)
						canMergeSubPass = false;
					else
					{
						for (var i = 0; i < outputs.Count; i++)
						{
							var output = outputs[i];
							var currentInput = attachments[outputIndices[i]];
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
				var isInSubPass = outputIndices.Length > 0 || depthStencilIndex != -1;
				if (isInSubPass)
				{
					EndSubPass();
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

				// Depth Stencil (TODO: This is unneccessarily repeated for non-first passes)
				if (depthStencil.index != -1)
				{
					var attachmentIndex = GetAttachmentIndexOrAdd(depthStencil);
					depthStencilAttachmentIndex = attachmentIndex;
					depthStencilIndex = depthStencil.index;
				}

				// Outputs
				foreach (var output in outputs)
				{
					var attachmentIndex = GetAttachmentIndexOrAdd(output);
					outputIndices.Add(attachmentIndex);
				}

				// Input attachments
				// TODO: Detect read-only depth and set in later pass
				foreach (var input in inputs)
				{
					var attachmentIndex = GetAttachmentIndexOrAdd(input);
					inputIndices.Add(attachmentIndex);
					if (input.index == depthStencilIndex)
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
