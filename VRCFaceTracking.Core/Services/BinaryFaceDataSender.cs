using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.OSC;
using VRCFaceTracking.Core.Params;
using VRCFaceTracking.Core.Params.Data;

namespace VRCFaceTracking.Core.Services;

public class BinaryFaceDataSender(
    ILogger<BinaryFaceDataSender> logger,
    OscQueryService oscqService,
    IOscTarget oscTarget) : IHostedService
{
    private const string OscMessageBlobAddress = "/tracking/face/v1";

    private byte[] _blobBuffer = new byte[UnifiedTrackingData.SerializedLength];
    private OscMessage? _blobMessage;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _blobMessage = OscMessage.CreateBlob(OscMessageBlobAddress, _blobBuffer, _blobBuffer.Length);

        oscqService.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != nameof(OscQueryService.AvatarInfo)) return;

            EvaluateSubscription();
        };
        oscTarget.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != nameof(IOscTarget.UseNativeFaceTracking)) return;

            EvaluateSubscription();
        };

        EvaluateSubscription();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        UnifiedTracking.OnUnifiedDataUpdated -= OnDataUpdated;
        return Task.CompletedTask;
    }

    // Native face data is streamed whenever the avatar advertises full face tracking support via OSCQuery,
    // or whenever the user has forced "Native VRC" mode on from the home menu.
    private bool NativeModeActive => oscTarget.UseNativeFaceTracking || oscqService.AvatarInfo.FullFaceTracking;

    private void EvaluateSubscription()
    {
        var isAlreadySubscribed = UnifiedTracking.OnUnifiedDataUpdated.GetInvocationList().Any(x => x.Target == this);
        var shouldBeSubscribed = NativeModeActive;
        ParameterSenderService.NativeStreamingActive = shouldBeSubscribed;

        if (shouldBeSubscribed && !isAlreadySubscribed)
        {
            logger.LogInformation(
                "Native face data stream ({0}) engaged: {1}",
                OscMessageBlobAddress,
                oscTarget.UseNativeFaceTracking ? "enabled manually from home menu" : "avatar reports full face tracking support");
            UnifiedTracking.OnUnifiedDataUpdated += OnDataUpdated;
        }
        else if (!shouldBeSubscribed && isAlreadySubscribed)
        {
            logger.LogInformation("Native face data stream ({0}) disengaged.", OscMessageBlobAddress);
            UnifiedTracking.OnUnifiedDataUpdated -= OnDataUpdated;
        }
    }

    private void OnDataUpdated(UnifiedTrackingData data)
    {
        if (_blobMessage == null) return;
        data.CopyTo(ref _blobBuffer);
        ParameterSenderService.Enqueue(_blobMessage);
    }
}
