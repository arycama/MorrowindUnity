using Esm;
using UnityEngine;

[SelectionBase]
public class LightItem : Item<LightItem, LightRecord>
{
	[SerializeField]
	private float time;

    private float phase, brightness, startTime, lastTime, ticksToAdvance;

	protected override void Initialize(LightRecord record, ReferenceData referenceData)
	{
		base.Initialize(record, referenceData);
		time = referenceData.Health;

        phase = 0.25f + Random.value * 0.75f;
        brightness = 0.675f;
        startTime = 0.0f;
        lastTime = 0.0f;
        ticksToAdvance = 0.0f;
    }

	private void Update()
	{
        var time = Time.time;
        if(startTime == 0.0f)
            startTime = time;

        var flicker = record.Data.Flags.HasFlag(LightFlags.Flicker);
        var flickerSlow = record.Data.Flags.HasFlag(LightFlags.FlickerSlow);
        var pulse = record.Data.Flags.HasFlag(LightFlags.Pulse);
        var pulseSlow = record.Data.Flags.HasFlag(LightFlags.PulseSlow);

        if (!flicker && !flickerSlow && !pulse && !pulseSlow)
            return;

        // Updating flickering at 15 FPS like vanilla.
        var updateRate = 15.0f;
        ticksToAdvance = (float)(time - startTime - lastTime) * updateRate * 0.25f + ticksToAdvance * 0.75f;
        lastTime = time - startTime;

        float speed = (flicker || pulse) ? 0.1f : 0.05f;
        if (brightness >= phase)
            brightness -= ticksToAdvance * speed;
        else
            brightness += ticksToAdvance * speed;

        if (Mathf.Abs(brightness - phase) < speed)
        {
            if (flicker || flickerSlow)
                phase = 0.25f + Random.value * 0.75f;
            else if (pulse || pulseSlow)
                phase = phase <= 0.5f ? 1.0f : 0.25f;
        }
        
        GetComponentInChildren<Light>().intensity = brightness;
    }
}