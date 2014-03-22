namespace XmlStreamProcessor.Test
{
	class Subclass
	{
		public int SomeInt;
		public SubSubclass SubSubclass;
		public string Tag;

		public Subclass(int someInt)
		{
			SomeInt = someInt;
		}

		public override bool Equals(object obj)
		{
			if (obj == null || GetType() != obj.GetType())
			{
				return false;
			}
			var other = (Subclass)obj;

			return SomeInt == other.SomeInt
			       && Tag == other.Tag
			       && Equals(SubSubclass, other.SubSubclass);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}
	}
}