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

            // Release any previous buffer
            if (m_ip != null)
            {
                m_ip = null;
            }

            // capture image
            m_ip = captura.GetFrameBytes();
            //snap.Blah(m_ip);
           // BitmapSource b = BitmapSource.Create(captura.Width, captura.Height, 96d, 96d, PixelFormats.Bgr24, null, m_ip, captura.Stride);
            // If the image is upsidedown
            //snap.Source = b;
            //snap.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            //ScaleTransform flipTrans = new ScaleTransform();
            //flipTrans.ScaleY = -1;
            //snap.RenderTransform = flipTrans;

            //System.Windows.Controls.Image img = new System.Windows.Controls.Image();
            //img.Source = b;
            //Capture.FlipVertical(img);
            snap.SetImage(captura.GetFrameAsImage());
            
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //snap.Source = captura.GetFrameAsImage();
            //Capture.FlipVertical(snap);
            snap.Save("imagem.jpg");
        }
    }
}
