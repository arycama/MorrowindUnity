using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine.Rendering;
using static Unmath.Math;

public class NativeRenderPassSystem : IDisposable
{
	private readonly NativeList<TextureHandle> attachments = new(8, Allocator.Persistent);
	private readonly NativeList<TextureHandle> outputs = new(8, Allocator.Persistent);
	private readonly NativeList<TextureHandle> inputs = new(8, Allocator.Persistent);
	private readonly NativeList<SubPassDescriptor> subPasses = new(8, Allocator.Persistent);
	private TextureHandle? depthStencil;
	private SubPassFlags flags;
	private readonly StringBuilder passNameBuilder = new();
	private readonly List<NativePassDescriptor> nativePassDescriptors = new();

	public NativePassDescriptor GetDescriptor(int index) => nativePassDescriptors[index];

	public void Clear()
	{
		nativePassDescriptors.Clear();
	}

	public (int nativePassIndex, bool isNewSubPass) AddRenderPass(PassBuilder builder)
	{
		var isNativePass = builder.Outputs.Count > 0 || builder.DepthStencil.index != -1;
		var canMergeWithExistingPass = isNativePass && subPasses.Length < 8;

		// If depth stencil is set, we can only merge if it is equal
		if (depthStencil.HasValue && builder.DepthStencil.index != -1 && builder.DepthStencil != depthStencil.Value)
			canMergeWithExistingPass = false;

		// If any current attachments are read as regualr resources, we need to start a new render pass
		if (canMergeWithExistingPass)
		{
			foreach (var attachment in attachments)
			{
				if (!builder.Resources.Contains(attachment))
					continue;

				canMergeWithExistingPass = false;
				break;
			}
		}

		void EndSubPass()
		{
			// Resolve the attachment handles into attachment indiecs
			var colorOutputs = new AttachmentIndexArray(outputs.Length);
			for (var i = 0; i < outputs.Length; i++)
			{
				var outputHandle = outputs[i];
				for (var j = 0; j < attachments.Length; j++)
				{
					if (outputHandle != attachments[j])
						continue;

					colorOutputs[i] = j;
					break;
				}
			}

			outputs.Clear();

			var inputs = new AttachmentIndexArray(this.inputs.Length);
			for (var i = 0; i < this.inputs.Length; i++)
			{
				var inputHandle = this.inputs[i];
				for (var j = 0; j < attachments.Length; j++)
				{
					if (inputHandle != attachments[j])
						continue;

					inputs[i] = j;
					break;
				}
			}

			this.inputs.Clear();

			subPasses.Add(new() { inputs = inputs, colorOutputs = colorOutputs, flags = flags });
			flags = SubPassFlags.None;
		}

		void EndRenderPass()
		{
			// Resolve depthStencil index
			var depthStencilAttachmentIndex = -1;
			for (var i = 0; i < attachments.Length; i++)
			{
				if (attachments[i].index != depthStencil)
					continue;

				depthStencilAttachmentIndex = i;
				break;
			}

			// TODO: Should this just call end subpass?
			var passEndIndex = builder.Index - 1; // Since this is called from the first pass that is not the render pass index, the previous pass is the end index
			nativePassDescriptors.Add(new(new(attachments.AsArray(), Allocator.Temp), new(subPasses.AsArray(), Allocator.Temp), depthStencilAttachmentIndex, passEndIndex, passNameBuilder.ToString()));
			attachments.Clear();
			subPasses.Clear();
			_ = passNameBuilder.Clear();
			depthStencil = default;
		}

		// If we have a current pass in progress we can't merge with, end it
		var isInNativePass = attachments.Length > 0 || depthStencil.HasValue;
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
			_ = passNameBuilder.Append(builder.Name);

			// Check if we can merge with an existing subpass
			var canMergeSubPass = canMergeWithExistingPass;
			if (canMergeSubPass)
			{
				// Check if all input indices are equal to existing ones. We don't check more than this, because this allows subpasses with no inputs to be merged with subpasses with inputs.
				for (var i = 0; i < Min(builder.Inputs.Count, inputs.Length); i++)
				{
					if (inputs[i] == builder.Inputs[i])
						continue;

					canMergeSubPass = false;
					break;
				}

				// Check outputs
				if (canMergeSubPass)
				{
					if (builder.Outputs.Count != outputs.Length)
						canMergeSubPass = false;
					else
					{
						for (var i = 0; i < builder.Outputs.Count; i++)
						{
							var output = builder.Outputs[i];
							var currentOutput = outputs[i];
							if (currentOutput == output)
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
				var isInSubPass = outputs.Length > 0 || depthStencil.HasValue;
				if (isInSubPass)
				{
					EndSubPass();
					isNewSubPass = true;
				}

				// Start new subpass

				// Depth Stencil (TODO: This is unneccessarily repeated for non-first passes)
				if (builder.DepthStencil.index != -1)
				{
					if (!attachments.Contains(builder.DepthStencil))
						attachments.Add(builder.DepthStencil);

					depthStencil = builder.DepthStencil;
				}

				// Outputs
				foreach (var output in builder.Outputs)
				{
					if (!attachments.Contains(output))
						attachments.Add(output);
					outputs.Add(output);
				}

				// Inputs
				foreach (var input in builder.Inputs)
				{
					if (!attachments.Contains(input))
						attachments.Add(input);
					inputs.Add(input);

					if (input == depthStencil)
						flags |= SubPassFlags.ReadOnlyDepth;
				}
			}
		}

		return (nativePassIndex, isNewSubPass);
	}

	public void Dispose()
	{
		attachments.Dispose();
		outputs.Dispose();
		inputs.Dispose();
		subPasses.Dispose();
	}
}
