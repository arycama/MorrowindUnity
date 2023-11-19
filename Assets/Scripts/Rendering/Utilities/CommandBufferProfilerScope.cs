using System;
using UnityEngine.Rendering;

public readonly struct CommandBufferProfilerScope : IDisposable
{
    private readonly string name;
    private readonly CommandBuffer command;

    public CommandBufferProfilerScope(CommandBuffer command, string name)
    {
        this.name = name;
        this.command = command;

        command.BeginSample(name);
    }

    void IDisposable.Dispose()
    {
        command.EndSample(name);
    }
}