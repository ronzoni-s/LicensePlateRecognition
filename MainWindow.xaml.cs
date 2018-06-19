using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
//Flurl
using Flurl.Http;

namespace BlurBehindDemo
{
	internal enum AccentState
	{
		ACCENT_DISABLED = 0,
		ACCENT_ENABLE_GRADIENT = 1,
		ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
		ACCENT_ENABLE_BLURBEHIND = 3,
		ACCENT_INVALID_STATE = 4
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct AccentPolicy
	{
		public AccentState AccentState;
		public int AccentFlags;
		public int GradientColor;
		public int AnimationId;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct WindowCompositionAttributeData
	{
		public WindowCompositionAttribute Attribute;
		public IntPtr Data;
		public int SizeOfData;
	}

	internal enum WindowCompositionAttribute
	{
		// ...
		WCA_ACCENT_POLICY = 19
		// ...
	}

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		[DllImport("user32.dll")]
		internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

		public MainWindow()
		{
			InitializeComponent();
            Submit.Click += LoginFunction;
            Close.Click += CloseWindow;
            Minimize.Click += MinimizeWindow;
            Username.Focus();
        }
		
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			EnableBlur();
		}

        internal void EnableBlur()
        {
            var windowHelper = new WindowInteropHelper(this);

            var accent = new AccentPolicy();
            accent.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;

            var accentStructSize = Marshal.SizeOf(accent);

            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData();
            data.Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY;
            data.SizeOfData = accentStructSize;
            data.Data = accentPtr;

            SetWindowCompositionAttribute(windowHelper.Handle, ref data);

            Marshal.FreeHGlobal(accentPtr);
        }
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
        private void CloseWindow(object sender,RoutedEventArgs e)
        {
            this.Close();
        }
        private void MinimizeWindow(object sender,RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        public void LoginFunction(object sender, RoutedEventArgs e)
        {
            if (Username.Text != "" && Password.Password != "")
            {
                try
                {
                    Api<Login> data = ("http://api.ipark.altervista.org/signin").PostJsonAsync(new { email = Username.Text, password = Password.Password }).ReceiveJson<Api<Login>>().Result;
                    if (data.meta.code == 200 && data.response.user == 'G')
                    {
                        Window1 nextWindow = new Window1(data.response.token);
                        nextWindow.Show();
                        this.Close();
                    }
                    else
                    {
                        Error.Content = "Unauthorized";
                    }
                }
                catch {
                    Error.Content = "An error occurred";
                }
            }
            else
            {
                Error.Content = "Something's missing";
            }
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
        public class Login
        {
            public string token { get; set; }
            public char user { get; set; }
        }
    }

}
