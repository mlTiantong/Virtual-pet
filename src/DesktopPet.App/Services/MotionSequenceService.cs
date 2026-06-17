using System.Text.Json;
using DesktopPet.App.Models;

namespace DesktopPet.App.Services;

public sealed class MotionSequenceService
{
    private readonly string _assetRoot;
    private MotionSequenceManifest _manifest;
    private CancellationTokenSource? _cts;

    public MotionSequenceService(string assetRoot)
    {
        _assetRoot = assetRoot;
        _manifest = new MotionSequenceManifest();
    }

    public void LoadManifest()
    {
        var path = System.IO.Path.Combine(_assetRoot, "motion-sequence.m8.json");
        if (!System.IO.File.Exists(path)) return;

        var json = System.IO.File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _manifest = JsonSerializer.Deserialize<MotionSequenceManifest>(json, options)
                     ?? new MotionSequenceManifest();
    }

    public bool HasSequence(string id) => _manifest.Sequences.ContainsKey(id);

    public async Task PlaySequenceAsync(
        string sequenceId,
        Action<string, bool> playAnimation,
        Func<string, double, double, Task>? showAndMoveProp = null,
        Func<string, double, int>? tweenProp = null)
    {
        if (!_manifest.Sequences.TryGetValue(sequenceId, out var seq)) return;

        Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        foreach (var step in seq.Steps)
        {
            if (token.IsCancellationRequested) return;

            if (step.Animation != null)
            {
                playAnimation(step.Animation, false);
            }

            if (step.Prop != null && step.Motion != null && showAndMoveProp != null && tweenProp != null)
            {
                await showAndMoveProp(step.Prop, 0, 0);
                await Task.Delay(step.DurationMs, token);
            }
            else if (step.DurationMs > 0)
            {
                await Task.Delay(step.DurationMs, token);
            }
        }

        if (!token.IsCancellationRequested)
        {
            playAnimation("idle_m8", false);
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _cts = null;
    }
}
