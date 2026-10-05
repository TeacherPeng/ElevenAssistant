using Android.AccessibilityServices;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Display;
using Android.OS;
using Android.Views;
using Android.Views.Accessibility;
using Java.Lang;

namespace ElevenAssistantV2;

[Service(Name = PackageInfo.ServiceName, Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE", Exported = true)]
[IntentFilter(["android.accessibilityservice.AccessibilityService"])]
[MetaData("android.accessibilityservice", Resource = "@xml/accessibility_service_config")]
public class ElevenAssistantV2Service : AccessibilityService
{
    private Handler? _handler;
    private Runnable? _actionRunnable;
    private bool _isActing = false;
    private BroadcastReceiver? _broadcastReceiver;
    private readonly Random _random = new();
    private int _minDelay = 3000;
    private int _maxDelay = 10000;

    public override void OnCreate()
    {
        base.OnCreate();

        _handler = new Handler(Looper.MainLooper);
        _actionRunnable = new Runnable(async () =>
        {
            try
            {
                if (_isActing)
                {
                    await PerformActionAsync();
                }
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Error("Eleven Assistant", "ActionRunnable exception: " + ex.Message);
            }
        });

        // 注册广播接收器，MainActivity通过广播控制开始和停止
        _broadcastReceiver = new ActionControlReceiver(this);
        var filter = new IntentFilter();
        filter.AddAction(PackageInfo.ActionStart);
        filter.AddAction(PackageInfo.ActionStop);
        RegisterReceiver(_broadcastReceiver, filter, ReceiverFlags.NotExported);
    }
    public override void OnDestroy()
    {
        StopElevenAssistantV2();
        if (_broadcastReceiver != null)
        {
            UnregisterReceiver(_broadcastReceiver);
            _broadcastReceiver = null;
        }
        base.OnDestroy();
    }

    public override void OnAccessibilityEvent(AccessibilityEvent? e) { }
    public override void OnInterrupt() { }

    public void StartElevenAssistantV2(int minDelay, int maxDelay)
    {
        _minDelay = minDelay;
        _maxDelay = maxDelay;

        if (!_isActing)
        {
            _isActing = true;
            _handler?.RemoveCallbacksAndMessages(null);
            _handler?.PostDelayed(_actionRunnable, 1000);
        }
    }

    public void StopElevenAssistantV2()
    {
        _isActing = false;
        // remove all pending callbacks and messages to ensure no scheduled actions remain
        _handler?.RemoveCallbacksAndMessages(null);
    }

    private long Swipe()
    {
        var gestureBuilder = new GestureDescription.Builder();

        // 依据实际屏幕分辨率按相对位置计算手势起点和终点（基准分辨率：1080x2400）
        var metrics = Resources.DisplayMetrics;
        int realW = metrics.WidthPixels;
        int realH = metrics.HeightPixels;
        const float baseW = 1080f;
        const float baseH = 2400f;

        float sxMin = 450f / baseW * realW;
        float sxMax = 550f / baseW * realW;
        float syMin = 1500f / baseH * realH;
        float syMax = 1600f / baseH * realH;
        float exMin = 450f / baseW * realW;
        float exMax = 550f / baseW * realW;
        float eyMin = 800f / baseH * realH;
        float eyMax = 1000f / baseH * realH;

        float startXf = (float)_random.NextDouble() * (sxMax - sxMin) + sxMin;
        float startYf = (float)_random.NextDouble() * (syMax - syMin) + syMin;
        float endXf = (float)_random.NextDouble() * (exMax - exMin) + exMin;
        float endYf = (float)_random.NextDouble() * (eyMax - eyMin) + eyMin;

        // 生成手势轨迹
        var path = new Android.Graphics.Path();
        path.MoveTo(startXf, startYf);

        int steps = _random.Next(18, 32);
        double freq = _random.NextDouble() * 2.0 + 2.0;
        float amplitude = _random.Next(6, 16);

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps; // 0..1

            // linear interpolation between start and end
            float baseX = startXf + (endXf - startXf) * t;
            float baseY = startYf + (endYf - startYf) * t;

            // sine wave on X to simulate tremor, plus small random noise on both axes
            double sine = System.Math.Sin(t * freq * 2.0 * System.Math.PI);
            float jitterX = (float)(sine * amplitude + (_random.NextDouble() * 4.0 - 2.0));
            float jitterY = (float)(_random.NextDouble() * 4.0 - 2.0);

            float px = baseX + jitterX;
            float py = baseY + jitterY;

            path.LineTo(px, py);
        }

        long duration = _random.Next(250, 401);
        var stroke = new GestureDescription.StrokeDescription(path, 0, duration);
        gestureBuilder.AddStroke(stroke);

        Android.Util.Log.Debug("Elevent Assistant", "Dispatching swipe gesture");
        DispatchGesture(gestureBuilder.Build(), null, null);

        return _random.Next(_minDelay, _maxDelay + 1);
    }

    private async Task PerformActionAsync()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.N) return;

        try
        {
            var nextDelay = Swipe();
            _handler?.PostDelayed(_actionRunnable, nextDelay); return;
        }
        catch (System.Exception ex)
        {
            Android.Util.Log.Error("Eleven Assistant", "Action failed: " + ex.Message);
        }
    }

    // 内部广播接收器
    private class ActionControlReceiver(ElevenAssistantV2Service service) : BroadcastReceiver
    {
        private readonly ElevenAssistantV2Service _service = service;

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == PackageInfo.ActionStart)
            {
                int minDelay = intent.GetIntExtra(PackageInfo.ExtraMinDelay, 4000);
                int maxDelay = intent.GetIntExtra(PackageInfo.ExtraMaxDelay, 10000);
                _service.StartElevenAssistantV2(minDelay, maxDelay);
            }
            else if (intent?.Action == PackageInfo.ActionStop)
            {
                _service.StopElevenAssistantV2();
            }
        }
    }

    private class ScreenshotCallback : Java.Lang.Object, AccessibilityService.ITakeScreenshotCallback
    {
        private readonly Action<AccessibilityService.ScreenshotResult> _onSuccess;
        private readonly Action<int> _onFailure;

        public ScreenshotCallback(Action<AccessibilityService.ScreenshotResult> onSuccess, Action<int> onFailure)
        {
            _onSuccess = onSuccess;
            _onFailure = onFailure;
        }

        public void OnSuccess(AccessibilityService.ScreenshotResult screenshot) => _onSuccess?.Invoke(screenshot);

        public void OnFailure(int errorCode) => _onFailure?.Invoke(errorCode);
    }
}
