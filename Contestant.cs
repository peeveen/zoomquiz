namespace ZoomQuiz
{
	public class Contestant
	{
		public string Name { get; private set; }
		public uint ID { get; private set; }
		public Contestant(uint id, string name)
		{
			ID = id;
			Name = name;
		}
		public override bool Equals(object obj)
		{
			if (obj is Contestant c2)
			{
				// ID is NOT constant between join/leave.
				return c2.Name == Name;
			}
			return false;
		}
		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}
	}
}
