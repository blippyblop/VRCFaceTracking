using System.ComponentModel;

namespace VRCFaceTracking.Core.Contracts;

public interface IOscTarget : INotifyPropertyChanged
{
    public bool IsConnected
    {
        get;
        set;
    }
    
    public int InPort
    {
        get;
        set;
    }
    
    public int OutPort
    {
        get;
        set;
    }
    
    public string DestinationAddress
    {
        get;
        set;
    }

    /// <summary>
    /// When true, VRCFaceTracking streams all face data as a single 102-key blob to /tracking/face/v1
    /// (VRChat's native face tracking OSC input), instead of individual avatar parameters.
    /// </summary>
    public bool UseNativeFaceTracking
    {
        get;
        set;
    }
}