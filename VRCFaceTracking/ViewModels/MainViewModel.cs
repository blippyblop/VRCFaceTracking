using System.Runtime.InteropServices.Swift;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using VRCFaceTracking.Contracts;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.OSC;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Data.Mutation;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    /// <summary>
    /// The Jerry-style exaggeration mutation, if present. Exposed for the home menu quick toggle.
    /// </summary>
    public Exaggeration? Exaggeration { get; }

    public ILibManager LibManager { get; }
    public OscQueryService ParameterOutputService { get; }
    public OscRecvService OscRecvService { get; }
    public OscSendService OscSendService { get; }
    public IOscTarget OscTarget { get; }
    private readonly IModuleDataService _moduleDataService;

    private int _messagesRecvd;
    [ObservableProperty] private int _messagesInPerSec;

    private int _messagesSent;
    [ObservableProperty] private int _messagesOutPerSec;

    [ObservableProperty] private bool _noModulesInstalled;
    
    [ObservableProperty] private bool _oscWasDisabled;

    private DispatcherTimer msgCounterTimer;

    public MainViewModel(
        ILibManager libManager,
        OscQueryService parameterOutputService,
        IModuleDataService moduleDataService,
        IOscTarget oscTarget,
        OscRecvService oscRecvService,
        OscSendService oscSendService,
        UnifiedTrackingMutator trackingMutator
        )
    {
        //Services
        LibManager = libManager;
        ParameterOutputService = parameterOutputService;
        OscTarget = oscTarget;
        OscRecvService = oscRecvService;
        OscSendService = oscSendService;
        _moduleDataService = moduleDataService;
        Exaggeration = trackingMutator._mutations.OfType<Exaggeration>().FirstOrDefault();
        
        // Message Timer
        OscRecvService.OnMessageReceived += MessageReceived;
        OscSendService.OnMessagesDispatched += MessageDispatched;
        msgCounterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        msgCounterTimer.Tick += (_, _) =>
        {
            MessagesInPerSec = _messagesRecvd;
            _messagesRecvd = 0;
            
            MessagesOutPerSec = _messagesSent;
            _messagesSent = 0;
        };
        msgCounterTimer.Start();
        
        OnNavigatedTo();
    }

    public void OnNavigatedTo()
    {
        // Modules
        var installedNewModules = _moduleDataService.GetInstalledModules();
        var installedLegacyModules = _moduleDataService.GetLegacyModules().Count();
        NoModulesInstalled = !installedNewModules.Any() && installedLegacyModules == 0;
    }
    
    private void MessageReceived(OscMessage msg) => _messagesRecvd++;
    private void MessageDispatched(int msgCount) => _messagesSent += msgCount;

    ~MainViewModel()
    {
        OscRecvService.OnMessageReceived -= MessageReceived;
        OscSendService.OnMessagesDispatched -= MessageDispatched;
        
        msgCounterTimer.Stop();
    }
}
