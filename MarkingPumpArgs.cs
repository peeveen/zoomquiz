namespace ZoomQuiz
{
	class MarkingPumpArgs
	{
		public bool UseLevenshtein { get; private set; }
		public bool AutoCountdown { get; private set; }
		public MarkingPumpArgs(bool useLevenshtein, bool autoCountdown)
		{
			UseLevenshtein = useLevenshtein;
			AutoCountdown = autoCountdown;
		}
	}
}
