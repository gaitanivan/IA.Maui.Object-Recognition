using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using System.Diagnostics;
using System.Timers;

namespace com.gaitanivan.maui.poryiaa
{
	public partial class MainPage : ContentPage
	{
		#region Vars
		/// <summary>
		/// Instanciar la clase que servirá como binding para la página.
		/// </summary>
		private CameraView_Binding binding = new();
		private float detection_interval = 1000;
		/// <summary>
		/// Ayuda a mantener la orientación del dispositivo, en móvil, android.
		/// </summary>
		private DisplayOrientation device_orientation;
		/// <summary>
		/// Mantiene la referencia a la sesión para realizar inferencias.
		/// </summary>
		private InferenceSession? inference_session;
		/// <summary>
		/// Usado para pintar sobre la imágen de la cámara.
		/// </summary>
		private SKSurface overlay = SKSurface.CreateNull(0, 0);
		/// <summary>
		/// Título base de la página.
		/// </summary>
		private string page_title_base = string.Empty;
		/// <summary>
		/// Temporizador para ir haciendo capturas cada x tiempo.
		/// </summary>
		private System.Timers.Timer timer = new();

		/// <summary>
		/// Listado de objetos que se pueden reconocer.
		/// </summary>
		public static readonly string[] object_names =
		[
			"Persona", "Bicicleta", "Coche", "Motocicleta", "Avión", "Autobús", "Tren", "Camión", "Barco", "Semáforo",
			"Hidrante", "Señal de pare", "Parquímetro", "Banco", "Pájaro", "Gato", "Perro", "Caballo", "Oveja", "Vaca",
			"Elefante", "Oso", "Cebra", "Jirafa", "Mochila", "Paraguas", "Bolso", "Corbata", "Maleta", "Frisbee",
			"Esquís", "Snowboard", "Pelota de deportes", "Cometa", "Bate de béisbol", "Guante de béisbol", "Monopatín", "Tabla de surf",
			"Raqueta de tenis", "Botella", "Copa de vino", "Taza", "Tenedor", "Cuchillo", "Cuchara", "Tazón", "Plátano", "Manzana",
			"Sándwich", "Naranja", "Brócoli", "Zanahoria", "Perro caliente", "Pizza", "Donut", "Pastel", "Silla", "Sofá",
			"Planta en maceta", "Cama", "Mesa de comedor", "Inodoro", "Televisión", "Portátil", "Ratón", "Control remoto", "Teclado", "Teléfono celular",
			"Microondas", "Horno", "Tostadora", "Fregadero", "Refrigerador", "Libro", "Reloj", "Florero", "Tijeras", "Oso de peluche",
			"Secador de pelo", "Cepillo de dientes"
		];
		#endregion


		#region Events
		public MainPage()
		{
			InitializeComponent();

			// Definir el binding context.
			BindingContext = binding;
			binding.GetCameras(CameraViewer);

			timer.Interval = 1000;
			timer.Elapsed += Timer_Elapsed;

			// Definir función que controle evento para verificar la orientación de la pantalla, en android.
			DeviceDisplay.MainDisplayInfoChanged += (s, e) =>
			{
				device_orientation = e.DisplayInfo.Orientation;
			};
		}

		private async void ContentPage_Appearing(object sender, EventArgs e)
		{
			// Tener el título base de la página.
			page_title_base = Title;
			
			// Cargar el modelo para hacer inferencias.
			inference_session ??= await LoadOnnxModelAsync();

			// Aquí ayudar a saber la orientación del dispositivo.
			device_orientation = DeviceDisplay.MainDisplayInfo.Orientation;

			// Para android, solicitar permiso de usar la cámara.
			var cameraPermissionsRequest = await Permissions.RequestAsync<Permissions.Camera>();
			if (cameraPermissionsRequest != PermissionStatus.Granted)
			{
				await DisplayAlertAsync("Atención", "No se ha dado permiso para usar la cámara.", "OK");
				return;
			}
			// Inicializar la vista previa.
			try
			{
				// Dar tiempo a que se inicialice la cámara.
				await Task.Delay(1000);
				// Iniciar las capturas periodicas.
				timer.Start();
				// EL control CameraView inicializa la vista previs por defecto pero igual hacer la llamada.
				var startCameraPreviewTCS = new CancellationTokenSource(TimeSpan.FromSeconds(3));
				await CameraViewer.StartCameraPreview(startCameraPreviewTCS.Token);
			}
			catch (Exception ex)
			{
				// Mostrar mensaje si hay fallo.
				Trace.WriteLine(ex);
			}
		}

