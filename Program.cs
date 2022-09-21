using System;

namespace SampleFilePlaybackWPF
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                var mainView = new View.View();
                // to handle window event
                mainView.Closing += (obj, ars) => ((ViewModel.ViewModel)mainView.DataContext).CloseWindow();
                app.Run(mainView);
            }
            catch (Exception)
            {

            }
        }
    }
}
