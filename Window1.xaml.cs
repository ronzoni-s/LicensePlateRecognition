using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading;
using System.Diagnostics;
//LiveCharts
using LiveCharts;
//Flurl
using Flurl.Http;
using Flurl;
//OpenALPR
using openalprnet;
//EMGU
using Emgu.CV;
using Emgu.CV.Structure;

namespace BlurBehindDemo
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    /// 
    public partial class Window1 : Window
    {
        private string _token;
        private List<Stream> _streams;
        private List<int> _inputs;
        public int _camera { get; set; }
        public Window1(string token)
        {
            InitializeComponent();
            _token = token;
            _streams = new List<Stream>();
            _inputs = new List<int>();
            this.Closing += WindowClosing;
            this.Closed += WindowClosed;
            Close.Click += CloseWindow;
            Minimize.Click += MinimizeWindow;
            InitializePie();
            WriteLog("Press on the white images to select the input device","Main");
        }
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
        public void SelectInputDevice(object sender, RoutedEventArgs e)
        {
            Window2 choice = new Window2(_inputs);
            choice._caller = this;
            if (choice.ShowDialog().Value)
            {
                if (!_inputs.Contains(_camera)) {
                    _inputs.Add(_camera);
                    if ((string)((Button)sender).Tag == "entrance")
                    {
                        _streams.Add(new Stream((string)((Button)sender).Tag, _camera, DisplayFrame, WriteLog, LogResult, UpdatePie, _token));
                    }
                    else
                    {
                        _streams.Add(new Stream((string)((Button)sender).Tag, _camera, DisplayFrame, WriteLog, LogResult, UpdatePie, _token, StreamType.OUT));
                    }
                    ComponentDispatcher.ThreadIdle += _streams.Last().Grab;
                    _streams.Last().Start();
                    ((Button)sender).Click -= SelectInputDevice;
                }
            }
        }
        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void MinimizeWindow(object sender, RoutedEventArgs e)
        {
            WindowState = System.Windows.WindowState.Minimized;
        }
        private void WindowClosing(object sender,System.ComponentModel.CancelEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Close the program?", "Are you sure?", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
        }
        private void WindowClosed(object sender, EventArgs e)
        {
            foreach (Stream stream in _streams)
            {
                stream.Close();
            }
        }
        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        public static BitmapSource ToBitmapSource(Bitmap image)
        {
            using (image)
            {
                IntPtr ptr = image.GetHbitmap(); //obtain the Hbitmap

                BitmapSource bs = System.Windows.Interop
                  .Imaging.CreateBitmapSourceFromHBitmap(
                  ptr,
                  IntPtr.Zero,
                  Int32Rect.Empty,
                  System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(ptr); //release the HBitmap
                return bs;
            }
        }
        public delegate void DisplayFrameDelegate(Bitmap bitmap, string name);
        private void DisplayFrame(Bitmap bitmap, string name)
        {
            ((System.Windows.Controls.Image)streams.FindName(name)).Source = ToBitmapSource(bitmap);
        }
        private void InitializePie()
        {
            try
            {
                Api<Occupied> data = "http://api.ipark.altervista.org/info/occupied".SetQueryParams(new { token = _token }).GetJsonAsync<Api<Occupied>>().Result;
                Func<ChartPoint, string> labelPoint = chartPoint => string.Format("{0} ({1:P})", chartPoint.Y, chartPoint.Participation);
                places.Series[0].Values = new ChartValues<int> { data.response.total-data.response.occupied };
                places.Series[1].Values = new ChartValues<int> { data.response.occupied };
                places.Series[0].LabelPoint = labelPoint;
                places.Series[1].LabelPoint = labelPoint;
            }
            catch (Exception exception)
            {
                WriteLog(exception.Message, "Pie", LogType.ERROR);
            }
        }
        public delegate void UpdatePieDelegate(UpdateType type);
        private void UpdatePie(UpdateType type)
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    UpdatePieDelegate DI = new UpdatePieDelegate(UpdatePie);
                    Dispatcher.Invoke(DI, new object[] { type });
                }
                else
                {
                    if (type == UpdateType.ADD)
                    {
                        places.Series[0].Values = new ChartValues<int> { Convert.ToInt32(places.Series[0].Values[0]) - 1 };
                        places.Series[1].Values = new ChartValues<int> { Convert.ToInt32(places.Series[1].Values[0]) + 1 };
                    }
                    else
                    {
                        places.Series[0].Values = new ChartValues<int> { Convert.ToInt32(places.Series[0].Values[0]) + 1 };
                        places.Series[1].Values = new ChartValues<int> { Convert.ToInt32(places.Series[1].Values[0]) - 1 };
                    }
                }
            }catch(Exception exception)
            {
                WriteLog(exception.Message, "Pie", LogType.ERROR);
            }
        }
        public delegate void WriteLogDelegate(string message, string sender, LogType type = LogType.INFO);
        public void WriteLog(string message, string sender, LogType type = LogType.INFO)
        {
            if (!Dispatcher.CheckAccess())
            {
                WriteLogDelegate DI = new WriteLogDelegate(WriteLog);
                Dispatcher.Invoke(DI, new object[] { message, sender, type });
            }
            else
            {
                log.AppendText(DateTime.Now.ToString("hh:mm:ss tt") + " ");
                log.AppendText("[" + sender + "] ");
                log.AppendText("[" + type + "] ");
                log.AppendText(message);
                log.AppendText(Environment.NewLine);
            }
        }
        public delegate void LogResultDelegate(Registered message, string name);
        public void LogResult(Registered message, string name)
        {
            if (!Dispatcher.CheckAccess())
            {
                LogResultDelegate DI = new LogResultDelegate(LogResult);
                Dispatcher.Invoke(DI, new object[] { message, name });
            }
            else
            {
                ((ListView)streams.FindName(name + "List")).Items.Add(new MyItem { Time = message.date, Plate = message.plate, User = message.user });
            }
        }
        public class Stream
        {
            private DisplayFrameDelegate _DisplayFrame;
            private WriteLogDelegate _WriteLog;
            private LogResultDelegate _LogResult;
            private UpdatePieDelegate _UpdatePie;
            private string _name;
            private int _source;
            private VideoCapture _flow;
            private string _token;
            private ConcurrentQueue<Bitmap> _framesQueue;
            private Rectangle _roi;
            private Bgr _color;
            private object _lock;
            private bool _kill;
            private StreamType _type;
            private Thread _thread;
            public Stream(string name, int source, DisplayFrameDelegate DisplayFrame, WriteLogDelegate WriteLog, LogResultDelegate LogResult, UpdatePieDelegate UpdatePie, string token, StreamType type = StreamType.IN)
            {
                try
                {
                    _source = source;
                    _flow = new VideoCapture(_source);
                    if (!_flow.IsOpened)
                    {
                        throw new Exception("Couldn't find the selected streaming input for " + name);
                    }
                    _token = token;
                    _name = name;
                    _type = type;
                    _color = new Bgr(51, 255, 255);
                    _roi = new Rectangle(50, 50, 300, 300);
                    _lock = new object();
                    _kill = false;
                    _framesQueue = new ConcurrentQueue<Bitmap>();
                    _DisplayFrame = DisplayFrame;
                    _WriteLog = WriteLog;
                    _LogResult = LogResult;
                    _UpdatePie = UpdatePie;
                    _thread = null;
                }
                catch (Exception exception)
                {
                    throw exception;
                }
            }
            public void Grab(object sender, EventArgs e)
            {
                Image<Bgr, Byte> frame = _flow.QueryFrame().ToImage<Bgr, Byte>();
                if (frame != null)
                {
                    frame.Draw(_roi, _color, 2);
                    _DisplayFrame(frame.ToBitmap(), _name);
                    if (_framesQueue.IsEmpty)
                    {
                        _framesQueue.Enqueue(frame.Copy(_roi).ToBitmap());
                    }
                }
            }
            public void Start()
            {
                if (_type == StreamType.IN)
                {
                    _thread = new Thread(() => EntranceExaminer());
                }
                else
                {
                    _thread = new Thread(() => ExitExaminer());
                }
                _thread.IsBackground = true;
                _thread.Start();
            }
            public void Close()
            {
                if (_flow.IsOpened)
                {
                    _flow.Dispose();
                }
                lock (_lock)
                {
                    _kill = true;
                }
                if (_thread!=null && _thread.IsAlive)
                {
                    _thread.Join();
                }
            }
            private void EntranceExaminer()
            {
                AlprNet alpr = new AlprNet("us", "openalpr.conf", "runtime_dir");
                alpr.TopN = 1;
                HashSet<string> recognized = new HashSet<string>();
                Stopwatch bar = null;
                while (true)
                {
                    Bitmap bitmap;
                    lock (_lock)
                    {
                        if (_kill)
                        {
                            break;
                        }
                    }
                    if (bar != null && bar.IsRunning)
                    {
                        if (bar.ElapsedMilliseconds < 10000)
                        {
                            continue;
                        }
                        else
                        {
                            bar.Stop();
                            _framesQueue.TryDequeue(out bitmap);
                            _WriteLog("Bar closed", _name);
                        }
                    }
                    if (_framesQueue.TryDequeue(out bitmap))
                    {
                        bool entered = false;
                        AlprResultsNet results = alpr.Recognize(bitmap);
                        if (results.Plates.Count != 0)
                        {
                            AlprPlateNet plate = results.Plates[0].BestPlate;
                            if (recognized.Add(plate.Characters))
                            {
                                _WriteLog(plate.Characters, _name);
                                try
                                {
                                    Api<Plate> data = ("http://api.ipark.altervista.org/plate/" + plate.Characters).SetQueryParams(new { token = _token }).GetJsonAsync<Api<Plate>>().Result;
                                    if (data.meta.code == 200)
                                    {
                                        if (data.response.blacklisted)
                                        {
                                            _WriteLog(plate.Characters + " is blacklisted", _name, LogType.WARNING);
                                            recognized.Remove(recognized.Last());
                                        }
                                        else
                                        {
                                            Api<Registered> internalData = ("http://api.ipark.altervista.org/history/register/entrance/" + data.response.plate).SetQueryParams(new { token = _token }).PostJsonAsync(null).ReceiveJson<Api<Registered>>().Result;
                                            if (internalData.meta.code == 201)
                                            {
                                                _UpdatePie(UpdateType.ADD);
                                                _LogResult(internalData.response, _name);
                                                entered = true;
                                            }
                                        }
                                    }
                                }
                                catch (Exception exception)
                                {
                                    _WriteLog(exception.Message, _name, LogType.ERROR);
                                }
                            }
                        }
                        if (entered)
                        {
                            recognized.Clear();
                            bar = new Stopwatch();
                            bar.Start();
                            _WriteLog("Bar raised", _name);
                        }
                    }
                }
            }

            private void ExitExaminer()
            {
                AlprNet alpr = new AlprNet("us", "openalpr.conf", "runtime_dir");
                alpr.TopN = 1;
                HashSet<string> recognized = new HashSet<string>();
                Stopwatch bar = null;
                while (true)
                {
                    Bitmap bitmap;
                    lock (_lock)
                    {
                        if (_kill)
                        {
                            break;
                        }
                    }
                    if (bar != null && bar.IsRunning)
                    {
                        if (bar.ElapsedMilliseconds < 10000)
                        {
                            continue;
                        }
                        else
                        {
                            bar.Stop();
                            _framesQueue.TryDequeue(out bitmap);
                            _WriteLog("Bar closed", _name);
                        }
                    }
                    if (_framesQueue.TryDequeue(out bitmap))
                    {
                        bool left = false;
                        AlprResultsNet results = alpr.Recognize(bitmap);
                        if (results.Plates.Count != 0)
                        {
                            AlprPlateNet plate = results.Plates[0].BestPlate;
                            if (recognized.Add(plate.Characters))
                            {
                                _WriteLog(plate.Characters, _name);
                                try
                                {
                                    Api<HistoryLast> data = ("http://api.ipark.altervista.org/history/" + plate.Characters + "/last").SetQueryParams(new { token = _token }).GetJsonAsync<Api<HistoryLast>>().Result;
                                    if (data.meta.code == 200)
                                    {
                                        if (data.response.payment == null)
                                        {
                                            _WriteLog(plate.Characters + " has not paid yet", _name, LogType.WARNING);
                                            recognized.Remove(recognized.Last());
                                        }
                                        else
                                        {
                                            Api<Registered> internalData = ("http://api.ipark.altervista.org/history/register/exit/" + data.response.plate).SetQueryParams(new { token = _token }).PutJsonAsync(null).ReceiveJson<Api<Registered>>().Result;
                                            if (internalData.meta.code == 200)
                                            {
                                                _UpdatePie(UpdateType.SUB);
                                                _LogResult(internalData.response, _name);
                                                left = true;
                                            }
                                        }
                                    }
                                }
                                catch (Exception exception)
                                {
                                    _WriteLog(exception.ToString(), _name, LogType.ERROR);
                                }
                            }
                        }
                        if (left)
                        {
                            recognized.Clear();
                            bar = new Stopwatch();
                            bar.Start();
                            _WriteLog("Bar raised", _name);
                        }
                    }
                }
            }
        }

        public enum LogType
        {
            INFO, ERROR, WARNING
        }
        public enum StreamType
        {
            IN, OUT
        }
        public enum UpdateType
        {
            ADD,SUB
        }
        public class Api<T>
        {
            public Meta meta;
            public T response;
        }
        public class Meta
        {
            public int code { get; set; }
            public string msg { get; set; }
        }
        public class Occupied
        {
            public short occupied { get; set; }
            public short total { get; set; }
        }
        public class Plate
        {
            public string plate { get; set; }
            public string user { get; set; }
            public bool blacklisted { get; set; }
        }
        public class Registered
        {
            public string plate { get; set; }
            public string user { get; set; }
            public string date { get; set; }

        }
        public class HistoryLast
        {
            public string plate { get; set; }
            public string user { get; set; }
            public string entree { get; set; }
            public string egress { get; set; }
            public string payment { get; set; }
        }
        public class MyItem
        {
            public string Time { get; set; }
            public string Plate { get; set; }
            public string User { get; set; }
        }
    }
}
