namespace com.gaitanivan.maui.poryiaa
{
	/// <summary>
	/// Clase con la que se mantiene la información de una predicción realizada por el modelo YOLO.
	/// </summary>
	public class Prediction
	{
		#region Vars
		/// <summary>
		/// Posición X del rectángulo en el que se encontró el objeto.
		/// </summary>
		public float X { get; set; }
		/// <summary>
		/// Posición Y del rectángulo en el que se encontró el objeto.
		/// </summary>
		public float Y { get; set; }
		/// <summary>
		/// Ancho del rectángulo en el que se encontró el objeto.
		/// </summary>
		public float W { get; set; }
		/// <summary>
		/// Alto del rectángulo en el que se encontró el objeto.
		/// </summary>
		public float H { get; set; }
		/// <summary>
		/// El grosor de la línea del rectángulo.
		/// </summary>
		public float StrokeWidth { get; set; }
		/// <summary>
		/// Clase de objeto encontrado.
		/// </summary>
		public int Id { get; set; }

		private string _name = string.Empty;
		/// <summary>
		/// Nombre del objeto encontrado.
		/// </summary>
		public string Name
		{ 
			get
			{
				return _name;
			}
			set
			{
				_name = value;
			}
		}
		/// <summary>
		/// El puntaje de confianza que el modelo dá a que sea este objeto el encontrado.
		/// </summary>
		public float Score { get; set; }
		#endregion
	}
}
