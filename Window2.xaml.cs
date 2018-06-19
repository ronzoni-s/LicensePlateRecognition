using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
//DirectShow
using DirectShowLib;

namespace BlurBehindDemo
{
    /// <summary>
    /// Interaction logic for Window2.xaml
    /// </summary>
    public partial class Window2 : Window
    {
        private List<KeyValuePair<int, string>> _listCamerasData;
        public Window1 _caller { get; set; }
        public Window2(List<int> ignore)
        {
            InitializeComponent();
            Close.Click += CloseWindow;
            _listCamerasData = new List<KeyValuePair<int, string>>();
            DsDevice[] systemCamereas = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            int deviceIndex = 0;
            foreach (DsDevice camera in systemCamereas)
            {
                if (!ignore.Contains(deviceIndex))
                {
                    selector.Items.Add(camera.Name);
                    _listCamerasData.Add(new KeyValuePair<int, string>(deviceIndex++, camera.Name));
                }
            }
            if (_listCamerasData.Count == 0)
            {
                error.Content = "There are no devices available";
                selector.IsEnabled = false;
                select.IsEnabled = false;
            }
        }
        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
        private void Select_Click(object sender, RoutedEventArgs e)
        {
            KeyValuePair<int, string> selected = _listCamerasData.SingleOrDefault(x => x.Value == selector.SelectedValue.ToString());
            _caller._camera = selected.Key;
            this.DialogResult = true;
        }
    }
}
