namespace Mathos.Parser
{
	using System;


	public sealed class CalculatorException : Exception
	{

		public CalculatorException(string message)
			: base(message)
		{
			Position = "?";
		}

		public CalculatorException(string message, string position)
			: base(message)
		{
			Position = position;
		}

		public CalculatorException(string message, Exception innerException)
			: base(message, innerException)
		{
			Position = "?";
		}

		public string Position { private set; get; }

		public override string Message =>
			string.Format("{0}, cell:{1}", base.Message, Position);


		/// <summary>
		/// Plain, non-localized message, used for logging.
		/// </summary>
		public string Text => base.Message;
	}
}
