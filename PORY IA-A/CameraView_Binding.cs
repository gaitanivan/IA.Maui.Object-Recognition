using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using System.ComponentModel;

namespace com.gaitanivan.maui.poryiaa
{
	internal class CameraView_Binding : INotifyPropertyChanged
	{
		#region Vars
		private IReadOnlyList<CameraInfo>? _availableCameras;
		/// <summary>
		/// Alamcena las cámaras habilitadas en el dispositivo.
		/// </summary>
		public IReadOnlyList<CameraInfo>? AvailableCameras
		{
			get => _availableCameras;
			set
			{
				if (_availableCameras != value)
				{
					_availableCameras = value;
					OnPropertyChanged(nameof(AvailableCameras));
				}
			}
		}

		private GridLength _hasManyCameras_Row = new(0);
		/// <summary>
		/// Indica si se muestra la fila de selección de cámaras si es que se detectan varias cámaras en el dispositivo.
		/// </summary>
		public GridLength HasManyCameras_Row
		{
			get => _hasManyCameras_Row;
			set
			{
				if (_hasManyCameras_Row != value)
				{
					_hasManyCameras_Row = value;
					OnPropertyChanged(nameof(HasManyCameras_Row));
				}
			}
		}

		private GridLength _hasResolutions_Row = new(0);
		/// <summary>
		/// Indica si se muestra la fila de selección de resolución para la cámara seleccionada.
		/// </summary>
		public GridLength HasResolutions_Row
		{
			get => _hasResolutions_Row;
			set
			{
				if (_hasResolutions_Row != value)
				{
					_hasResolutions_Row = value;
					OnPropertyChanged(nameof(HasResolutions_Row));
				}
			}
		}

		private CameraInfo? _selectedCamera;
		/// <summary>
		/// Indica la cámara seleccionada.
		/// </summary>
		public CameraInfo? SelectedCamera
		{
			get => _selectedCamera;
			set
			{
				if (_selectedCamera != value)
				{
					_selectedCamera = value;
					SetSupportedResolutions();
					OnPropertyChanged(nameof(SelectedCamera));
				}
			}
		}

		private Size _selectedResolution;
		/// <summary>
		/// Indica la resolución a usar con la cámara seleccionada.
		/// </summary>
		public Size SelectedResolution
		{
			get => _selectedResolution;
			set
			{
				if (_selectedResolution != value)
				{
					_selectedResolution = value;
					OnPropertyChanged(nameof(SelectedResolution));
				}
			}
		}

		private IReadOnlyList<Size> _supportedResolutions;
		/// <summary>
		/// Indica las resoluciónes soportadas por la cámara seleccionada.
		/// </summary>
		public IReadOnlyList<Size> SupportedResolutions
		{
			get => _supportedResolutions;
			set
			{
				if (_supportedResolutions != value)
				{
					_supportedResolutions = value;
					OnPropertyChanged(nameof(SupportedResolutions));
				}
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		#endregion


		#region Funcs
		public async void GetCameras(CameraView camera_view)
		{
			// Validar si hay varias cámaras en el dispositivo.
			var cams = await camera_view.GetAvailableCameras(CancellationToken.None);
			HasManyCameras_Row = cams.Count > 1 ? GridLength.Auto : new(0);
			AvailableCameras = cams;
			// Asignar la primera cámara encontrada como cámara seleccionada.
			if (cams.Count > 0)
			{
				SelectedCamera = cams[0];
				HasResolutions_Row = SelectedCamera.SupportedResolutions.Count > 1 ? GridLength.Auto : new(0);
				// Asignar la primera resolución disponible.
				if (SupportedResolutions.Count > 0)
				{
					SelectedResolution = SupportedResolutions[0];
				}
			}
		}

		private void SetSupportedResolutions()
		{
			// Buscamos resoluciones donde el lado más largo esté entre 720 y 1920
			// Y priorizamos las que tengan un aspecto común (16:9 o 4:3)
			var spres = _selectedCamera!.SupportedResolutions
				.Where(s => {
					double maxSide = Math.Max(s.Width, s.Height);
					return maxSide >= 720 && maxSide <= 1920;
				})
				.OrderByDescending(s => s.Width * s.Height); // De mejor a menor calidad
			// Verificar que haya alguna resolución encontrada, si no, dejar las resoluciones originales.
			SupportedResolutions = (!spres.Any() ? _selectedCamera!.SupportedResolutions : [.. spres]);
		}

		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		#endregion
	}
}