		private void ContentPage_Disappearing(object sender, EventArgs e)
		{
			// Detener la vista previa.
			try
			{
				timer.Stop();
				CameraViewer.StopCameraPreview();
				inference_session?.Dispose();
				overlay?.Dispose();
			}
			catch (Exception ex)
			{
				// Mostrar mensaje si hay fallo.
				Trace.WriteLine(ex);
			}
		}

		private void MultiDetectionSwitch_Toggled(object sender, ToggledEventArgs e)
		{

		}

		private async void Timer_Elapsed(object? sender, ElapsedEventArgs e)
		{
			timer.Stop();
			await CaptureAsync();
		}
		#endregion


		#region Funcs
		/// <summary>
		/// Función que aplica una técnica conocida como letterbox.
		/// </summary>
		/// <param name="_original">La imágen en el tamaño original.</param>
		/// <param name="_targetSize">El tamaño de destino. Por defecto es 640, el usado por el modelo de reconocimiento de objetos YOLO.</param>
		/// <returns>Retorna la imagen lista para procesar por el modelo.</returns>
		public SKBitmap ApplyLetterbox(SKBitmap _original, int _targetSize = 640)
		{
			// Crear el lienzo cuadrado con el color neutro de YOLO (Gris 114)
			var letterboxed = new SKBitmap(_targetSize, _targetSize);
			using var canvas = new SKCanvas(letterboxed);
			canvas.Clear(new SKColor(114, 114, 114));

			// Calcular escala manteniendo proporción
			float scale = Math.Min((float)_targetSize / _original.Width, (float)_targetSize / _original.Height);
			int newWidth = (int)(_original.Width * scale);
			int newHeight = (int)(_original.Height * scale);

			// Calcular posición para centrarla
			float left = (_targetSize - newWidth) / 2f;
			float top = (_targetSize - newHeight) / 2f;

			// Dibujar la imagen escalada sobre el fondo gris
			var destRect = new SKRect(left, top, left + newWidth, top + newHeight);
			canvas.DrawBitmap(_original, destRect);

			return letterboxed;
		}

		/// <summary>
		/// Método asíncrono para realizar la captura de imágen desde la cámara y a partir de allí, procesarla.
		/// </summary>
		private async Task CaptureAsync()
		{
			try
			{
				// Obtener la imagen de la cámara como un stream.
				var captureImageCTS = new CancellationTokenSource(TimeSpan.FromSeconds(3));
				var st = await CameraViewer.CaptureImage(captureImageCTS.Token);
				
				// Crear la imagen y liberar el stream.
				var bmp_original = SKBitmap.Decode(st);
				st.Dispose();
				// Definir la imagen que se usará finalmente.
				SKBitmap bmp_final;

				// Detectar si el bitmap viene acostado cuando debería ser vertical.
				// Esto porque la imagen capturada por CameraView en portrait llega como si estuviese en landscape.
				if (DeviceInfo.Platform == DevicePlatform.Android && device_orientation == DisplayOrientation.Portrait)
				{
					// Rotamos los píxeles 90 grados
					bmp_final = new SKBitmap(bmp_original.Height, bmp_original.Width);
					using (var canvas = new SKCanvas(bmp_final))
					{
						canvas.Translate(bmp_original.Height, 0);
						canvas.RotateDegrees(90);
						canvas.DrawBitmap(bmp_original, 0, 0);
					}
				}
				else
				{
					bmp_final = bmp_original.Copy(); // En Landscape o Windows se queda igual
				}
				// Liberar los recursos usados por la imagen original que ya no se va a usar mas.
				bmp_original.Dispose();

				// Ahora ww y hh SIEMPRE se toman del bitmap resultante
				int ww = bmp_final.Width;
				int hh = bmp_final.Height;

				// Procesar la imágen, detectar objetos.
				var predictions = await ProcessDetectionAsync(bmp_final, inference_session!);
				bmp_final.Dispose();

				// Escalar cada predicción de la lista.
				foreach (var pred in predictions)
					ScalePrediction(pred, ww, hh);

				// Dibujar el overlay.
				SetOverlaySize(ww, hh);
				await DrawOverlayAsync(predictions);

				// Reiniciar el conteo para una nueva captura.
				timer.Interval = detection_interval;
				timer.Start();
			}
			catch (Exception ex)
			{
				// Handle Exception
				Trace.WriteLine(ex);
				timer.Interval = 1000;
				timer.Start();
			}
		}

