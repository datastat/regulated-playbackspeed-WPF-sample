using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MFORMATSLib;
using Microsoft.Win32;

namespace SampleFilePlaybackWPF.ViewModel
{
    class ViewModel : INotifyPropertyChanged
    {
        #region FIELDS
        private MFReaderClass _myReader;
        private MFPreviewClass _myPreview;
        private bool _isThreadWork;
        // preview object
        private D3DImage _previewSource;
        private int _garbageCounter;
        #endregion

        #region PROPERTIES
        public D3DImage PreviewSource
        {
            get { return _previewSource; }
            set { _previewSource = value; }
        }
       
        public ICommand SetPlaybackSpeed_100 { get; set; }
        public ICommand SetPlaybackSpeed_75 { get; set; }
        public ICommand SetPlaybackSpeed_50 { get; set; }
        public ICommand SetPlaybackSpeed_25 { get; set; }
        #endregion

        #region CONSTRUCTOR
        public ViewModel()
        {
            SetPlaybackSpeed_100 = new RelayCommand(args => SetPlaybackSpeed(1));
            SetPlaybackSpeed_75 = new RelayCommand(args => SetPlaybackSpeed(0.75));
            SetPlaybackSpeed_50 = new RelayCommand(args => SetPlaybackSpeed(0.5));
            SetPlaybackSpeed_25 = new RelayCommand(args => SetPlaybackSpeed(0.25));

            InitializeReader();

            StartPlayback();
        }

        private void SetPlaybackSpeed(double v)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region METHODS

        public void CloseWindow()
        {
            _isThreadWork = false;

            if (_myPreview != null)
                _myPreview.OnEventSafe -= _myPreview_OnEventSafe;

            if (_mSavedPointer != IntPtr.Zero)
                Marshal.Release(_mSavedPointer);
        }

        private void StartPlayback()
        {
            if (_myReader == null)
            {
                MessageBox.Show("Select a file for playback first.");
                return;
            }
            if (!_isThreadWork)
            {
                PreviewSource = new D3DImage();
                _isThreadWork = true;
                Thread workingThread = new Thread(WpfPreviewBody);
                workingThread.Start();
            }
        }

        private void WpfPreviewBody()
        {
            while (_isThreadWork)
            {
                try
                { 
                    MFFrame sourceFrame;
                    _myReader.SourceFrameGet(-1, out sourceFrame, "");
                    _myPreview.ReceiverFramePut(sourceFrame, -1, "");
                    Marshal.ReleaseComObject(sourceFrame);
                    _garbageCounter++;
                    if (_garbageCounter%10 == 0) 
                        GC.Collect();
                }
                catch (Exception)
                {
                    Thread.Sleep(1);
                }
            }
        }

        private void InitializeReader()
        {
            try
            {
                _myReader = new MFReaderClass();
                _myReader.ReaderOpen(@"C:\tmp\BBB_50fps_12mbit_60s_snowflake.mp4", "loop=true");
                _myPreview = new MFPreviewClass();
                _myPreview.PropsSet("wpf_preview", "true");
                _myPreview.PreviewEnable("", 0, 1);
                _myPreview.OnEventSafe += _myPreview_OnEventSafe;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Can't open the file:" + Environment.NewLine + ex.Message);
                _myReader = null;
            }
        }
        private IntPtr _mSavedPointer;
        private void _myPreview_OnEventSafe(string bsChannelId, string bsEventName, string bsEventParam, object pEventObject)
        {
            if (bsEventName == "wpf_nextframe")
            {
                IntPtr pEventObjectPtr = Marshal.GetIUnknownForObject(pEventObject);
                if (pEventObjectPtr != _mSavedPointer)
                {
                    if (_mSavedPointer != IntPtr.Zero)
                        Marshal.Release(_mSavedPointer);

                    _mSavedPointer = pEventObjectPtr;
                    Marshal.AddRef(_mSavedPointer);

                    PreviewSource.Lock();
                    PreviewSource.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                    PreviewSource.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _mSavedPointer);
                    PreviewSource.Unlock();
                    OnPropertyChanged("PreviewSource");
                }

                if (pEventObjectPtr != IntPtr.Zero)
                    Marshal.Release(pEventObjectPtr);

                PreviewSource.Lock();
                PreviewSource.AddDirtyRect(new Int32Rect(0, 0, _previewSource.PixelWidth, _previewSource.PixelHeight));
                PreviewSource.Unlock();
            }

            Marshal.ReleaseComObject(pEventObject);
        }
        #endregion

        #region NotifyPropertyChangedIMPLEMENTATION
        public event PropertyChangedEventHandler PropertyChanged; // default implementation of INotifyPropertyChanged interface

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
