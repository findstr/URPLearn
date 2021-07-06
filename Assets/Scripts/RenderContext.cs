using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class RenderContext {
    public Camera camera;
    public ScriptableRenderContext ctx;
    public CullingResults cull_result;
	public ShadowSetting shadow_setting;
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

