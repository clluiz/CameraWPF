using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace camera
{
    public class Capture : IDisposable, ISampleGrabberCB
    {

        #region Filtros
        private IFilterGraph2 filterGraph = null;
        private IBaseFilter captureFilter;
        private IBaseFilter smartTee;
        private IBaseFilter renderFilter;
        private ISampleGrabber grabFilter;
        #endregion

        // altura e largura da imagem a ser capturada
        private int width = 0;
        public int Width
        {
            get
            {
                return width;
            }
        }

        private int height = 0;
        public int Height
        {
            get
            {
                return height;
            }
        }

        private int stride;
        public int Stride
        {
            get
            {
                return stride;
            }
        }

        private byte[] buffer = null;
        private DsDevice camera;
        private Boolean capturar;
        private IVMRWindowlessControl9 vmr9Control;
        private int previewWidth;
        private int previewHeight;
        public System.Windows.Forms.Control previewElement;

        private ManualResetEvent m_PictureReady = null;

        /// <summary>
        /// Construtor
        /// </summary>
        /// <param name="camera">Dispositivo de captura</param>
        /// <param name="picWidth">Largura da foto</param>
        /// <param name="picHeight">Altura da foto</param>
        public Capture(DsDevice camera, int picWidth, int picHeight)
        {
            this.previewWidth = picWidth;
            this.previewHeight = picHeight;
            this.camera = camera;
            m_PictureReady = new ManualResetEvent(false);
        }

        /// <summary>
        /// Encontra quantas cameras tem no dispositivo
        /// </summary>
        public static DsDevice[] DetectCameras()
        {
            return DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
        }

        /// <summary>
        /// Inverte a imagem na vertical
        /// </summary>
        /// <param name="image">Imagem a ser invertida</param>
        /// <returns></returns>
        public static System.Windows.Controls.Image FlipVertical(System.Windows.Controls.Image image)
        {
            image.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            ScaleTransform flipTrans = new ScaleTransform();
            flipTrans.ScaleY = -1;
            image.RenderTransform = flipTrans;
            return image;
        }

        /// <summary>
        /// Constrói o grafo do Directshow
        /// </summary>
        /// <param name="preview">Componente que receberá as imagens</param>
        private void BuildGraph(System.Windows.Forms.Control preview)
        {
            try
            {
                int hr;
                filterGraph = new FilterGraph() as IFilterGraph2;
                hr = filterGraph.AddSourceFilterForMoniker(camera.Mon, null, camera.Name, out this.captureFilter);
                DsError.ThrowExceptionForHR(hr);

                IPin pinCaptura = DsFindPin.ByCategory(captureFilter, PinCategory.Capture, 0);

                // pega as resolucoes disponiveis
                GetMediaTypes(pinCaptura);
                //
                AMMediaType media = medias[10];

                VideoInfoHeader videoInfoHeader = (VideoInfoHeader)Marshal.PtrToStructure(media.formatPtr, typeof(VideoInfoHeader));

                //resolucoesDisponiveis = GetResolutions(pinCaptura);

                //MessageBox.Show(videoInfoHeader.BmiHeader.Width.ToString() + "x" + videoInfoHeader.BmiHeader.Height.ToString() + "x" + videoInfoHeader.BmiHeader.BitCount.ToString());

                (pinCaptura as IAMStreamConfig).SetFormat(media);

                // filtro em que será ligada a saída do captureFilter
                smartTee = (IBaseFilter)new SmartTee();


                hr = filterGraph.AddFilter(smartTee, "SmartTee");
                DsError.ThrowExceptionForHR(hr);

                IPin pinPreview = DsFindPin.ByName(smartTee, "Preview");
                IPin pinInput = DsFindPin.ByDirection(smartTee, PinDirection.Input, 0);

                hr = filterGraph.Connect(pinCaptura, pinInput);
                DsError.ThrowExceptionForHR(hr);

                // adiciona o filtro para renderizar o preview
                renderFilter = (IBaseFilter)new VideoMixingRenderer9();

                ConfigurePreview(renderFilter, preview);

                hr = filterGraph.AddFilter(renderFilter, "Renderer");
                DsError.ThrowExceptionForHR(hr);

                IPin pinEntradaRenderer = DsFindPin.ByDirection(renderFilter, PinDirection.Input, 0);
                hr = filterGraph.Connect(pinPreview, pinEntradaRenderer);
                DsError.ThrowExceptionForHR(hr);

                int Width, Height, ARWidth, ARHeight;
                hr = vmr9Control.GetNativeVideoSize(out Width, out Height, out ARWidth, out ARHeight);
                DsError.ThrowExceptionForHR(hr);

                // adiciona o SampleGrabber para captuar os frames
                grabFilter = new SampleGrabber() as ISampleGrabber;

                ConfigureSampleGrabber(grabFilter, media);

                hr = filterGraph.AddFilter(grabFilter as IBaseFilter, "Sample Grabber");
                DsError.ThrowExceptionForHR(hr);

                IPin saidaSmartTee = DsFindPin.ByName(smartTee, "Capture");
                IPin entradaGrabber = DsFindPin.ByDirection(grabFilter as IBaseFilter, PinDirection.Input, 0);

                hr = filterGraph.Connect(saidaSmartTee, entradaGrabber);

                DsError.ThrowExceptionForHR(hr);
                SaveSizeInfo(grabFilter);

                hr = grabFilter.SetBufferSamples(false);
                DsError.ThrowExceptionForHR(hr);

                hr = grabFilter.SetOneShot(false);
                DsError.ThrowExceptionForHR(hr);

                hr = grabFilter.SetCallback(this, 1);
                DsError.ThrowExceptionForHR(hr);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private List<Resolution> resolucoesDisponiveis;
        private List<AMMediaType> medias;

        private void GetMediaTypes(IPin pin)
        {
            IAMStreamConfig streamConfig = pin as IAMStreamConfig;
            int iC = 0, iS = 0;
            streamConfig.GetNumberOfCapabilities(out iC, out iS);

            IntPtr ptr;
            ptr = Marshal.AllocCoTaskMem(iS);

            medias = new List<AMMediaType>();
            for (var i = 0; i < iC; i++)
            {
                AMMediaType m;
                streamConfig.GetStreamCaps(i, out m, ptr);
                medias.Add(m);
            }
        }

        private List<Resolution> GetResolutions(IPin pin)
        {
            try
            {
                int hr;
                int max = 0;
                int bitCount = 0;
                List<Resolution> resolutions = new List<Resolution>();
                VideoInfoHeader v = new VideoInfoHeader();
                IEnumMediaTypes mediaTypeEnum;
                hr = pin.EnumMediaTypes(out mediaTypeEnum);

                AMMediaType[] mediaTypes = new AMMediaType[1];
                IntPtr fetched = IntPtr.Zero;

                hr = mediaTypeEnum.Next(1, mediaTypes, fetched);
                String rels = "";

                while (fetched != null && mediaTypes[0] != null)
                {
                    Marshal.PtrToStructure(mediaTypes[0].formatPtr, v);
                    if (v.BmiHeader.Size != 0 && v.BmiHeader.BitCount != 0)
                    {
                        if (v.BmiHeader.BitCount > bitCount)
                        {
                            max = 0;
                            bitCount = v.BmiHeader.BitCount;
                        }
                        resolutions.Add(new Resolution(v.BmiHeader.Width, v.BmiHeader.Height, bitCount));
                        rels += "\n" + v.BmiHeader.Width + "x" + v.BmiHeader.Height + "x" + bitCount.ToString() + ";";
                        if (v.BmiHeader.Width > max || v.BmiHeader.Height > max)
                            max = (Math.Max(v.BmiHeader.Width, v.BmiHeader.Height));
                    }
                    hr = mediaTypeEnum.Next(1, mediaTypes, fetched);
                }
                MessageBox.Show(rels);
                return resolutions;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return new List<Resolution>();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filter">Filtro que fornecerá a imagem ao componente</param>
        /// <param name="preview">Elemento que receberá o preview, deve ser um componente do tipo System.Windows.Forms</param>
        private void ConfigurePreview(IBaseFilter filter, System.Windows.Forms.Control preview)
        {
            int hr;
            preview.Width = this.previewWidth;
            preview.Height = this.previewHeight;
            IVMRFilterConfig9 vmr9Config = filter as IVMRFilterConfig9;
            hr = vmr9Config.SetRenderingMode(VMR9Mode.Windowless);
            DsError.ThrowExceptionForHR(hr);

            vmr9Control = (IVMRWindowlessControl9)filter;
            hr = vmr9Control.SetVideoClippingWindow(preview.Handle);
            DsError.ThrowExceptionForHR(hr);

            hr = vmr9Control.SetAspectRatioMode(VMR9AspectRatioMode.None);
            DsError.ThrowExceptionForHR(hr);

            hr = vmr9Control.SetVideoPosition(null, DsRect.FromRectangle(preview.ClientRectangle));
            DsError.ThrowExceptionForHR(hr);
        }

        /// <summary>
        /// Define as informações das dimensões da imagem
        /// </summary>
        /// <param name="sampGrabber"></param>
        private void SaveSizeInfo(ISampleGrabber sampGrabber)
        {
            int hr;

            // Get the media type from the SampleGrabber
            AMMediaType media = new AMMediaType();

            hr = sampGrabber.GetConnectedMediaType(media);
            DsError.ThrowExceptionForHR(hr);

            if ((media.formatType != FormatType.VideoInfo) || (media.formatPtr == IntPtr.Zero))
            {
                throw new NotSupportedException("Unknown Grabber Media Format");
            }

            // Grab the size info
            VideoInfoHeader videoInfoHeader = (VideoInfoHeader)Marshal.PtrToStructure(media.formatPtr, typeof(VideoInfoHeader));

            // código para pegar a maior resolução possivel
            //Resolution best = resolucoesDisponiveis[resolucoesDisponiveis.Count - 1];

            width = videoInfoHeader.BmiHeader.Width;
            height = videoInfoHeader.BmiHeader.Height;
            stride = width * (videoInfoHeader.BmiHeader.BitCount / 8);

            DsUtils.FreeAMMediaType(media);
            media = null;
        }

        /// <summary>
        /// Configura o filtro para fazer a captura dos bytes da imagem
        /// </summary>
        /// <param name="sg"></param>
        private void ConfigureSampleGrabber(ISampleGrabber sg, AMMediaType _media)
        {
            AMMediaType media = new AMMediaType();
            media.majorType = MediaType.Video;
            media.subType = MediaSubType.RGB24;
            media.formatType = FormatType.VideoInfo;
           
            int hr = sg.SetMediaType(media);
            DsError.ThrowExceptionForHR(hr);
        }

        /// <summary>
        /// Constrói o grafo do directshow e inicia o stream da camera
        /// </summary>
        public void Start()
        {
            if (camera == null)
            {
                return;
            }

            if (previewElement == null)
            {
                return;
            }
            BuildGraph(previewElement);

            int hr;
            // inicia o fluxo de dados no grafo
            hr = (filterGraph as IMediaControl).Run();
            DsError.ThrowExceptionForHR(hr);
        }

        public void Stop()
        {
            (filterGraph as IMediaControl).Stop();
        }

        public void Resume()
        {
            (filterGraph as IMediaControl).Run();
        }

        /// <summary>
        /// Retorna os bytes da imagem
        /// </summary>
        /// <returns></returns>
        public byte[] GetFrameBytes()
        {

            // get ready to wait for new image
            m_PictureReady.Reset();
            buffer = new byte[stride * height];

            try
            {
                capturar = true;
                if (!m_PictureReady.WaitOne(9000, false))
                {
                    throw new Exception("Timeout waiting to get picture");
                }
            }
            catch
            {
                buffer = null;
                throw;
            }

            // Got one
            return buffer;
        }

        /// <summary>
        /// Retorna o frame como um BitmapSource que pode ser atribuido a uma imagem
        /// </summary>
        /// <returns></returns>
        public BitmapSource GetFrameAsBitmapSource()
        {
            byte[] bytes = this.GetFrameBytes();
            BitmapSource bitmap = BitmapSource.Create(this.width, this.height, 96d, 96d, PixelFormats.Bgr24, null, bytes, this.stride);
            return bitmap;
        }

        public System.Windows.Controls.Image GetFrameAsImage()
        {
            byte[] bytes = this.GetFrameBytes();
            BitmapSource bitmap = BitmapSource.Create(this.width, this.height, 96d, 96d, PixelFormats.Bgr24, null, bytes, this.stride);

            System.Windows.Controls.Image image = new System.Windows.Controls.Image();
            image.Source = bitmap;
            Capture.FlipVertical(image);
            return image;
        }

        /// <summary>
        /// Pega o buffer, salva em disco e retorna a imagem 
        /// </summary>
        /// <param name="path">Caminha onde a imagem será salva</param>
        /// <returns></returns>
        public System.Windows.Controls.Image SaveFrameToFile(string path)
        {
            System.Windows.Controls.Image image = new System.Windows.Controls.Image();
            //MessageBox.Show(this.width.ToString() + "x" + this.height.ToString());
            image.Width = this.width;
            image.Height = this.height;
            image.Source = this.GetFrameAsBitmapSource();

            JpegBitmapEncoder jpegEncoder = new JpegBitmapEncoder();
            jpegEncoder.Frames.Add(BitmapFrame.Create(image.Source as BitmapSource));
            jpegEncoder.FlipVertical = true;
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                jpegEncoder.Save(fs);
            }
            return image;
        }

        private void CloseInterfaces()
        {
            int hr;

            try
            {
                if (filterGraph != null)
                {
                    IMediaControl mediaCtrl = filterGraph as IMediaControl;

                    // Stop the graph
                    hr = mediaCtrl.Stop();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            if (filterGraph != null)
            {
                Marshal.ReleaseComObject(filterGraph);
                filterGraph = null;
            }
        }

        public void Dispose()
        {
            CloseInterfaces();
            if (m_PictureReady != null)
            {
                m_PictureReady.Close();
            }
        }

        /// <summary>
        /// Não utilizado. Apenas implemanta o método necessário da interface ISampleGrabberCB
        /// </summary>
        /// <param name="SampleTime"></param>
        /// <param name="pSample"></param>
        /// <returns></returns>
        int ISampleGrabberCB.SampleCB(double SampleTime, IMediaSample pSample)
        {
            Marshal.ReleaseComObject(pSample);
            return 0;
        }

        /// <summary>
        /// Recebe os bytes da imagem e espera para fazer uma cópia da imagem
        /// </summary>
        /// <param name="SampleTime">Tempo de transmissão</param>
        /// <param name="pBuffer">Bytes da imamge</param>
        /// <param name="BufferLen">Tamanho do buffer</param>
        /// <returns></returns>
        

        private String b = "";

        int ISampleGrabberCB.BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
        {
            // Note that we depend on only being called once per call to Click.  Otherwise
            // a second call can overwrite the previous image.

            //if(b == "")
            //{
            //    MessageBox.Show("Stride = " + this.stride + ";\n" + "Width = " + this.width + ";\n" + "Height = " + this.height + "Bufferlen = " + BufferLen.ToString());
            //}

            Debug.Assert(BufferLen == Math.Abs(stride) * height, "Incorrect buffer length");
            if (capturar)
            {
                capturar = false;
                Debug.Assert(buffer != null, "Unitialized buffer");

                // Salva o buffer
                System.Runtime.InteropServices.Marshal.Copy(pBuffer, buffer, 0, BufferLen);

                // Foto está pronta
                m_PictureReady.Set();
            }

            return 0;
        }
    }
}
