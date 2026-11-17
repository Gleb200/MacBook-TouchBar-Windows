using Avalonia.DfrFrameBuffer.Device.Hid;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;
using System;
using System.IO;
using System.Linq;

namespace Avalonia.DfrFrameBuffer
{
    public class Digitizer
    {
        private readonly double _scale;
        private readonly double _width;
        private readonly double _height;
        private readonly Vector _dpi;

        private HidDevice _digitizer;
        private ReportDescriptor _reportDescr;
        private HidStream _hidStream;

        private HidDeviceInputReceiver _hidDeviceInputReceiver;
        private DeviceItemInputParser _hiddeviceInputParser;

        // Custom touch interception hooks (e.g. For dragging mascot directly)
        public static Func<double, bool> TouchInterceptFilter;
        public static Action<double, int> TouchRawEvent; // (scaledX, type: 0=Down, 1=Move, 2=Up)
        public static Action TouchNonCompanionEvent; // User is touching Touch Bar controls

        // Internal touch tracking state
        private bool _isDown = false;
        private bool _isIntercepted = false;
        private double _lastActiveX = 0;
        private bool _pendingMoveInFlight = false;
        private double _latestMoveX = 0;
        private readonly object _moveLock = new object();

        public event Action<RawInputEventArgs> Event;

        public Digitizer(double physicalWidth, double physicalHeight, Vector? dpi = null)
        {
            // 96 DPI is standard 100% scale
            _dpi = dpi ?? new Vector(192, 192);
            _scale = 96 / _dpi.X;
            _width = physicalWidth * _scale;
            _height = physicalHeight * _scale;

            // Discover DFR digitizer device with startup retry
            for (int retry = 0; retry < 15; retry++)
            {
                _digitizer = DeviceList.Local.GetHidDeviceOrNull(0x05ac, 0x8302);
                if (_digitizer != null) break;
                System.Threading.Thread.Sleep(500);
            }

            if (_digitizer == null)
            {
                throw new Exception("iBridge HID digitizer not found");
            }

            _reportDescr = _digitizer.GetReportDescriptor();
        }

        public void Start()
        {
            bool opened = false;
            for (int retry = 0; retry < 10; retry++)
            {
                if (_digitizer.TryOpen(out _hidStream))
                {
                    opened = true;
                    break;
                }
                System.Threading.Thread.Sleep(500);
            }

            if (opened)
            {
                _hidDeviceInputReceiver = _reportDescr.CreateHidDeviceInputReceiver();
                _hiddeviceInputParser = _reportDescr.DeviceItems[0].CreateDeviceItemInputParser();
                _hidDeviceInputReceiver.Received += OnDigitizerInputReceived;
                _hidDeviceInputReceiver.Start(_hidStream);
            }
            else
            {
                throw new Exception("Failed to open iBridge HID digitizer");
            }
        }

        private void OnDigitizerInputReceived(object sender, EventArgs e)
        {
            try
            {
                var inputReportBuffer = new byte[_digitizer.GetMaxInputReportLength()];
                while (_hidDeviceInputReceiver.TryRead(inputReportBuffer, 0, out Report report))
                {
                    if (_hiddeviceInputParser.TryParseReport(inputReportBuffer, 0, report))
                    {
                        if (!_hiddeviceInputParser.HasChanged) continue;

                        bool hasActive = false;
                        double activeScaledX = 0;
                        int valCount = _hiddeviceInputParser.ValueCount;
                        int j = -1;
                        for (int i = 0; i < valCount; i++)
                        {
                            var data = _hiddeviceInputParser.GetValue(i);
                            if (data.Usages.FirstOrDefault() == VendorUsage.FingerIdentifier)
                            {
                                j++;
                            }
                            else
                            {
                                continue;
                            }

                            if (j >= 11 || i + 4 >= valCount) break;

                            var fingerTapData1 = _hiddeviceInputParser.GetValue(i + 1);
                            bool status = (fingerTapData1.GetPhysicalValue() != 0);
                            if (status && !hasActive)
                            {
                                var xData = _hiddeviceInputParser.GetValue(i + 3);
                                double xMax = (xData.DataItem != null && xData.DataItem.PhysicalMaximum > 0)
                                    ? xData.DataItem.PhysicalMaximum : 32767.0;
                                double xVal = xData.GetPhysicalValue();
                                double pct = (xMax > 0) ? (xVal / xMax) : 0;
                                activeScaledX = Math.Max(0.0, Math.Min(_width, pct * _width));
                                hasActive = true;
                            }
                        }

                        DispatchParsedTouch(hasActive, activeScaledX);
                    }
                }
            }
            catch (Exception ex)
            {
                try { File.AppendAllText("digitizer_errors.log", $"[{DateTime.UtcNow}] Read error: {ex}\n"); } catch { }
            }
        }

