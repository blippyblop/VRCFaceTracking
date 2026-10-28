using Microsoft.Extensions.Hosting;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.OSC;
using VRCFaceTracking.Core.Params;
using VRCFaceTracking.Core.Params.Data;

namespace VRCFaceTracking.Core.Services;

public class BinaryFaceDataSender(OscQueryService oscqService, IOscTarget oscTarget) : IHostedService
{
    private byte[] _blobBuffer = new byte[UnifiedTrackingData.SerializedLength];
    private OscMessage? _blobMessage;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _blobMessage = OscMessage.CreateBlob("/tracking/face/v1", _blobBuffer, _blobBuffer.Length);

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

        if (shouldBeSubscribed && !isAlreadySubscribed)
        {
            UnifiedTracking.OnUnifiedDataUpdated += OnDataUpdated;
        }
        else if (!shouldBeSubscribed && isAlreadySubscribed)
        {
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
