using System;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Data/New Pipeline")]
public class NewPipelineAsset : RenderPipelineAsset<NewPipeline>
{
	public bool clearBetweenPasses;
	[field: SerializeField, Pow2(8)] public int Samples { get; private set; } = 1;

	public override string renderPipelineShaderTag => "NewPipeline";

	protected override RenderPipeline CreatePipeline()
	{
		return new NewPipeline(this);
	}
}
