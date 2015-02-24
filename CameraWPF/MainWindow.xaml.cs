using DirectShowLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using camera;
using System.Windows.Controls;

namespace CameraWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DsDevice camera = null;
        public byte[] m_ip = null;

        private Capture captura;
      
        public MainWindow()
        {
            InitializeComponent();
        }

        private void start(object sender, EventArgs e)
        {
            camera = Capture.DetectCameras()[0];
            captura = new Capture(camera, 320, 240);
            captura.previewElement = preview;
            captura.Start();
        }

        public void Dispose()
        {
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            snap.SetImage(captura.GetFrameAsImage());
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            captura.SaveFrameToFile("foto.jpeg");
        }
    }
}