        private void DispatchParsedTouch(bool hasActive, double scaledX)
        {
            if (hasActive)
            {
                if (!_isDown)
                {
                    _isDown = true;
                    _lastActiveX = scaledX;
                    Dispatcher.UIThread.Post(() => ProcessDown(scaledX));
                }
                else
                {
                    _lastActiveX = scaledX;
                    lock (_moveLock)
                    {
                        _latestMoveX = scaledX;
                        if (!_pendingMoveInFlight)
                        {
                            _pendingMoveInFlight = true;
                            Dispatcher.UIThread.Post(ProcessPendingMove);
                        }
                    }
                }
            }
            else
            {
                if (_isDown)
                {
                    _isDown = false;
                    double upX = _lastActiveX;
                    Dispatcher.UIThread.Post(() => ProcessUp(upX));
                }
            }
        }

        private void ProcessPendingMove()
        {
            double moveX;
            lock (_moveLock)
            {
                moveX = _latestMoveX;
                _pendingMoveInFlight = false;
            }
            ProcessMove(moveX);
        }

        private void ProcessDown(double scaledX)
        {
            try
            {
                bool intercept = (TouchInterceptFilter != null && TouchInterceptFilter(scaledX));
                _isIntercepted = intercept;

                if (_isIntercepted)
                {
                    TouchRawEvent?.Invoke(scaledX, 0); // Down
                }
                else
                {
                    TouchNonCompanionEvent?.Invoke();
                    Event?.Invoke(new RawMouseEventArgs(
                        BridgeFrameBufferPlatform.MouseDevice,
                        BridgeFrameBufferPlatform.Timestamp,
                        BridgeFrameBufferPlatform.TopLevel.InputRoot,
                        RawMouseEventType.LeftButtonDown,
                        new Point(scaledX, _height / 2),
                        default));
                }
            }
            catch { }
        }

        private void ProcessMove(double scaledX)
        {
            try
            {
                if (_isIntercepted)
                {
                    TouchRawEvent?.Invoke(scaledX, 1); // Move
                }
                else
                {
                    TouchNonCompanionEvent?.Invoke();
                    Event?.Invoke(new RawMouseEventArgs(
                        BridgeFrameBufferPlatform.MouseDevice,
                        BridgeFrameBufferPlatform.Timestamp,
                        BridgeFrameBufferPlatform.TopLevel.InputRoot,
                        RawMouseEventType.Move,
                        new Point(scaledX, _height / 2),
                        InputModifiers.LeftMouseButton));
                }
            }
            catch { }
        }

        private void ProcessUp(double scaledX)
        {
            try
            {
                lock (_moveLock)
                {
                    _pendingMoveInFlight = false;
                }

                if (_isIntercepted)
                {
                    _isIntercepted = false;
                    TouchRawEvent?.Invoke(scaledX, 2); // Up
                }
                else
                {
                    Event?.Invoke(new RawMouseEventArgs(
                        BridgeFrameBufferPlatform.MouseDevice,
                        BridgeFrameBufferPlatform.Timestamp,
                        BridgeFrameBufferPlatform.TopLevel.InputRoot,
                        RawMouseEventType.LeftButtonUp,
                        new Point(scaledX, _height / 2),
                        default));
                }

                // Always release capture so Avalonia controls never freeze/trap mouse state
                try
                {
                    BridgeFrameBufferPlatform.MouseDevice?.Capture(null);
                }
                catch { }
            }
            catch { }
        }
    }
}