		/// <summary>
		/// Método para dibujar un cuadrado y texto en el overlay.
		/// </summary>
		/// <param name="_predictions">Recibe la lista de predicciones a dibujar.</param>
		private async Task DrawOverlayAsync(List<Prediction> _predictions)
		{
			await Task.Run(() =>
			{
				var canvas = overlay.Canvas;
				canvas.Clear(SKColors.Transparent);

				// La primera es la principal (mayor score)
				var mainPrediction = _predictions.FirstOrDefault();

				// Si hay elementos, iteramos para dibujar cada uno
				foreach (var prediction in _predictions)
				{
					if (string.IsNullOrEmpty(prediction.Name)) continue;

					// Determinar si es el principal para ajustar la opacidad
					bool isMain = prediction == mainPrediction;

					// Definimos los colores: Rojo sólido para el principal, 
					// y un rojo con Alpha (transparencia) para los demás.
					// 0xFF = 255 (sólido), 0x80 = 128 (50% transparente)
					byte alpha = isMain ? (byte)0xFF : (byte)0x60;
					var themeColor = new SKColor(255, 0, 0, alpha);

					// Dibujar Rectángulo
					// Pincel del Rectángulo
					using var paint = new SKPaint
					{
						Color = themeColor,
						Style = SKPaintStyle.Stroke,
						StrokeWidth = isMain ? prediction.StrokeWidth : prediction.StrokeWidth * 0.7f,
						StrokeJoin = SKStrokeJoin.Round
					};
					canvas.DrawRect(prediction.X, prediction.Y, prediction.W, prediction.H, paint);

					// Configurar Texto y Fuente
					using var textPaint = new SKPaint
					{
						Color = isMain ? SKColors.White : SKColors.White.WithAlpha(alpha),
						IsAntialias = true
					};
					using var font = new SKFont
					{
						Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
						Size = prediction.StrokeWidth * (isMain ? 6 : 5) // Texto un poco más pequeño para secundarios
					};

					string tag_text = $"{prediction.Name} {prediction.Score:P0}";
					SKRect textBounds = new();
					font.MeasureText(tag_text, out textBounds, textPaint);

					float padding = prediction.StrokeWidth * (isMain ? 2 : 1.5f);
					var backgroundRect = new SKRect(
						prediction.X,
						prediction.Y,
						prediction.X + textBounds.Width + (padding * 2),
						prediction.Y + textBounds.Height + padding
					);

					// Dibujar Fondo del Texto
					// Pincel del Fondo del Texto (usando el mismo color tenue)
					using var bgPaint = new SKPaint { Color = themeColor, Style = SKPaintStyle.Fill };
					canvas.DrawRect(backgroundRect, bgPaint);

					// Dibujar Texto
					canvas.DrawText(
						tag_text,
						backgroundRect.Left + padding,
						backgroundRect.Top + textBounds.Height + (padding / 2),
						font,
						textPaint
					);
				}

				// Preparar la imagen para la UI
				using var image = overlay.Snapshot();
				using var data = image.Encode(SKEncodedImageFormat.Png, 100);
				var ms = new MemoryStream();
				data.AsStream().CopyTo(ms);
				ms.Position = 0;

				// Actualizar UI
				string infoTitle = page_title_base;
				if (_predictions.Count > 0)
				{
					var best = _predictions.First();
					infoTitle += _predictions.Count > 1
						? $" : {best.Name} y {_predictions.Count - 1} más"
						: $" : {best.Name} {best.Score:P0}";
				}

				MainThread.BeginInvokeOnMainThread(() =>
				{
					MyDisplayImage.Source = ImageSource.FromStream(() => ms);
					Title = infoTitle;
				});
			});
		}

		/// <summary>
		/// Función que recibe la imágen a procesar para generear el tensor a procesar.
		/// </summary>
		/// <param name="_bitmap">Recibe la imágen a procesar.</param>
		/// <returns>El tensor a procesar.</returns>
		private DenseTensor<float> ImageToTensor(SKBitmap _bitmap)
		{
			// Redimensionar a 640x640 (lo que espera el modelo)
			//using var resizedBitmap = _bitmap.Resize(new SKImageInfo(640, 640), SKSamplingOptions.Default);
			using var resizedBitmap = ApplyLetterbox(_bitmap);

			// Crear el contenedor del tensor [Batch, Canales, Alto, Ancho]
			var tensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });

