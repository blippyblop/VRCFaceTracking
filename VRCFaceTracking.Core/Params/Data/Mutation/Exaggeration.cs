using System;
using System.Diagnostics;
using VRCFaceTracking.Core.Params.Expressions;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Core.Params.Data.Mutation;

/// <summary>
/// Jerry-template style expression exaggeration and output shaping.
///
/// Reproduces the processing stages of Adjerry91's VRCFaceTracking templates inside VRCFT, as decoded
/// from the template's animator controllers:
/// - Master gain of 1.25 (the OSCm proxy channels: output thresholds at 0.8 == x1.25, saturating)
/// - Gaze via a directional (freeform-cartesian) blend model: clips on the L1 diamond at radius 0.7,
///   i.e. gaze saturates at ~0.56 raw input and diagonals are normalized (sum of keys &lt;= 1)
/// - Eyelids via the 2D lid/squint model: X = 1.25 x composite lid (0.75 openness + 0.25 EyeWide;
///   0 = closed, 0.75 = neutral, 1 = wide), Y = 1.25 x squint; squint fully expressed at 0.8 raw
/// - Optional squint cancellation when the lid is closed (poor-man's replacement for Jerry's
///   EyeClosedSquintCorrective shapes, which only the avatar/prefab can drive)
/// - Optional exponential smoothing (Jerry's OSCm smoothing, scaler 15)
/// </summary>
public class Exaggeration : TrackingMutation
{
    public override string Name => "Expression Exaggeration";
    public override string Description =>
        "Jerry-template style gain and output shaping: output = clamp(value * master * region). " +
        "Enables Jerry gaze/eyelid models below for closest parity with Jerry's templates.";
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

    [MutationProperty("Jerry Gaze Blend", true)]
    public bool jerryGazeBlend = true;

    [MutationProperty("Jerry Eyelid Curve", true)]
    public bool jerryEyelidCurve = true;

    // Emulates Jerry's EyeClosedSquintCorrective shapes for avatars that do NOT define
    // closed+squint Parameter Behaviors natively: damps squint by lid closure so the
    // squint shape cannot drag a closed eyelid open. Leave off if your avatar handles
    // closed+squint via its FT Blendshapes Set / Parameter Behaviors.
    [MutationProperty("Squint Cancel When Closed", true)]
    public bool jerrySquintCancel = false;

    [MutationProperty("Jerry Smoothing", true)]
    public bool jerrySmoothing = false;

    // Jerry's OSCm smoothing scaler (15 in the templates). Time constant ~ 1/speed seconds.
    [MutationProperty("Smoothing Speed", true, 0f, 60f)]
    public float smoothingSpeed = 15f;

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private float[] _smoothedShapes = Array.Empty<float>();
    private float _smoothedLeftOpen, _smoothedRightOpen;
    private float _smoothedLeftGx, _smoothedLeftGy, _smoothedRightGx, _smoothedRightGy;

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
        // This mutation exists to shape the NATIVE /tracking/face/v1 output stream. The
        // individual-parameter output path (legacy / v2 OSC avatars, e.g. Jerry's prefab)
        // must remain bit-identical to upstream, so do nothing unless the native stream
        // is actually engaged.
        if (!ParameterSenderService.NativeStreamingActive)
        {
            return;
        }

        var shapes = data.Shapes;

        if (_smoothedShapes.Length != shapes.Length)
        {
            _smoothedShapes = new float[shapes.Length];
        }

