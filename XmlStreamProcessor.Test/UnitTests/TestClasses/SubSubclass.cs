namespace XmlStreamProcessor.Test
{
	class SubSubclass
	{
		public string SomeString;
		public string Tag;

		public override bool Equals(object obj)
		{
			if (obj == null || GetType() != obj.GetType())
			{
				return false;
			}
			var other = (SubSubclass)obj;

			return SomeString == other.SomeString
			       && Tag == other.Tag;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}
	}
}