			// Extraer los píxeles y normalizar
			// Recorrer la imagen para separar los canales R, G y B
			for (int y = 0; y < 640; y++)
			{
				for (int x = 0; x < 640; x++)
				{
					var color = resizedBitmap.GetPixel(x, y);

					// Normalizamos de 0-255 a 0.0-1.0
					// Llenamos el tensor en formato Planar: [Canal][Y][X]
					tensor[0, 0, y, x] = color.Red / 255f;   // Canal R
					tensor[0, 1, y, x] = color.Green / 255f; // Canal G
					tensor[0, 2, y, x] = color.Blue / 255f;  // Canal B
				}
			}

			return tensor;
		}

		/// <summary>
		/// Función para cargar el modelo onnx para reconocimiento de objetos.
		/// </summary>
		/// <returns>Retorna el InferenceSession a usar.</returns>
		private static async Task<InferenceSession> LoadOnnxModelAsync()
		{
			// Abrir el archivo desde los recursos de la App (Resources/raw).
			using var stream = await FileSystem.OpenAppPackageFileAsync("yolo11n.onnx");

			// Leerlo a memoria (ONNX Runtime necesita los bytes o el Stream).
			using var memoryStream = new MemoryStream();
			await stream.CopyToAsync(memoryStream);
			byte[] modelBytes = memoryStream.ToArray();

			// Crear la sesión de inferencia usando los bytes.
			return new InferenceSession(modelBytes);
		}

		/// <summary>
		/// Método que procesa una imágen para validar si reconoce algún objeto en ella.
		/// </summary>
		/// <param name="_image">Recibe la imagen a procesar.</param>
		/// <param name="_session">Recibe la sesión de inferencia con la que se procesará la imagen.</param>
		/// <returns>Retorna una lista de predicciones encontradas.</returns>
		private async Task<List<Prediction>> ProcessDetectionAsync(SKBitmap _image, InferenceSession _session)
		{
			// Convertir imagen a tensor
			var inputTensor = ImageToTensor(_image);

			// Crear la entrada para ONNX (el nombre "images" debe coincidir con el del modelo)
			var inputs = new List<NamedOnnxValue>
			{
				NamedOnnxValue.CreateFromTensor("images", inputTensor)
			};

			// Correr el modelo
			using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);

			// Obtener los resultados crudos
			var output = results.First().AsEnumerable<float>().ToArray();

			// AQUÍ es donde recibes las predicciones de objetos con mayor confianza de haber sido detectados.
			return await GetModelPredictionsAsync(output);
		}

		/// <summary>
		/// Función para procesar la salida del modelo y obtener los resultados de objetos encontrados mas fiables.
		/// </summary>
		/// <param name="output">Recibe la salida de una inferencia.</param>
		/// <returns>Retorna una lista con las probabilidades mas altas de que sea X objeto.</returns>
		public async Task<List<Prediction>> GetModelPredictionsAsync(float[] output)
		{
			return await Task.Run(() =>
			{
				// Lista para almacenar los resultados.
				var predictions = new List<Prediction>();
				// Parámetros de YOLO11
				int dimensions = 84; // 4 coordenadas + 80 clases
				int cells = 8400;    // Cantidad de predicciones
				float confidenceThreshold = 0.5f; // Umbral de 50%

				for (int i = 0; i < cells; i++)
				{
					// Encontrar la clase con mayor puntaje para esta "celda"
					float maxClassScore = 0;
					int classId = -1;

					for (int c = 4; c < dimensions; c++)
					{
						// El índice se calcula saltando de 8400 en 8400 debido a la transposición
						float score = output[c * cells + i];
						if (score > maxClassScore)
						{
							maxClassScore = score;
							classId = c - 4;
						}
					}

					// Si el puntaje es alto, extraemos la caja
					if (maxClassScore > confidenceThreshold)
					{
						// Coordenadas en formato 0-640 (relativo al modelo)
						float x_center = output[0 * cells + i];
						float y_center = output[1 * cells + i];
						float width = output[2 * cells + i];
						float height = output[3 * cells + i];

						// Convertir de "centro" a "esquina superior izquierda" (formato estándar)
						float x = x_center - (width / 2);
						float y = y_center - (height / 2);

						predictions.Add(new Prediction
						{
							X = x,
							Y = y,
							W = width,
							H = height,
							Id = classId,
							Score = maxClassScore,
							// Asignamos el nombre usando el ID como índice
							Name = (classId >= 0 && classId < object_names.Length)
								? object_names[classId]
								: string.Empty
						});
					}
				}

				// Aplicar NMS para limpiar duplicados (Fundamental para modo "Muchos")
				predictions = PerformNMS(predictions);

				// Ordenamos por Score de mayor a menor y tomamos el primero
				var preds = predictions
					.OrderByDescending(p => p.Score)
					.Take(MultiDetectionSwitch.IsToggled ? predictions.Count : 1)
					.ToList();
				return preds;
			});
		}
		private List<Prediction> PerformNMS(List<Prediction> _predictions, float _iouThreshold = 0.45f)
		{
			var result = new List<Prediction>();
			var remaining = _predictions.OrderByDescending(p => p.Score).ToList();

			while (remaining.Count > 0)
			{
				var current = remaining[0];
				result.Add(current);
				remaining.RemoveAt(0);

				// Eliminamos de la lista los que se solapan demasiado con el que acabamos de agregar
				remaining.RemoveAll(next => CalculateIoU(current, next) > _iouThreshold);
			}

			return result;
		}

		private float CalculateIoU(Prediction a, Prediction b)
		{
			// Calculamos el área de intersección entre dos rectángulos
			float x1 = Math.Max(a.X, b.X);
			float y1 = Math.Max(a.Y, b.Y);
			float x2 = Math.Min(a.X + a.W, b.X + b.W);
			float y2 = Math.Min(a.Y + a.H, b.Y + b.H);

			float intersectionWidth = Math.Max(0, x2 - x1);
			float intersectionHeight = Math.Max(0, y2 - y1);
			float intersectionArea = intersectionWidth * intersectionHeight;

			// Área de unión
			float areaA = a.W * a.H;
			float areaB = b.W * b.H;
			float unionArea = areaA + areaB - intersectionArea;

			return intersectionArea / unionArea;
		}

		/// <summary>
		/// Función para pasar de escala el rectángulo de una predicción, pasando de 640x640 del modelo a la resolución real de la imágen pasada.
		/// </summary>
		/// <param name="_prediction">Recibe la prediccoón a perocesar.</param>
		/// <param name="_real_width">Recibe el ancho real de la imágen procesada.</param>
		/// <param name="_real_height">Recibe el alto real de la imágen procesada.</param>
		private static void ScalePrediction(Prediction _prediction, float _real_width, float _real_height)
		{
			// Calcular la escala que se usó para encajar la imagen en el cuadrado de 640
			// (Es la misma lógica que usamos en AplicarLetterbox)
			float scale = Math.Min(640f / _real_width, 640f / _real_height);

			// Calcular cuánto espacio gris (offset) hay a los lados o arriba/abajo en el tensor de 640x640
			float offsetX = (640f - (_real_width * scale)) / 2f;
			float offsetY = (640f - (_real_height * scale)) / 2f;

			// Corregir las coordenadas del modelo:
			// Primero restamos el espacio gris y luego dividimos por la escala para volver a píxeles reales
			_prediction.X = (_prediction.X - offsetX) / scale;
			_prediction.Y = (_prediction.Y - offsetY) / scale;
			_prediction.W = _prediction.W / scale;
			_prediction.H = _prediction.H / scale;

			// Deducir el grosor de línea (esto se mantiene igual, es excelente)
			float max_dimension = Math.Max(_real_width, _real_height);
			float stroke_width = max_dimension * 0.005f;
			stroke_width = Math.Clamp(stroke_width, 2f, 10f);

			_prediction.StrokeWidth = stroke_width;
		}

		/// <summary>
		/// Método para asignar el tamaño del overlay donde se pintan los cuadrados.
		/// </summary>
		/// <param name="_width">Recibe el ancho a asignar.</param>
		/// <param name="_height">Recibe el alto a asignar.</param>
		private void SetOverlaySize(int _width, int _height)
		{
			if (overlay != null)
			{
				using var img = overlay.Snapshot();
				if (img.Width == _width && img.Height == _height)
					return;
			}
			// Librar el SKSurface anterior.
			overlay?.Dispose();
			// Definir el tamaño del surface.
			overlay = SKSurface.Create(new SKImageInfo(_width, _height));
		}

		//private async Task Process_Resource_Image_Async()
		//{
		//	using var original_image = await LoadImage("test_image.jpeg");
		//	// Manipular la imagen.
		//	using var resized_image = original_image.Resize(new SKSizeI(400, 400), SKSamplingOptions.Default);
		//	// Liberar el original si ya no se va a usar, limpiar memoria.
		//	original_image.Dispose();
		//	// Definir la imagen como un lienzo sobre el que se va a pintar.
		//	using (var canvas = new SKCanvas(resized_image))
		//	{
		//		// Definir el estilo del pincel que pinta.
		//		using var paint = new SKPaint
		//		{
		//			Color = SKColors.Yellow,    // El color del texto
		//			IsAntialias = true,         // Suavizado de bordes
		//			Style = SKPaintStyle.Fill   // Relleno sólido
		//		};
		//		// Definir la fuente a usar para escribir.
		//		using var font = new SKFont(SKTypeface.Default, 24);
		//		// Definir el texto a mostrar.
		//		var res = $"{resized_image.Width}x{resized_image.Height}";
		//		// Pintar el texto.
		//		canvas.DrawText(res, 5, 20, font, paint);

		//		// Dibujar un cículo en el centro.
		//		// Redefinir el pincel.
		//		paint.Color = SKColor.Parse("88ff0000"); // Color argb
		//		paint.Style = SKPaintStyle.StrokeAndFill;
		//		canvas.DrawCircle(new SKPoint(resized_image.Width / 2, resized_image.Height / 2), 50, paint);
		//	}

		//	// Trabajar sobre los pixeles.
		//	//SKColor col;
		//	//for (var px = 0; px < resized_image.Width; px++)
		//	//	for (var py = 0; py < resized_image.Height; py++)
		//	//	{
		//	//		col = resized_image.GetPixel(px, py);
		//	//		resized_image.SetPixel(px, py, new SKColor(
		//	//			(byte)(255 - col.Red),
		//	//			(byte)(255 - col.Green),
		//	//			(byte)(255 - col.Blue),
		//	//			col.Alpha
		//	//		));
		//	//	}

		//	// Validar que el puntero no sea nulo.
		//	var pixelsPtr = resized_image.GetPixels();
		//	if (pixelsPtr == IntPtr.Zero)
		//	{
		//		throw new InvalidOperationException("No se pudo obtener acceso a la memoria del Bitmap.");
		//	}
		//	Span<byte> pixels;
		//	unsafe
		//	{
		//		// Obtener el puntero a la memoria nativa.
		//		byte* rawPixels = (byte*)pixelsPtr.ToPointer();
		//		int totalBytes = resized_image.RowBytes * resized_image.Height;

		//		// Se crea un "Span" (una vista segura sobre esa memoria RAM cruda)
		//		pixels = MemoryMarshal.CreateSpan(ref rawPixels[0], totalBytes);
		//	}
		//	// 3. Iteramos sobre los bytes (asumiendo formato RGBA: 4 bytes por píxel)
		//	// Nota: 'resized_image.BytesPerPixel' usualmente es 4
		//	int bytesPerPixel = resized_image.BytesPerPixel;

		//	for (int i = 0; i < pixels.Length; i += bytesPerPixel)
		//	{
		//		// i   = Rojo
		//		// i+1 = Verde
		//		// i+2 = Azul
		//		// i+3 = Alfa (transparencia)

		//		pixels[i] = (byte)(255 - pixels[i]);     // R
		//		pixels[i + 1] = (byte)(255 - pixels[i + 1]); // G
		//		pixels[i + 2] = (byte)(255 - pixels[i + 2]); // B

		//		// No tocamos el canal Alfa (pixels[i+3]) para mantener la transparencia intacta
		//	}

		//	// Mostrarla en pantalla.
		//	using var data = resized_image.Encode(SKEncodedImageFormat.Png, 100);
		//	var ms = new MemoryStream(data.ToArray());
		//	MyDisplayImage.Source = ImageSource.FromStream(
		//		() => ms
		//	);
		//}
		#endregion
	}
}