        // 1. Jerry smoothing (exponential, scaler = speed). Applied to raw values first,
        //    matching Jerry's pipeline order (smooth -> gain -> blend trees).
        if (jerrySmoothing && smoothingSpeed > 0f)
        {
            var dt = (float)_stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();
            if (dt <= 0f || dt > 0.5f)
            {
                dt = 0.011f; // clamp to ~90hz on gaps/first frame
            }

            var alpha = 1f - MathF.Exp(-smoothingSpeed * dt);
            for (var i = 0; i < shapes.Length; i++)
            {
                shapes[i].Weight = SmoothValue(_smoothedShapes[i], shapes[i].Weight, alpha);
                _smoothedShapes[i] = shapes[i].Weight;
            }

            data.Eye.Left.Openness = SmoothValue(_smoothedLeftOpen, data.Eye.Left.Openness, alpha);
            data.Eye.Right.Openness = SmoothValue(_smoothedRightOpen, data.Eye.Right.Openness, alpha);
            data.Eye.Left.Gaze.x = SmoothValue(_smoothedLeftGx, data.Eye.Left.Gaze.x, alpha);
            data.Eye.Left.Gaze.y = SmoothValue(_smoothedLeftGy, data.Eye.Left.Gaze.y, alpha);
            data.Eye.Right.Gaze.x = SmoothValue(_smoothedRightGx, data.Eye.Right.Gaze.x, alpha);
            data.Eye.Right.Gaze.y = SmoothValue(_smoothedRightGy, data.Eye.Right.Gaze.y, alpha);
            _smoothedLeftOpen = data.Eye.Left.Openness;
            _smoothedRightOpen = data.Eye.Right.Openness;
            _smoothedLeftGx = data.Eye.Left.Gaze.x;
            _smoothedLeftGy = data.Eye.Left.Gaze.y;
            _smoothedRightGx = data.Eye.Right.Gaze.x;
            _smoothedRightGy = data.Eye.Right.Gaze.y;
        }
        else
        {
            _stopwatch.Restart();
            for (var i = 0; i < shapes.Length; i++)
            {
                _smoothedShapes[i] = shapes[i].Weight;
            }
        }

        // 2. Jerry eyelid model (composite lid + squint 2D tree)
        var skipWideAndSquint = jerryEyelidCurve;
        if (jerryEyelidCurve)
        {
            ApplyJerryEyelid(ref data, shapes);
        }
        else
        {
            // Openness is 1 = open; exaggerate the *closed* amount
            data.Eye.Left.Openness = 1f - Boost(1f - data.Eye.Left.Openness, eyelidGain);
            data.Eye.Right.Openness = 1f - Boost(1f - data.Eye.Right.Openness, eyelidGain);
        }

        // 3. Expression shapes with region gains (wide/squint already handled by the Jerry lid model)
        for (var i = 0; i < shapes.Length; i++)
        {
            if (skipWideAndSquint && i is
                    (int)UnifiedExpressions.EyeWideLeft or (int)UnifiedExpressions.EyeWideRight or
                    (int)UnifiedExpressions.EyeSquintLeft or (int)UnifiedExpressions.EyeSquintRight)
            {
                continue;
            }

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

        // 4. Gaze
        if (jerryGazeBlend)
        {
            ApplyJerryGaze(ref data.Eye.Left.Gaze);
            ApplyJerryGaze(ref data.Eye.Right.Gaze);
        }
        else
        {
            var lookGain = masterGain * eyeLookGain;
            data.Eye.Left.Gaze.x = Math.Clamp(data.Eye.Left.Gaze.x * lookGain, -1f, 1f);
            data.Eye.Left.Gaze.y = Math.Clamp(data.Eye.Left.Gaze.y * lookGain, -1f, 1f);
            data.Eye.Right.Gaze.x = Math.Clamp(data.Eye.Right.Gaze.x * lookGain, -1f, 1f);
            data.Eye.Right.Gaze.y = Math.Clamp(data.Eye.Right.Gaze.y * lookGain, -1f, 1f);
        }
    }

