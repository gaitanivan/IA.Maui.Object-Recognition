using Microsoft.Extensions.DependencyInjection;

namespace com.gaitanivan.maui.poryiaa
{
	public partial class App : Application
	{
		public App()
		{
			InitializeComponent();
		}

		protected override Window CreateWindow(IActivationState? activationState)
		{
			var window = new Window(new AppShell());
			{
#if WINDOWS
				// Suscribirse al evento de creación de la ventana nativa
				window.Created += (s, e) =>
				{
					var nativeWindow = window.Handler.PlatformView as Microsoft.UI.Xaml.Window;
					if (nativeWindow != null)
					{
						var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
						// Usamos Win32Interop para obtener el ID de forma más directa
						var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
						var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

						var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
						presenter?.Maximize();
					}
				};
#endif
			};

			return window;
		}
	}
}