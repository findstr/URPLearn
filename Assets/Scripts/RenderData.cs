using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class RenderData {
    public Camera camera;
    public ScriptableRenderContext ctx;
    public CullingResults cullResults;
	public ShadowSetting shadowSettings;
    private CommandBuffer command = new CommandBuffer();
    public CommandBuffer command_begin(string name)
    {
        command.name = name;
        return command;
    }
    public void command_end()
    {
        ctx.ExecuteCommandBuffer(command);
        command.Clear();
    }
}

