using System;
using VRCFaceTracking.Core.Params.Expressions;

namespace VRCFaceTracking.Core.Params.Data.Mutation;

/// <summary>
/// Jerry-template style expression exaggeration.
///
/// Jerry's VRCFaceTracking templates (Adjerry91/VRCFaceTracking-Templates) run every expression
/// through a "sensitivity" direct blend tree in the avatar's animator which multiplies the smoothed
/// OSC value by a per-expression gain (1.25 by default), saturating at full blendshape range so
/// expressions read as more pronounced. This mutation reproduces that gain stage inside VRCFT so
/// avatars without Jerry's prefab (e.g. native /tracking/face/v1 avatars) can get the same look,
/// with per-region multipliers matching Jerry's in-game sensitivity groups.
/// </summary>
public class Exaggeration : TrackingMutation
{
    public override string Name => "Expression Exaggeration";
    public override string Description =>
        "Jerry-template style gain: output = clamp(value * master * region). " +
        "1.25 (Jerry's default) makes expressions read ~25% stronger and saturate sooner. " +
        "Curve exponent shapes the response (1 = linear, >1 = softer mids with punchier peaks).";
    public override MutationPriority Step => MutationPriority.PostprocessorFinal;

    [MutationProperty("Master Gain", true, 0.25f, 3f)]
    public float masterGain = 1.25f;

    [MutationProperty("Face Gain", true, 0f, 3f)]
    public float faceGain = 1f;

    [MutationProperty("Brow Gain", true, 0f, 3f)]
    public float browGain = 1f;

    [MutationProperty("Eye Squint Gain", true, 0f, 3f)]
    public float eyeSquintGain = 1f;

    [MutationProperty("Eyelid Gain", true, 0f, 3f)]
    public float eyelidGain = 1f;

    [MutationProperty("Eye Look Gain", true, 0f, 3f)]
    public float eyeLookGain = 1f;

    [MutationProperty("Curve Exponent", true, 0.25f, 4f)]
    public float curveExponent = 1f;

    /// <summary>
    /// Apply master * region gain, optional gamma curve, and clamp to the 0-1 blendshape range.
    /// </summary>
    private float Boost(float value, float regionGain)
    {
        value *= masterGain * regionGain;
        if (curveExponent != 1f && value > 0f)
        {
            value = MathF.Pow(value, curveExponent);
        }

        return Math.Clamp(value, 0f, 1f);
    }

    public override void MutateData(ref UnifiedTrackingData data)
    {
        var shapes = data.Shapes;

        // Expression shapes, region-split like Jerry's sensitivity groups
        for (var i = 0; i < shapes.Length; i++)
        {
            float regionGain;
            if (i is (int)UnifiedExpressions.EyeSquintRight or (int)UnifiedExpressions.EyeSquintLeft)
            {
                regionGain = eyeSquintGain;
            }
            else if (i is >= (int)UnifiedExpressions.BrowPinchRight and <= (int)UnifiedExpressions.BrowOuterUpLeft)
            {
                regionGain = browGain;
            }
            else
            {
                regionGain = faceGain;
            }

            shapes[i].Weight = Boost(shapes[i].Weight, regionGain);
        }

        // Eyelids: openness is 1 = open, so exaggerate the *closed* amount
        data.Eye.Left.Openness = 1f - Boost(1f - data.Eye.Left.Openness, eyelidGain);
        data.Eye.Right.Openness = 1f - Boost(1f - data.Eye.Right.Openness, eyelidGain);

        // Eye look: gain is applied symmetrically around center; no gamma (keep gaze linear)
        var lookGain = masterGain * eyeLookGain;
        data.Eye.Left.Gaze.x = Math.Clamp(data.Eye.Left.Gaze.x * lookGain, -1f, 1f);
        data.Eye.Left.Gaze.y = Math.Clamp(data.Eye.Left.Gaze.y * lookGain, -1f, 1f);
        data.Eye.Right.Gaze.x = Math.Clamp(data.Eye.Right.Gaze.x * lookGain, -1f, 1f);
        data.Eye.Right.Gaze.y = Math.Clamp(data.Eye.Right.Gaze.y * lookGain, -1f, 1f);
    }
}
