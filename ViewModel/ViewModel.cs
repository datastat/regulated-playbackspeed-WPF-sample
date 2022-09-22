using System;
using System.ComponentModel;
using System.Diagnostics;
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
       
        public ICommand SetPlaybackSpeed_200 { get; set; }
        public ICommand SetPlaybackSpeed_150 { get; set; }
        public ICommand SetPlaybackSpeed_125 { get; set; }
        public ICommand SetPlaybackSpeed_100 { get; set; }
        public ICommand SetPlaybackSpeed_99 { get; set; }
        public ICommand SetPlaybackSpeed_75 { get; set; }
        public ICommand SetPlaybackSpeed_50 { get; set; }
        public ICommand SetPlaybackSpeed_25 { get; set; }
        #endregion

        #region CONSTRUCTOR
        public ViewModel()
        {
            SetPlaybackSpeed_200 = new RelayCommand(args => SetPlaybackSpeed(2));
            SetPlaybackSpeed_150 = new RelayCommand(args => SetPlaybackSpeed(1.5));
            SetPlaybackSpeed_125 = new RelayCommand(args => SetPlaybackSpeed(1.25));
            SetPlaybackSpeed_100 = new RelayCommand(args => SetPlaybackSpeed(1));
            SetPlaybackSpeed_75 = new RelayCommand(args => SetPlaybackSpeed(0.75));
            SetPlaybackSpeed_50 = new RelayCommand(args => SetPlaybackSpeed(0.5));
            SetPlaybackSpeed_25 = new RelayCommand(args => SetPlaybackSpeed(0.25));

            InitializeReader();

            StartPlayback();
        }

        private void SetPlaybackSpeed(double v)
        {
            desiredPlaybackSpeed = v;
            //readerParams = $"rate={Math.Abs(v).ToString().Replace(',', '.')}";
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

        private string readerParams = "";

        public double CurrentPlaybackSpeed { get; set; } = 1;
        public string CurrentPlaybackSpeedString { get; set; }
        public int CurrentFrame { get; set; } = 0;
        private double desiredPlaybackSpeed = 1;

        private double tickValue = 0.025;

        private bool getNextCalculatedFrame = false;


        private void WpfPreviewBody()
        {
            while (_isThreadWork)
            {
                try
                {
                    MFFrame sourceFrame;

                    // change playbackspeed
                    if (Math.Abs(CurrentPlaybackSpeed - desiredPlaybackSpeed) > tickValue)
                    {
                        if (CurrentPlaybackSpeed > desiredPlaybackSpeed)
                        {
                            CurrentPlaybackSpeed = Math.Round(CurrentPlaybackSpeed - tickValue, 3);
                        }
                        else
                        {
                            CurrentPlaybackSpeed = Math.Round(CurrentPlaybackSpeed + tickValue, 3);
                        }

                        OnPropertyChanged(nameof(CurrentPlaybackSpeed));

                        if (CurrentPlaybackSpeed != 1)
                        {
                            CurrentPlaybackSpeedString = $"rate={Math.Abs(CurrentPlaybackSpeed).ToString().Replace(',', '.')}";
                        }
                        else
                        {
                            CurrentPlaybackSpeedString = "";
                            getNextCalculatedFrame = true;
                        }

                        OnPropertyChanged(nameof(CurrentPlaybackSpeedString));
                    }

                    // get frame
                    if (getNextCalculatedFrame)
                    {
                        _myReader.SourceFrameGetByNumber(CurrentFrame + 1, -1, out sourceFrame, "");
                        getNextCalculatedFrame = false;
                    }
                    else
                    {
                        _myReader.SourceFrameGet(-1, out sourceFrame, CurrentPlaybackSpeedString);
                    }

                    _myPreview.ReceiverFramePut(sourceFrame, -1, "");

                    // get current frame
                    sourceFrame.MFTimeGet(out M_TIME m_TIME);
                    sourceFrame.MFAVPropsGet(out var props, out _);

                    var changeFor = Math.Abs(CurrentFrame - m_TIME.tcFrame.nExtraCounter);

                    if (changeFor > 1)
                    {
                        Debug.WriteLine($"{DateTime.UtcNow} Jumped {CurrentFrame - m_TIME.tcFrame.nExtraCounter}");
                    }

                    CurrentFrame = m_TIME.tcFrame.nExtraCounter;
                    OnPropertyChanged(nameof(CurrentFrame));


                    // clean up
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
                //_myReader.ReaderOpen(@"C:\tmp\BBB_50fps_12mbit_10m_snowflake.mp4", "loop=true");
                _myReader.ReaderOpen(@"C:\FairReplayC\Exports\607-Short_event_at_14_18_11_796-Mat_2_F.mp4", "loop=true");
                _myReader.PropsSet("stat::frame_get", "true");



                _myPreview = new MFPreviewClass();
                _myPreview.PropsSet("wpf_preview", "true");
                _myPreview.PreviewEnable("", 0, 1);

                _myPreview.PropsSet("preview.drop_frames", "true");
                _myPreview.PropsSet("rate_control", "true");

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