    /// <summary>
    /// Jerry's 2D eyelid model, per eye. X axis: 1.25 x composite lid (0.75 openness + 0.25 EyeWide):
    /// 0 = closed, 0.75 = neutral, 1 = wide (matching the blend tree sample positions).
    /// Y axis: 1.25 x squint, fully expressed at 0.8 raw. Writes final EyeClosed (into Openness),
    /// EyeWide and EyeSquint weights, plus optional squint cancellation when closed.
    /// </summary>
    private void ApplyJerryEyelid(ref UnifiedTrackingData data, UnifiedExpressionShape[] shapes)
    {
        // Left eye
        var (lClosed, lWide, lSquint) = JerryEyelid(
            masterGain * eyelidGain * (0.75f * data.Eye.Left.Openness + 0.25f * shapes[(int)UnifiedExpressions.EyeWideLeft].Weight),
            masterGain * eyeSquintGain * shapes[(int)UnifiedExpressions.EyeSquintLeft].Weight);
        if (jerrySquintCancel)
        {
            lSquint *= 1f - lClosed;
        }
        data.Eye.Left.Openness = 1f - lClosed;
        shapes[(int)UnifiedExpressions.EyeWideLeft].Weight = lWide;
        shapes[(int)UnifiedExpressions.EyeSquintLeft].Weight = lSquint;

        // Right eye
        var (rClosed, rWide, rSquint) = JerryEyelid(
            masterGain * eyelidGain * (0.75f * data.Eye.Right.Openness + 0.25f * shapes[(int)UnifiedExpressions.EyeWideRight].Weight),
            masterGain * eyeSquintGain * shapes[(int)UnifiedExpressions.EyeSquintRight].Weight);
        if (jerrySquintCancel)
        {
            rSquint *= 1f - rClosed;
        }
        data.Eye.Right.Openness = 1f - rClosed;
        shapes[(int)UnifiedExpressions.EyeWideRight].Weight = rWide;
        shapes[(int)UnifiedExpressions.EyeSquintRight].Weight = rSquint;
    }

    /// <summary>
    /// Jerry's eyelid transfer: X axis closed(0) -> neutral(0.75) -> wide(1.0); squint added on the
    /// Y axis, fully expressed at 0.8 raw (1.0 gained). Returns final closed/wide/squint weights.
    /// </summary>
    private static (float closed, float wide, float squint) JerryEyelid(float x, float y)
    {
        x = Math.Clamp(x, 0f, 1.25f);
        var squint = Math.Clamp(y, 0f, 1.25f);

        float closed, wide;
        if (x <= 0.75f)
        {
            // closed(0) -> neutral(0.75)
            closed = 1f - x / 0.75f;
            wide = 0f;
        }
        else
        {
            // neutral(0.75) -> wide(1.0)
            closed = 0f;
            wide = (x - 0.75f) / 0.25f;
        }

        squint = Math.Min(squint, 1f);
        return (closed, wide, squint);
    }

    /// <summary>
    /// Jerry's gaze model, per eye (freeform-cartesian blend tree with clips at radius 0.7):
    /// magnitude t = min(L1 / 0.7, 1) along the gained gaze direction, split across the two
    /// nearest axis keys. Stored back into the signed gaze vector (Out/In and Up/Down are never
    /// simultaneously nonzero in this model, so the sign-splitting encoders reproduce it exactly).
    /// </summary>
    private void ApplyJerryGaze(ref Types.Vector2 gaze)
    {
        var gx = gaze.x * masterGain * eyeLookGain;
        var gy = gaze.y * masterGain * eyeLookGain;

        var l1 = Math.Abs(gx) + Math.Abs(gy);
        if (l1 < 1e-5f)
        {
            gaze.x = 0f;
            gaze.y = 0f;
            return;
        }

        var t = Math.Min(l1 / 0.7f, 1f);
        gaze.x = t * gx / l1;  // signed: encodes Out(+)/In(-) weight
        gaze.y = t * gy / l1;  // signed: encodes Up(+)/Down(-) weight
    }

    private static float SmoothValue(float current, float target, float alpha)
    {
        if (float.IsNaN(current))
        {
            return target;
        }

        return current + (target - current) * alpha;
    }
}
